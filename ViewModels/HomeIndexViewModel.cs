using System.Globalization;

namespace PureAvocadoes.ViewModels
{
    public class MovieCardViewModel
    {
        public int MovieId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public ushort ReleaseYear { get; set; }

        public ushort DurationMinutes { get; set; }

        public string Director { get; set; } = string.Empty;

        public string Synopsis { get; set; } = string.Empty;

        public string? PosterUrl { get; set; }

        public long TotalReviews { get; set; }

        public decimal AverageRating { get; set; }

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

        public static string? SanitizePosterUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var trimmed = url.Trim();
            if (trimmed.StartsWith("/", StringComparison.Ordinal)
                && !trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                return trimmed;
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.AbsoluteUri;
            }

            return null;
        }
    }

    public class HomeIndexViewModel
    {
        public MovieCardViewModel? FeaturedMovie { get; set; }

        public IReadOnlyList<MovieCardViewModel> RecentMovies { get; set; } = Array.Empty<MovieCardViewModel>();

        public IReadOnlyList<MovieCardViewModel> TopAvocadoPicks { get; set; } = Array.Empty<MovieCardViewModel>();

        public int TotalMovieCount { get; set; }

        public long TotalReviewCount { get; set; }

        public bool HasDatabaseError { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
