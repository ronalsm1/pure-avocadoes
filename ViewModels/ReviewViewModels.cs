using PureAvocadoes.Dtos;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace PureAvocadoes.ViewModels
{
    /// Form model for creating or editing a user's movie review via MVC Razor views.
    /// Reviewer identity is always derived from the authenticated principal server-side.
    public class ReviewFormViewModel
    {
        public int? ReviewId { get; set; }

        [Required(ErrorMessage = "Movie selection is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Movie selection is invalid.")]
        public int MovieId { get; set; }

        public string MovieTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a rating from 1 to 5.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int? Rating { get; set; }

        [Required(ErrorMessage = "Review text is required.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Review text must be between 10 and 2,000 characters.")]
        [Display(Name = "Written review")]
        public string ReviewText { get; set; } = string.Empty;

        public bool IsEdit => ReviewId.HasValue;

        public ReviewCreateDto ToCreateDto() => new()
        {
            MovieId = MovieId,
            Rating = Rating,
            ReviewText = ReviewText
        };

        public ReviewUpdateDto ToUpdateDto() => new()
        {
            Rating = Rating,
            ReviewText = ReviewText
        };
    }

    /// Single review entry rendered in the authenticated user's /Reviews/Mine list.
    public class MyReviewItemViewModel
    {
        public int ReviewId { get; set; }

        public MovieCardViewModel Movie { get; set; } = new();

        public byte Rating { get; set; }

        public string ReviewText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsEdited => UpdatedAt.HasValue;

        public string FormattedDate => (UpdatedAt ?? CreatedAt)
            .ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    /// ViewModel for the authenticated user's /Reviews/Mine page.
    public class MyReviewsViewModel
    {
        public IReadOnlyList<MyReviewItemViewModel> Reviews { get; set; } = Array.Empty<MyReviewItemViewModel>();

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }

        public int TotalCount => Reviews.Count;
    }

    /// Confirmation ViewModel for deleting a user-owned review (/Reviews/Delete/{id}).
    public class ReviewDeleteViewModel
    {
        public int ReviewId { get; set; }

        public int MovieId { get; set; }

        public string MovieTitle { get; set; } = string.Empty;

        public byte Rating { get; set; }

        public string ReviewText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsEdited => UpdatedAt.HasValue;

        public string FormattedDate => (UpdatedAt ?? CreatedAt)
            .ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }
}
