using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PureAvocadoes.Data;
using PureAvocadoes.Dtos;
using PureAvocadoes.Models;
using PureAvocadoes.ViewModels;

namespace PureAvocadoes.Services
{
    /// Admin-only user management operations shared by AdminUsersController and UsersApiController.
    /// Never exposes PasswordHash in any projection and enforces last-admin + FK deletion guards.
    public class AdminUserService
    {
        private readonly ApplicationDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AdminUserService(ApplicationDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        public async Task<ServiceResult<IReadOnlyList<UserDto>>> GetUsersAsync(
            string? search,
            string? role,
            CancellationToken cancellationToken = default)
        {
            var normalizedRole = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
            if (normalizedRole is not null && normalizedRole is not ("User" or "Admin"))
            {
                return ServiceResult<IReadOnlyList<UserDto>>.Fail(
                    ServiceOutcome.ValidationError,
                    "Role filter must be 'User' or 'Admin'.",
                    nameof(role));
            }

            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

            var query = _db.Users.AsNoTracking();

            if (normalizedRole is not null)
            {
                query = query.Where(u => u.Role == normalizedRole);
            }

            if (normalizedSearch is not null)
            {
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(normalizedSearch)
                    || u.Email.ToLower().Contains(normalizedSearch));
            }

            // shortcut: course-project user directory loads all matched users without server-side pagination.
            var users = await query
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.UserId)
                .Select(u => new UserDto(
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt,
                    u.Reviews.Count))
                .ToListAsync(cancellationToken);

            return ServiceResult<IReadOnlyList<UserDto>>.Success(users);
        }

        public async Task<UserDetailDto?> GetUserDetailsAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return null;
            }

            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return null;
            }

            var reviews = await _db.RatingsReviews
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ThenByDescending(r => r.ReviewId)
                .Select(r => new UserReviewSummaryDto(
                    r.ReviewId,
                    r.MovieId,
                    r.Movie.Title,
                    r.Rating,
                    r.ReviewText,
                    r.CreatedAt,
                    r.UpdatedAt))
                .ToListAsync(cancellationToken);

            return new UserDetailDto(
                user.UserId,
                user.FullName,
                user.Email,
                user.Role,
                user.CreatedAt,
                reviews.Count,
                reviews);
        }

        public async Task<AdminUserEditViewModel?> GetUserForEditAsync(
            int userId,
            int currentAdminUserId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return null;
            }

            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.UserId, u.FullName, u.Email, u.Role })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return null;
            }

            var adminCount = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "Admin", cancellationToken);

            return new AdminUserEditViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                IsCurrentUser = user.UserId == currentAdminUserId,
                IsOnlyAdmin = user.Role == "Admin" && adminCount <= 1
            };
        }

        public async Task<AdminUserDeleteViewModel?> GetUserForDeleteAsync(
            int userId,
            int currentAdminUserId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return null;
            }

            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new UserDto(
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt,
                    u.Reviews.Count))
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return null;
            }

            var adminCount = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "Admin", cancellationToken);

            return new AdminUserDeleteViewModel
            {
                User = user,
                IsCurrentUser = user.UserId == currentAdminUserId,
                IsOnlyAdmin = user.Role == "Admin" && adminCount <= 1
            };
        }

        public async Task<ServiceResult<UserDto>> CreateUserAsync(
            UserCreateDto input,
            CancellationToken cancellationToken = default)
        {
            var fullName = (input.FullName ?? string.Empty).Trim();
            var normalizedEmail = AccountService.NormalizeEmail(input.Email);
            var role = (input.Role ?? string.Empty).Trim();

            var errors = ValidateUserFields(fullName, normalizedEmail, role);
            if (string.IsNullOrEmpty(input.Password) || input.Password.Length < 8 || input.Password.Length > 100)
            {
                errors[nameof(UserCreateDto.Password)] = ["Password must be at least 8 characters."];
            }

            if (errors.Count > 0)
            {
                var first = errors.First();
                return ServiceResult<UserDto>.Fail(ServiceOutcome.ValidationError, first.Value[0], first.Key, errors);
            }

            var emailExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (emailExists)
            {
                return ServiceResult<UserDto>.Fail(
                    ServiceOutcome.Conflict,
                    "An account with this email already exists.",
                    nameof(UserCreateDto.Email));
            }

            var user = new User
            {
                FullName = fullName,
                Email = normalizedEmail,
                Role = role,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, input.Password);

            _db.Users.Add(user);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return ServiceResult<UserDto>.Fail(
                    ServiceOutcome.Conflict,
                    "An account with this email already exists.",
                    nameof(UserCreateDto.Email));
            }

            return ServiceResult<UserDto>.Success(
                new UserDto(user.UserId, user.FullName, user.Email, user.Role, user.CreatedAt, 0));
        }

        public async Task<ServiceResult<UserDto>> UpdateUserAsync(
            int targetUserId,
            int currentAdminUserId,
            UserUpdateDto input,
            CancellationToken cancellationToken = default)
        {
            if (targetUserId <= 0)
            {
                return ServiceResult<UserDto>.Fail(ServiceOutcome.NotFound, $"User with ID {targetUserId} was not found.");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == targetUserId, cancellationToken);
            if (user is null)
            {
                return ServiceResult<UserDto>.Fail(ServiceOutcome.NotFound, $"User with ID {targetUserId} was not found.");
            }

            var fullName = (input.FullName ?? string.Empty).Trim();
            var normalizedEmail = AccountService.NormalizeEmail(input.Email);
            var role = (input.Role ?? string.Empty).Trim();

            var errors = ValidateUserFields(fullName, normalizedEmail, role);
            if (errors.Count > 0)
            {
                var first = errors.First();
                return ServiceResult<UserDto>.Fail(ServiceOutcome.ValidationError, first.Value[0], first.Key, errors);
            }

            var emailExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email == normalizedEmail && u.UserId != targetUserId, cancellationToken);

            if (emailExists)
            {
                return ServiceResult<UserDto>.Fail(
                    ServiceOutcome.Conflict,
                    "An account with this email already exists.",
                    nameof(UserUpdateDto.Email));
            }

            if (string.Equals(user.Role, "Admin", StringComparison.Ordinal)
                && !string.Equals(role, "Admin", StringComparison.Ordinal))
            {
                if (targetUserId == currentAdminUserId)
                {
                    return ServiceResult<UserDto>.Fail(
                        ServiceOutcome.Conflict,
                        "You cannot demote your own currently signed-in Admin account.",
                        nameof(UserUpdateDto.Role));
                }

                var adminCount = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "Admin", cancellationToken);
                if (adminCount <= 1)
                {
                    return ServiceResult<UserDto>.Fail(
                        ServiceOutcome.Conflict,
                        "Cannot demote the last remaining Admin account.",
                        nameof(UserUpdateDto.Role));
                }
            }

            user.FullName = fullName;
            user.Email = normalizedEmail;
            user.Role = role;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return ServiceResult<UserDto>.Fail(
                    ServiceOutcome.Conflict,
                    "An account with this email already exists.",
                    nameof(UserUpdateDto.Email));
            }

            var reviewCount = await _db.RatingsReviews
                .AsNoTracking()
                .CountAsync(r => r.UserId == targetUserId, cancellationToken);

            return ServiceResult<UserDto>.Success(
                new UserDto(user.UserId, user.FullName, user.Email, user.Role, user.CreatedAt, reviewCount));
        }

        public async Task<ServiceResult> DeleteUserAsync(
            int targetUserId,
            int currentAdminUserId,
            CancellationToken cancellationToken = default)
        {
            if (targetUserId <= 0)
            {
                return ServiceResult.Fail(ServiceOutcome.NotFound, $"User with ID {targetUserId} was not found.");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == targetUserId, cancellationToken);
            if (user is null)
            {
                return ServiceResult.Fail(ServiceOutcome.NotFound, $"User with ID {targetUserId} was not found.");
            }

            if (targetUserId == currentAdminUserId)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.Conflict,
                    "You cannot delete your own currently signed-in Admin account.");
            }

            if (string.Equals(user.Role, "Admin", StringComparison.Ordinal))
            {
                var adminCount = await _db.Users.AsNoTracking().CountAsync(u => u.Role == "Admin", cancellationToken);
                if (adminCount <= 1)
                {
                    return ServiceResult.Fail(
                        ServiceOutcome.Conflict,
                        "Cannot delete the last remaining Admin account.");
                }
            }

            var dependentReviewCount = await _db.RatingsReviews
                .AsNoTracking()
                .CountAsync(r => r.UserId == targetUserId, cancellationToken);

            if (dependentReviewCount > 0)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.Conflict,
                    $"Cannot delete \"{user.FullName}\" because this account has {dependentReviewCount} dependent movie {(dependentReviewCount == 1 ? "review" : "reviews")}. Existing review history is preserved under ON DELETE RESTRICT.");
            }

            _db.Users.Remove(user);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return ServiceResult.Success();
            }
            catch (DbUpdateException)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.Conflict,
                    $"Cannot delete \"{user.FullName}\" because dependent movie reviews still reference this account.");
            }
        }

        private static Dictionary<string, string[]> ValidateUserFields(string fullName, string normalizedEmail, string role)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (fullName.Length < 2 || fullName.Length > 120)
            {
                errors[nameof(UserUpdateDto.FullName)] = ["Full name must be between 2 and 120 characters."];
            }

            if (!AccountService.IsValidEmailFormat(normalizedEmail))
            {
                errors[nameof(UserUpdateDto.Email)] = ["Enter a valid email address."];
            }

            if (role is not ("User" or "Admin"))
            {
                errors[nameof(UserUpdateDto.Role)] = ["Role must be either 'User' or 'Admin'."];
            }

            return errors;
        }
    }
}
