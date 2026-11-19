using System.ComponentModel.DataAnnotations;

namespace PureAvocadoes.Dtos
{
    /// Safe public/user API representation of a movie review.
    /// Excludes sensitive user credentials such as email and password hash.
    public sealed record ReviewDto(
        int ReviewId,
        int MovieId,
        string MovieTitle,
        int UserId,
        string ReviewerName,
        byte Rating,
        string ReviewText,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    /// API request payload for creating a review (POST /api/reviews).
    /// Reviewer identity is always resolved server-side from the authenticated principal.
    public sealed class ReviewCreateDto
    {
        [Required(ErrorMessage = "Movie ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Movie ID must be a positive integer.")]
        public int? MovieId { get; set; }

        [Required(ErrorMessage = "Please select a rating from 1 to 5.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int? Rating { get; set; }

        [Required(ErrorMessage = "Review text is required.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Review text must be between 10 and 2,000 characters.")]
        public string ReviewText { get; set; } = string.Empty;
    }

    /// API request payload for updating an existing review (PUT /api/reviews/{id}).
    public sealed class ReviewUpdateDto
    {
        [Required(ErrorMessage = "Please select a rating from 1 to 5.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int? Rating { get; set; }

        [Required(ErrorMessage = "Review text is required.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Review text must be between 10 and 2,000 characters.")]
        public string ReviewText { get; set; } = string.Empty;
    }
}
