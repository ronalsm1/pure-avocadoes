using PureAvocadoes.Dtos;
using System.ComponentModel.DataAnnotations;

namespace PureAvocadoes.ViewModels
{
    /// Admin User Management index listing (/Admin/Users).
    public class AdminUsersIndexViewModel
    {
        public IReadOnlyList<UserDto> Users { get; set; } = Array.Empty<UserDto>();

        public string? Search { get; set; }

        public string? Role { get; set; }

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }
    }

    /// Admin User Creation form (/Admin/Users/Create).
    public class AdminUserCreateViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 120 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm the password.")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        [RegularExpression("^(User|Admin)$", ErrorMessage = "Role must be either 'User' or 'Admin'.")]
        [Display(Name = "Account role")]
        public string Role { get; set; } = "User";

        public UserCreateDto ToCreateDto() => new()
        {
            FullName = FullName,
            Email = Email,
            Password = Password,
            Role = Role
        };
    }

    /// Admin User Edit form (/Admin/Users/Edit/{id}).
    /// Permits editing FullName, Email, and Role only; never edits or exposes PasswordHash.
    public class AdminUserEditViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 120 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        [RegularExpression("^(User|Admin)$", ErrorMessage = "Role must be either 'User' or 'Admin'.")]
        [Display(Name = "Account role")]
        public string Role { get; set; } = "User";

        public bool IsCurrentUser { get; set; }

        public bool IsOnlyAdmin { get; set; }

        public UserUpdateDto ToUpdateDto() => new()
        {
            FullName = FullName,
            Email = Email,
            Role = Role
        };
    }

    /// Admin User Delete confirmation page (/Admin/Users/Delete/{id}).
    public class AdminUserDeleteViewModel
    {
        public UserDto User { get; set; } = new(0, string.Empty, string.Empty, "User", DateTime.UtcNow, 0);

        public bool IsCurrentUser { get; set; }

        public bool IsOnlyAdmin { get; set; }

        public bool CanDelete => !IsCurrentUser && !IsOnlyAdmin && User.ReviewCount == 0;
    }
}
