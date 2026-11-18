using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace PureAvocadoes.ViewModels
{
    public class ProfileUpdateViewModel
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
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "New password must be at least 8 characters.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password.")]
        [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public class ProfilePageViewModel
    {
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; }

        public int ReviewCount { get; set; }

        public ProfileUpdateViewModel UpdateForm { get; set; } = new();

        public ChangePasswordViewModel PasswordForm { get; set; } = new();

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }

        public string FormattedMemberSince => CreatedAt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }
}
