using System.Globalization;

namespace PureAvocadoes.ViewModels
{
    /// Read-only movie summary row displayed on the Admin Dashboard.
    public class AdminDashboardMovieItemViewModel
    {
        public int MovieId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public ushort ReleaseYear { get; set; }

        public long TotalReviews { get; set; }

        public decimal AverageRating { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool HasReviews => TotalReviews > 0;

        public string FormattedScore => HasReviews
            ? AverageRating.ToString("0.00", CultureInfo.InvariantCulture)
            : "--";

        public string ReviewCountLabel => TotalReviews switch
        {
            0 => "No reviews yet",
            1 => "1 review",
            _ => $"{TotalReviews} reviews"
        };

        public string FormattedAddedDate => CreatedAt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    /// Safe recent review activity projection for the Admin Dashboard (excludes reviewer email/credentials).
    public class AdminDashboardReviewItemViewModel
    {
        public int ReviewId { get; set; }

        public int MovieId { get; set; }

        public string MovieTitle { get; set; } = string.Empty;

        public string ReviewerName { get; set; } = string.Empty;

        public byte Rating { get; set; }

        public string Excerpt { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsEdited => UpdatedAt.HasValue;

        public string FormattedDate => (UpdatedAt ?? CreatedAt).ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    /// Safe recently registered user summary for the Admin Dashboard (excludes email and PasswordHash).
    public class AdminDashboardUserItemViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; }

        public int ReviewCount { get; set; }

        public string FormattedJoinedDate => CreatedAt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    /// Aggregate database-backed overview displayed on /Admin.
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }

        public int RegularUserCount { get; set; }

        public int AdminUserCount { get; set; }

        public int TotalMovies { get; set; }

        public int RatedMovieCount { get; set; }

        public int TotalReviews { get; set; }

        public decimal? OverallAverageRating { get; set; }

        public IReadOnlyList<AdminDashboardMovieItemViewModel> RecentMovies { get; set; } =
            Array.Empty<AdminDashboardMovieItemViewModel>();

        public IReadOnlyList<AdminDashboardMovieItemViewModel> HighestRatedMovies { get; set; } =
            Array.Empty<AdminDashboardMovieItemViewModel>();

        public IReadOnlyList<AdminDashboardReviewItemViewModel> RecentReviews { get; set; } =
            Array.Empty<AdminDashboardReviewItemViewModel>();

        public IReadOnlyList<AdminDashboardUserItemViewModel> RecentUsers { get; set; } =
            Array.Empty<AdminDashboardUserItemViewModel>();

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }

        public bool HasReviews => TotalReviews > 0 && OverallAverageRating.HasValue;

        public string FormattedOverallAverage => HasReviews
            ? OverallAverageRating!.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "--";
    }
}
