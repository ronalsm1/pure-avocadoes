using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PureAvocadoes.Data;
using PureAvocadoes.Models;
using PureAvocadoes.ViewModels;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;

namespace PureAvocadoes.Services
{
    public sealed record AuthenticatedUserInfo(int UserId, string FullName, string Email, string Role);

    public sealed record AuthOperationResult(
        bool Succeeded,
        AuthenticatedUserInfo? User = null,
        string? ErrorMessage = null,
        string? ErrorField = null);

    /// Handles user registration, password verification, email normalization, and safe cookie claim creation.
    public class AccountService
    {
        private const string GenericLoginError = "Invalid email or password.";
        private static readonly EmailAddressAttribute EmailValidator = new();

        private readonly ApplicationDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountService(ApplicationDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        public static string NormalizeEmail(string? email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        public static bool IsValidEmailFormat(string? normalizedEmail) =>
            !string.IsNullOrWhiteSpace(normalizedEmail)
            && normalizedEmail.Length <= 254
            && EmailValidator.IsValid(normalizedEmail);

        public async Task<AuthOperationResult> RegisterAsync(
            RegisterViewModel model,
            CancellationToken cancellationToken = default)
        {
            var fullName = (model.FullName ?? string.Empty).Trim();
            var normalizedEmail = NormalizeEmail(model.Email);

            if (fullName.Length < 2 || fullName.Length > 120)
            {
                return new AuthOperationResult(
                    false,
                    ErrorMessage: "Full name must be between 2 and 120 characters.",
                    ErrorField: nameof(RegisterViewModel.FullName));
            }

            if (string.IsNullOrWhiteSpace(normalizedEmail)
                || normalizedEmail.Length > 254
                || !EmailValidator.IsValid(normalizedEmail))
            {
                return new AuthOperationResult(
                    false,
                    ErrorMessage: "Enter a valid email address.",
                    ErrorField: nameof(RegisterViewModel.Email));
            }

            if (string.IsNullOrEmpty(model.Password) || model.Password.Length < 8 || model.Password.Length > 100)
            {
                return new AuthOperationResult(
                    false,
                    ErrorMessage: "Password must be at least 8 characters.",
                    ErrorField: nameof(RegisterViewModel.Password));
            }

            if (!string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
            {
                return new AuthOperationResult(
                    false,
                    ErrorMessage: "Passwords do not match.",
                    ErrorField: nameof(RegisterViewModel.ConfirmPassword));
            }

            var emailExists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
            if (emailExists)
            {
                return new AuthOperationResult(
                    false,
                    ErrorMessage: "An account with this email already exists.",
                    ErrorField: nameof(RegisterViewModel.Email));
            }

            var user = new User
            {
                FullName = fullName,
                Email = normalizedEmail,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return new AuthOperationResult(
                    false,
                    ErrorMessage: "An account with this email already exists.",
                    ErrorField: nameof(RegisterViewModel.Email));
            }

            return new AuthOperationResult(
                true,
                new AuthenticatedUserInfo(user.UserId, user.FullName, user.Email, user.Role));
        }

        public async Task<AuthOperationResult> AuthenticateAsync(
            string? email,
            string? password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrEmpty(password))
            {
                return new AuthOperationResult(false, ErrorMessage: GenericLoginError);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
            if (user is null)
            {
                return new AuthOperationResult(false, ErrorMessage: GenericLoginError);
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return new AuthOperationResult(false, ErrorMessage: GenericLoginError);
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new AuthOperationResult(
                true,
                new AuthenticatedUserInfo(user.UserId, user.FullName, user.Email, user.Role));
        }

        public ClaimsPrincipal CreateClaimsPrincipal(AuthenticatedUserInfo user)
        {
            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new ClaimsPrincipal(identity);
        }

        public async Task<ProfilePageViewModel?> GetProfilePageAsync(
            int authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            if (authenticatedUserId <= 0)
            {
                return null;
            }

            var profile = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == authenticatedUserId)
                .Select(u => new
                {
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt,
                    ReviewCount = u.Reviews.Count
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (profile is null)
            {
                return null;
            }

            return new ProfilePageViewModel
            {
                FullName = profile.FullName,
                Email = profile.Email,
                Role = profile.Role,
                CreatedAt = profile.CreatedAt,
                ReviewCount = profile.ReviewCount,
                UpdateForm = new ProfileUpdateViewModel
                {
                    FullName = profile.FullName,
                    Email = profile.Email
                }
            };
        }

        public async Task<ServiceResult<AuthenticatedUserInfo>> UpdateOwnProfileAsync(
            int authenticatedUserId,
            ProfileUpdateViewModel model,
            CancellationToken cancellationToken = default)
        {
            if (authenticatedUserId <= 0)
            {
                return ServiceResult<AuthenticatedUserInfo>.Fail(
                    ServiceOutcome.Forbidden,
                    "Authenticated user identity is required.");
            }

            var fullName = (model.FullName ?? string.Empty).Trim();
            var normalizedEmail = NormalizeEmail(model.Email);

            if (fullName.Length < 2 || fullName.Length > 120)
            {
                return ServiceResult<AuthenticatedUserInfo>.Fail(
                    ServiceOutcome.ValidationError,
                    "Full name must be between 2 and 120 characters.",
                    nameof(ProfileUpdateViewModel.FullName));
            }

            if (!IsValidEmailFormat(normalizedEmail))
            {
                return ServiceResult<AuthenticatedUserInfo>.Fail(
                    ServiceOutcome.ValidationError,
                    "Enter a valid email address.",
                    nameof(ProfileUpdateViewModel.Email));
            }

            var duplicateExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Email == normalizedEmail && u.UserId != authenticatedUserId, cancellationToken);

            if (duplicateExists)
            {
                return ServiceResult<AuthenticatedUserInfo>.Fail(
                    ServiceOutcome.Conflict,
                    "An account with this email already exists.",
                    nameof(ProfileUpdateViewModel.Email));
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == authenticatedUserId, cancellationToken);
            if (user is null)
            {
                return ServiceResult<AuthenticatedUserInfo>.Fail(
                    ServiceOutcome.NotFound,
                    "Your user account could not be found.");
            }

            user.FullName = fullName;
            user.Email = normalizedEmail;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return ServiceResult<AuthenticatedUserInfo>.Fail(
                    ServiceOutcome.Conflict,
                    "An account with this email already exists.",
                    nameof(ProfileUpdateViewModel.Email));
            }

            return ServiceResult<AuthenticatedUserInfo>.Success(
                new AuthenticatedUserInfo(user.UserId, user.FullName, user.Email, user.Role));
        }

        public async Task<ServiceResult> ChangeOwnPasswordAsync(
            int authenticatedUserId,
            ChangePasswordViewModel model,
            CancellationToken cancellationToken = default)
        {
            if (authenticatedUserId <= 0)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.Forbidden,
                    "Authenticated user identity is required.");
            }

            if (string.IsNullOrEmpty(model.CurrentPassword))
            {
                return ServiceResult.Fail(
                    ServiceOutcome.ValidationError,
                    "Current password is required.",
                    nameof(ChangePasswordViewModel.CurrentPassword));
            }

            if (string.IsNullOrEmpty(model.NewPassword) || model.NewPassword.Length < 8 || model.NewPassword.Length > 100)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.ValidationError,
                    "New password must be at least 8 characters.",
                    nameof(ChangePasswordViewModel.NewPassword));
            }

            if (!string.Equals(model.NewPassword, model.ConfirmNewPassword, StringComparison.Ordinal))
            {
                return ServiceResult.Fail(
                    ServiceOutcome.ValidationError,
                    "New password and confirmation do not match.",
                    nameof(ChangePasswordViewModel.ConfirmNewPassword));
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == authenticatedUserId, cancellationToken);
            if (user is null)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.NotFound,
                    "Your user account could not be found.");
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.ValidationError,
                    "Current password is incorrect.",
                    nameof(ChangePasswordViewModel.CurrentPassword));
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            await _db.SaveChangesAsync(cancellationToken);

            return ServiceResult.Success();
        }
    }
}
