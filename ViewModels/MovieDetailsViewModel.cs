using System.Globalization;

namespace PureAvocadoes.ViewModels
{
    public class PublicReviewItemViewModel
    {
        public int ReviewId { get; set; }

        public int UserId { get; set; }

        public string ReviewerName { get; set; } = string.Empty;

        public byte Rating { get; set; }

        public string ReviewText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsOwnedByCurrentUser { get; set; }

        public string ReviewerInitial => string.IsNullOrWhiteSpace(ReviewerName)
            ? "?"
            : ReviewerName.Trim()[0].ToString().ToUpperInvariant();

        public string FormattedDate => (UpdatedAt ?? CreatedAt)
            .ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

        public bool IsEdited => UpdatedAt.HasValue;
    }

    public class MovieDetailsViewModel
    {
        public MovieCardViewModel Movie { get; set; } = new();

        public IReadOnlyList<PublicReviewItemViewModel> Reviews { get; set; } = Array.Empty<PublicReviewItemViewModel>();

        public IReadOnlyList<MovieCardViewModel> TopRatedSidebarMovies { get; set; } = Array.Empty<MovieCardViewModel>();

        public PublicReviewItemViewModel? CurrentUserReview { get; set; }

        public bool HasReviewedByCurrentUser => CurrentUserReview is not null;

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
