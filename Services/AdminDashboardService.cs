using Microsoft.EntityFrameworkCore;
using PureAvocadoes.Data;
using PureAvocadoes.ViewModels;

namespace PureAvocadoes.Services
{
    /// Queries database-backed platform statistics, recent catalog/user activity,
    /// and SQL View summaries (vw_movie_rating_summary, vw_movie_review_details)
    /// for the Admin Dashboard (/Admin) without full-table memory scans.
    public class AdminDashboardService
    {
        private const int DefaultRecentLimit = 5;
        private readonly ApplicationDbContext _db;

        public AdminDashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<AdminDashboardViewModel> GetDashboardAsync(
            CancellationToken cancellationToken = default)
        {
            var roleCounts = await _db.Users
                .AsNoTracking()
                .GroupBy(u => u.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var adminCount = roleCounts
                .Where(x => string.Equals(x.Role, "Admin", StringComparison.Ordinal))
                .Sum(x => x.Count);
            var regularUserCount = roleCounts
                .Where(x => string.Equals(x.Role, "User", StringComparison.Ordinal))
                .Sum(x => x.Count);
            var totalUsers = roleCounts.Sum(x => x.Count);

            var recentUsers = await _db.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .ThenByDescending(u => u.UserId)
                .Take(DefaultRecentLimit)
                .Select(u => new AdminDashboardUserItemViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    ReviewCount = u.Reviews.Count
                })
                .ToListAsync(cancellationToken);

            var totalMovies = await _db.Movies
                .AsNoTracking()
                .CountAsync(cancellationToken);

            var totalReviews = await _db.RatingsReviews
                .AsNoTracking()
                .CountAsync(cancellationToken);

            decimal? overallAverageRating = totalReviews > 0
                ? Math.Round(
                    await _db.RatingsReviews
                        .AsNoTracking()
                        .AverageAsync(r => (decimal)r.Rating, cancellationToken),
                    2,
                    MidpointRounding.AwayFromZero)
                : null;

            var isRelational = _db.Database.IsRelational();

            var recentMoviesRaw = await _db.Movies
                .AsNoTracking()
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.MovieId)
                .Take(DefaultRecentLimit)
                .Select(m => new
                {
                    m.MovieId,
                    m.Title,
                    m.Genre,
                    m.ReleaseYear,
                    m.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var recentMovieIds = recentMoviesRaw.Select(m => m.MovieId).ToList();
            var recentMovieRatings = await MovieService.LoadRatingSummariesAsync(_db, recentMovieIds, cancellationToken);

            var recentMovies = recentMoviesRaw
                .Select(m =>
                {
                    recentMovieRatings.TryGetValue(m.MovieId, out var rating);
                    return new AdminDashboardMovieItemViewModel
                    {
                        MovieId = m.MovieId,
                        Title = m.Title,
                        Genre = m.Genre,
                        ReleaseYear = m.ReleaseYear,
                        CreatedAt = m.CreatedAt,
                        TotalReviews = rating.TotalReviews,
                        AverageRating = rating.AverageRating
                    };
                })
                .ToList();

            int ratedMovieCount;
            List<AdminDashboardMovieItemViewModel> highestRatedMovies;

            if (isRelational)
            {
                ratedMovieCount = await _db.MovieRatingSummaries
                    .AsNoTracking()
                    .CountAsync(s => s.TotalReviews > 0, cancellationToken);

                var topRatedSummaries = await _db.MovieRatingSummaries
                    .AsNoTracking()
                    .Where(s => s.TotalReviews > 0)
                    .OrderByDescending(s => s.AverageRating)
                    .ThenByDescending(s => s.TotalReviews)
                    .ThenBy(s => s.Title)
                    .Take(DefaultRecentLimit)
                    .Select(s => new { s.MovieId, s.Title, s.TotalReviews, s.AverageRating })
                    .ToListAsync(cancellationToken);

                var topRatedIds = topRatedSummaries.Select(s => s.MovieId).ToList();
                var topRatedMeta = topRatedIds.Count == 0
                    ? new Dictionary<int, (string Genre, ushort ReleaseYear, DateTime CreatedAt)>()
                    : (await _db.Movies
                        .AsNoTracking()
                        .Where(m => topRatedIds.Contains(m.MovieId))
                        .Select(m => new { m.MovieId, m.Genre, m.ReleaseYear, m.CreatedAt })
                        .ToListAsync(cancellationToken))
                        .ToDictionary(m => m.MovieId, m => (m.Genre, m.ReleaseYear, m.CreatedAt));

                highestRatedMovies = topRatedSummaries
                    .Select(s =>
                    {
                        topRatedMeta.TryGetValue(s.MovieId, out var meta);
                        return new AdminDashboardMovieItemViewModel
                        {
                            MovieId = s.MovieId,
                            Title = s.Title,
                            Genre = meta.Genre ?? string.Empty,
                            ReleaseYear = meta.ReleaseYear,
                            CreatedAt = meta.CreatedAt,
                            TotalReviews = s.TotalReviews,
                            AverageRating = s.AverageRating
                        };
                    })
                    .ToList();
            }
            else
            {
                ratedMovieCount = await _db.RatingsReviews
                    .AsNoTracking()
                    .Select(r => r.MovieId)
                    .Distinct()
                    .CountAsync(cancellationToken);

                var topRatedAgg = await _db.RatingsReviews
                    .AsNoTracking()
                    .GroupBy(r => r.MovieId)
                    .Select(g => new
                    {
                        MovieId = g.Key,
                        TotalReviews = (long)g.Count(),
                        AverageRating = Math.Round((decimal)g.Average(r => r.Rating), 2, MidpointRounding.AwayFromZero)
                    })
                    .OrderByDescending(x => x.AverageRating)
                    .ThenByDescending(x => x.TotalReviews)
                    .Take(DefaultRecentLimit)
                    .ToListAsync(cancellationToken);

                var topIds = topRatedAgg.Select(x => x.MovieId).ToList();
                var topMoviesById = topIds.Count == 0
                    ? new Dictionary<int, (string Title, string Genre, ushort ReleaseYear, DateTime CreatedAt)>()
                    : (await _db.Movies
                        .AsNoTracking()
                        .Where(m => topIds.Contains(m.MovieId))
                        .Select(m => new { m.MovieId, m.Title, m.Genre, m.ReleaseYear, m.CreatedAt })
                        .ToListAsync(cancellationToken))
                        .ToDictionary(m => m.MovieId, m => (m.Title, m.Genre, m.ReleaseYear, m.CreatedAt));

                highestRatedMovies = topRatedAgg
                    .Where(x => topMoviesById.ContainsKey(x.MovieId))
                    .Select(x =>
                    {
                        var meta = topMoviesById[x.MovieId];
                        return new AdminDashboardMovieItemViewModel
                        {
                            MovieId = x.MovieId,
                            Title = meta.Title,
                            Genre = meta.Genre,
                            ReleaseYear = meta.ReleaseYear,
                            CreatedAt = meta.CreatedAt,
                            TotalReviews = x.TotalReviews,
                            AverageRating = x.AverageRating
                        };
                    })
                    .OrderByDescending(m => m.AverageRating)
                    .ThenByDescending(m => m.TotalReviews)
                    .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var recentReviewRows = isRelational
                ? await _db.MovieReviewDetails
                    .AsNoTracking()
                    .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .ThenByDescending(r => r.ReviewId)
                    .Take(DefaultRecentLimit)
                    .Select(r => new
                    {
                        r.ReviewId,
                        r.MovieId,
                        r.MovieTitle,
                        r.ReviewerName,
                        r.Rating,
                        r.ReviewText,
                        r.CreatedAt,
                        r.UpdatedAt
                    })
                    .ToListAsync(cancellationToken)
                : await _db.RatingsReviews
                    .AsNoTracking()
                    .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .ThenByDescending(r => r.ReviewId)
                    .Take(DefaultRecentLimit)
                    .Select(r => new
                    {
                        r.ReviewId,
                        r.MovieId,
                        MovieTitle = r.Movie.Title,
                        ReviewerName = r.User.FullName,
                        r.Rating,
                        r.ReviewText,
                        r.CreatedAt,
                        r.UpdatedAt
                    })
                    .ToListAsync(cancellationToken);

            var recentReviews = recentReviewRows
                .Select(r => new AdminDashboardReviewItemViewModel
                {
                    ReviewId = r.ReviewId,
                    MovieId = r.MovieId,
                    MovieTitle = r.MovieTitle,
                    ReviewerName = r.ReviewerName,
                    Rating = r.Rating,
                    Excerpt = CreateExcerpt(r.ReviewText),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToList();

            return new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                RegularUserCount = regularUserCount,
                AdminUserCount = adminCount,
                TotalMovies = totalMovies,
                RatedMovieCount = ratedMovieCount,
                TotalReviews = totalReviews,
                OverallAverageRating = overallAverageRating,
                RecentMovies = recentMovies,
                HighestRatedMovies = highestRatedMovies,
                RecentReviews = recentReviews,
                RecentUsers = recentUsers
            };
        }

        private static string CreateExcerpt(string? reviewText)
        {
            if (string.IsNullOrWhiteSpace(reviewText))
            {
                return string.Empty;
            }

            var singleLine = string.Join(" ", reviewText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (singleLine.Length <= 140)
            {
                return singleLine;
            }

            return string.Concat(singleLine.AsSpan(0, 137).TrimEnd(), "...");
        }
    }
}
