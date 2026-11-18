using System.ComponentModel.DataAnnotations;

namespace PureAvocadoes.Dtos
{
    public sealed record UserDto(
        int UserId,
        string FullName,
        string Email,
        string Role,
        DateTime CreatedAt,
        int ReviewCount);

    public sealed record UserReviewSummaryDto(
        int ReviewId,
        int MovieId,
        string MovieTitle,
        byte Rating,
        string ReviewText,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public sealed record UserDetailDto(
        int UserId,
        string FullName,
        string Email,
        string Role,
        DateTime CreatedAt,
        int ReviewCount,
        IReadOnlyList<UserReviewSummaryDto> Reviews);

    public sealed class UserCreateDto
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 120 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        [RegularExpression("^(User|Admin)$", ErrorMessage = "Role must be either 'User' or 'Admin'.")]
        public string Role { get; set; } = "User";
    }

    public sealed class UserUpdateDto
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 120 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        [RegularExpression("^(User|Admin)$", ErrorMessage = "Role must be either 'User' or 'Admin'.")]
        public string Role { get; set; } = "User";
    }
}
