using Microsoft.EntityFrameworkCore;
using PureAvocadoes.Data;
using PureAvocadoes.Dtos;
using PureAvocadoes.Models;
using PureAvocadoes.ViewModels;
using System.Globalization;

namespace PureAvocadoes.Services
{
    /// Shared catalog query logic used across MVC controllers and API endpoints.
    /// Uses the vw_movie_rating_summary SQL View on relational MySQL providers.
    public class MovieService
    {
        private readonly ApplicationDbContext _db;

        public MovieService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<HomeIndexViewModel> GetHomePageDataAsync(
            int recentLimit = 4,
            CancellationToken cancellationToken = default)
        {
            var totalMovieCount = await _db.Movies
                .AsNoTracking()
                .CountAsync(cancellationToken);

            if (totalMovieCount == 0)
            {
                return new HomeIndexViewModel
                {
                    TotalMovieCount = 0,
                    TotalReviewCount = 0
                };
            }

            var totalReviewCount = await _db.RatingsReviews
                .AsNoTracking()
                .LongCountAsync(cancellationToken);

            var topPicks = totalReviewCount > 0
                ? await LoadTopRatedCardsAsync(8, excludeMovieId: null, cancellationToken)
                : Array.Empty<MovieCardViewModel>();

            MovieCardViewModel featuredCard;
            if (topPicks.Count > 0)
            {
                featuredCard = topPicks[0];
            }
            else
            {
                var fallbackFeatured = await _db.Movies
                    .AsNoTracking()
                    .OrderByDescending(m => m.ReleaseYear)
                    .ThenBy(m => m.MovieId)
                    .FirstAsync(cancellationToken);
                featuredCard = ToCardViewModel(fallbackFeatured, new Dictionary<int, (long, decimal)>());
            }

            var recentMovies = recentLimit > 0
                ? await _db.Movies
                    .AsNoTracking()
                    .Where(m => m.MovieId != featuredCard.MovieId)
                    .OrderByDescending(m => m.ReleaseYear)
                    .ThenByDescending(m => m.MovieId)
                    .Take(recentLimit)
                    .ToListAsync(cancellationToken)
                : [];

            var recentSummaries = await LoadRatingSummaryLookupAsync(
                recentMovies.Select(m => m.MovieId).ToArray(),
                cancellationToken);

            return new HomeIndexViewModel
            {
                FeaturedMovie = featuredCard,
                RecentMovies = recentMovies.Select(m => ToCardViewModel(m, recentSummaries)).ToList(),
                TopAvocadoPicks = topPicks,
                TotalMovieCount = totalMovieCount,
                TotalReviewCount = totalReviewCount
            };
        }

        public Task<MovieListViewModel> GetCatalogAsync(
            string? search,
            string? genre,
            string? releaseYearRaw,
            CancellationToken cancellationToken = default)
        {
            return GetCatalogAsync(search, genre, releaseYearRaw, sort: null, cancellationToken);
        }

        public async Task<MovieListViewModel> GetCatalogAsync(
            string? search,
            string? genre,
            string? releaseYearRaw,
            string? sort,
            CancellationToken cancellationToken = default)
        {
            var metadata = await _db.Movies
                .AsNoTracking()
                .Select(m => new { m.Genre, m.ReleaseYear })
                .ToListAsync(cancellationToken);

            var availableGenres = metadata
                .SelectMany(m => SplitGenres(m.Genre))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var availableYears = metadata
                .Select(m => (int)m.ReleaseYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            if (normalizedSearch is { Length: > 200 })
            {
                normalizedSearch = normalizedSearch[..200];
            }

            var normalizedGenre = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim();
            var normalizedSort = NormalizeCatalogSort(sort);

            int? selectedYear = null;
            string? filterWarning = null;
            if (!string.IsNullOrWhiteSpace(releaseYearRaw))
            {
                if (int.TryParse(releaseYearRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedYear)
                    && parsedYear >= 1888
                    && parsedYear <= 9999)
                {
                    selectedYear = parsedYear;
                }
                else
                {
                    filterWarning = "Invalid release year filter was ignored. Choose a year of 1888 or later.";
                }
            }

            IQueryable<Movie> query = _db.Movies.AsNoTracking();

            if (normalizedSearch is not null)
            {
                var searchLower = normalizedSearch.ToLowerInvariant();
                query = query.Where(m => m.Title.ToLower().Contains(searchLower));
            }

            if (selectedYear.HasValue)
            {
                var yearValue = (ushort)selectedYear.Value;
                query = query.Where(m => m.ReleaseYear == yearValue);
            }

            IReadOnlyList<MovieCardViewModel> cards;
            if (string.Equals(normalizedSort, "top-rated", StringComparison.Ordinal))
            {
                if (_db.Database.IsRelational())
                {
                    var ratedRows = await (
                        from m in query
                        join s in _db.MovieRatingSummaries.AsNoTracking() on m.MovieId equals s.MovieId
                        orderby s.AverageRating descending, s.TotalReviews descending, m.ReleaseYear descending, m.Title ascending, m.MovieId ascending
                        select new { Movie = m, s.TotalReviews, s.AverageRating })
                        .ToListAsync(cancellationToken);
                        if (normalizedGenre is not null)
                        {
                            ratedRows = ratedRows
                                .Where(r => MovieHasGenre(r.Movie.Genre, normalizedGenre))
                                .ToList();
                        }

                    cards = ratedRows
                        .Select(r => ToCardViewModel(
                            r.Movie,
                            new Dictionary<int, (long TotalReviews, decimal AverageRating)>
                            {
                                [r.Movie.MovieId] = (r.TotalReviews, r.AverageRating)
                            }))
                        .ToList();
                }
                else
                {
                    var matchedMovies = await query.ToListAsync(cancellationToken);
                    if (normalizedGenre is not null)
                    {
                        matchedMovies = matchedMovies
                            .Where(m => MovieHasGenre(m.Genre, normalizedGenre))
                            .ToList();
                    }
                    var ratingSummaries = await LoadRatingSummaryLookupAsync(
                        matchedMovies.Select(m => m.MovieId).ToArray(),
                        cancellationToken);

                    cards = matchedMovies
                        .OrderByDescending(m => ratingSummaries.TryGetValue(m.MovieId, out var s) && s.TotalReviews > 0 ? s.AverageRating : 0m)
                        .ThenByDescending(m => ratingSummaries.TryGetValue(m.MovieId, out var s) ? s.TotalReviews : 0L)
                        .ThenByDescending(m => m.ReleaseYear)
                        .ThenBy(m => m.Title, StringComparer.Ordinal)
                        .ThenBy(m => m.MovieId)
                        .Select(m => ToCardViewModel(m, ratingSummaries))
                        .ToList();
                }
            }
            else
            {
                IOrderedQueryable<Movie> orderedQuery = normalizedSort switch
                {
                    "newest" => query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.MovieId),
                    "title" => query.OrderBy(m => m.Title).ThenByDescending(m => m.ReleaseYear).ThenBy(m => m.MovieId),
                    _ => query.OrderByDescending(m => m.ReleaseYear).ThenBy(m => m.Title).ThenBy(m => m.MovieId)
                };

                // shortcut: course-project catalog loads the full matched movie list without server-side pagination.
                var matchedMovies = await orderedQuery.ToListAsync(cancellationToken);
                if (normalizedGenre is not null)
                {
                    matchedMovies = matchedMovies
                        .Where(m => MovieHasGenre(m.Genre, normalizedGenre))
                        .ToList();
                }

                var ratingSummaries = await LoadRatingSummaryLookupAsync(
                    matchedMovies.Select(m => m.MovieId).ToArray(),
                    cancellationToken);
                cards = matchedMovies
                    .Select(m => ToCardViewModel(m, ratingSummaries))
                    .ToList();
            }

            return new MovieListViewModel
            {
                Movies = cards,
                Search = normalizedSearch,
                Genre = normalizedGenre,
                ReleaseYear = selectedYear,
                Sort = normalizedSort,
                AvailableGenres = availableGenres,
                AvailableReleaseYears = availableYears,
                TotalCatalogCount = metadata.Count,
                FilterWarning = filterWarning
            };
        }

        public async Task<ServiceResult<IReadOnlyList<MovieDto>>> GetMovieDtosAsync(
            string? search,
            string? genre,
            string? releaseYearRaw,
            CancellationToken cancellationToken = default)
        {
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            if (normalizedSearch is { Length: > 200 })
            {
                normalizedSearch = normalizedSearch[..200];
            }

            var normalizedGenre = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim();

            int? selectedYear = null;
            if (!string.IsNullOrWhiteSpace(releaseYearRaw))
            {
                if (int.TryParse(releaseYearRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedYear)
                    && parsedYear >= 1888
                    && parsedYear <= 9999)
                {
                    selectedYear = parsedYear;
                }
                else
                {
                    return ServiceResult<IReadOnlyList<MovieDto>>.Fail(
                        ServiceOutcome.ValidationError,
                        "Release year filter must be a valid integer of 1888 or later.",
                        "releaseYear");
                }
            }

            IQueryable<Movie> query = _db.Movies.AsNoTracking();

            if (normalizedSearch is not null)
            {
                var searchLower = normalizedSearch.ToLowerInvariant();
                query = query.Where(m => m.Title.ToLower().Contains(searchLower));
            }

            if (selectedYear.HasValue)
            {
                var yearValue = (ushort)selectedYear.Value;
                query = query.Where(m => m.ReleaseYear == yearValue);
            }

            var movies = await query
                .OrderByDescending(m => m.ReleaseYear)
                .ThenBy(m => m.Title)
                .ToListAsync(cancellationToken);
                if (normalizedGenre is not null)
                {
                    movies = movies
                        .Where(m => MovieHasGenre(m.Genre, normalizedGenre))
                        .ToList();
                }

            var ratingSummaries = await LoadRatingSummaryLookupAsync(
                movies.Select(m => m.MovieId).ToArray(),
                cancellationToken);
            var dtos = movies.Select(m => ToMovieDto(m, ratingSummaries)).ToList();

            return ServiceResult<IReadOnlyList<MovieDto>>.Success(dtos);
        }

        public async Task<MovieDto?> GetMovieDtoAsync(int movieId, CancellationToken cancellationToken = default)
        {
            var movie = await _db.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);

            if (movie is null)
            {
                return null;
            }

            var ratingSummaries = await LoadRatingSummaryLookupAsync([movieId], cancellationToken);
            return ToMovieDto(movie, ratingSummaries);
        }

        public async Task<MovieFormViewModel?> GetMovieFormAsync(int movieId, CancellationToken cancellationToken = default)
        {
            var movie = await _db.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);

            if (movie is null)
            {
                return null;
            }

            return new MovieFormViewModel
            {
                MovieId = movie.MovieId,
                Title = movie.Title,
                Genre = movie.Genre,
                ReleaseYear = movie.ReleaseYear,
                DurationMinutes = movie.DurationMinutes,
                Director = movie.Director,
                Synopsis = movie.Synopsis,
                PosterUrl = movie.PosterUrl
            };
        }

        public async Task<AdminMovieDeleteViewModel?> GetMovieDeleteInfoAsync(int movieId, CancellationToken cancellationToken = default)
        {
            var movie = await _db.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);

            if (movie is null)
            {
                return null;
            }

            var ratingSummaries = await LoadRatingSummaryLookupAsync([movieId], cancellationToken);
            var card = ToCardViewModel(movie, ratingSummaries);

            return new AdminMovieDeleteViewModel
            {
                Movie = card,
                DependentReviewCount = card.TotalReviews
            };
        }

        public async Task<ServiceResult<MovieDto>> CreateMovieAsync(
            MovieUpsertDto input,
            CancellationToken cancellationToken = default)
        {
            var validation = ValidateMovieInput(input);
            if (validation.Errors.Count > 0)
            {
                return ServiceResult<MovieDto>.Fail(
                    ServiceOutcome.ValidationError,
                    validation.FirstError!,
                    validation.FirstField,
                    validation.Errors);
            }

            var titleLower = validation.Title.ToLowerInvariant();
            var duplicateExists = await _db.Movies
                .AnyAsync(m => m.Title.ToLower() == titleLower && m.ReleaseYear == validation.ReleaseYear, cancellationToken);

            if (duplicateExists)
            {
                return ServiceResult<MovieDto>.Fail(
                    ServiceOutcome.Conflict,
                    "A movie with this title and release year already exists.",
                    nameof(MovieUpsertDto.Title));
            }

            var movie = new Movie
            {
                Title = validation.Title,
                Genre = validation.Genre,
                ReleaseYear = validation.ReleaseYear,
                DurationMinutes = validation.DurationMinutes,
                Director = validation.Director,
                Synopsis = validation.Synopsis,
                PosterUrl = validation.PosterUrl,
                CreatedAt = DateTime.UtcNow
            };

            _db.Movies.Add(movie);
            await _db.SaveChangesAsync(cancellationToken);

            var dto = ToMovieDto(movie, new Dictionary<int, (long, decimal)>());
            return ServiceResult<MovieDto>.Success(dto);
        }

        public async Task<ServiceResult<MovieDto>> UpdateMovieAsync(
            int movieId,
            MovieUpsertDto input,
            CancellationToken cancellationToken = default)
        {
            var movie = await _db.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
            if (movie is null)
            {
                return ServiceResult<MovieDto>.Fail(
                    ServiceOutcome.NotFound,
                    $"Movie with ID {movieId} was not found.");
            }

            var validation = ValidateMovieInput(input);
            if (validation.Errors.Count > 0)
            {
                return ServiceResult<MovieDto>.Fail(
                    ServiceOutcome.ValidationError,
                    validation.FirstError!,
                    validation.FirstField,
                    validation.Errors);
            }

            var titleLower = validation.Title.ToLowerInvariant();
            var duplicateExists = await _db.Movies
                .AnyAsync(
                    m => m.MovieId != movieId
                         && m.Title.ToLower() == titleLower
                         && m.ReleaseYear == validation.ReleaseYear,
                    cancellationToken);

            if (duplicateExists)
            {
                return ServiceResult<MovieDto>.Fail(
                    ServiceOutcome.Conflict,
                    "Another movie with this title and release year already exists.",
                    nameof(MovieUpsertDto.Title));
            }

            movie.Title = validation.Title;
            movie.Genre = validation.Genre;
            movie.ReleaseYear = validation.ReleaseYear;
            movie.DurationMinutes = validation.DurationMinutes;
            movie.Director = validation.Director;
            movie.Synopsis = validation.Synopsis;
            movie.PosterUrl = validation.PosterUrl;

            await _db.SaveChangesAsync(cancellationToken);

            var ratingSummaries = await LoadRatingSummaryLookupAsync([movieId], cancellationToken);
            var dto = ToMovieDto(movie, ratingSummaries);

            return ServiceResult<MovieDto>.Success(dto);
        }

        public async Task<ServiceResult> DeleteMovieAsync(
            int movieId,
            CancellationToken cancellationToken = default)
        {
            var movie = await _db.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
            if (movie is null)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.NotFound,
                    $"Movie with ID {movieId} was not found.");
            }

            var dependentReviewCount = await _db.RatingsReviews
                .CountAsync(r => r.MovieId == movieId, cancellationToken);

            if (dependentReviewCount > 0)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.Conflict,
                    $"Cannot delete \"{movie.Title}\" because it has {dependentReviewCount} dependent audience {(dependentReviewCount == 1 ? "review" : "reviews")}. Remove those reviews before deleting this movie.");
            }

            _db.Movies.Remove(movie);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return ServiceResult.Success();
            }
            catch (DbUpdateException)
            {
                return ServiceResult.Fail(
                    ServiceOutcome.Conflict,
                    $"Cannot delete \"{movie.Title}\" because dependent audience reviews still reference it.");
            }
        }

        public async Task<MovieDetailsViewModel?> GetMovieDetailsAsync(
            int movieId,
            int? currentUserId,
            CancellationToken cancellationToken = default)
        {
            var movie = await _db.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);

            if (movie is null)
            {
                return null;
            }

            (long TotalReviews, decimal AverageRating) summary = (0L, 0.00m);
            List<PublicReviewItemViewModel> reviews;

            if (_db.Database.IsRelational())
            {
                var viewSummary = await _db.MovieRatingSummaries
                    .AsNoTracking()
                    .Where(s => s.MovieId == movieId)
                    .Select(s => new { s.TotalReviews, s.AverageRating })
                    .FirstOrDefaultAsync(cancellationToken);

                if (viewSummary is not null)
                {
                    summary = (viewSummary.TotalReviews, viewSummary.AverageRating);
                }

                reviews = await _db.MovieReviewDetails
                    .AsNoTracking()
                    .Where(r => r.MovieId == movieId)
                    .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .ThenByDescending(r => r.ReviewId)
                    .Select(r => new PublicReviewItemViewModel
                    {
                        ReviewId = r.ReviewId,
                        UserId = r.UserId,
                        ReviewerName = r.ReviewerName,
                        Rating = r.Rating,
                        ReviewText = r.ReviewText,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsOwnedByCurrentUser = currentUserId.HasValue && r.UserId == currentUserId.Value
                    })
                    .ToListAsync(cancellationToken);
            }
            else
            {
                reviews = await _db.RatingsReviews
                    .AsNoTracking()
                    .Where(r => r.MovieId == movieId)
                    .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .ThenByDescending(r => r.ReviewId)
                    .Select(r => new PublicReviewItemViewModel
                    {
                        ReviewId = r.ReviewId,
                        UserId = r.UserId,
                        ReviewerName = r.User.FullName,
                        Rating = r.Rating,
                        ReviewText = r.ReviewText,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsOwnedByCurrentUser = currentUserId.HasValue && r.UserId == currentUserId.Value
                    })
                    .ToListAsync(cancellationToken);

                if (reviews.Count > 0)
                {
                    summary = (
                        reviews.Count,
                        Math.Round((decimal)reviews.Average(r => r.Rating), 2, MidpointRounding.AwayFromZero));
                }
            }

            var card = ToCardViewModel(
                movie,
                new Dictionary<int, (long TotalReviews, decimal AverageRating)>
                {
                    [movie.MovieId] = summary
                });

            var currentUserReview = currentUserId.HasValue
                ? reviews.FirstOrDefault(r => r.UserId == currentUserId.Value)
                : null;

            var topRatedSidebarMovies = await LoadTopRatedCardsAsync(6, excludeMovieId: movieId, cancellationToken);

            return new MovieDetailsViewModel
            {
                Movie = card,
                Reviews = reviews,
                TopRatedSidebarMovies = topRatedSidebarMovies,
                CurrentUserReview = currentUserReview
            };
        }

        private static MovieCardViewModel ToCardViewModel(
            Movie movie,
            IReadOnlyDictionary<int, (long TotalReviews, decimal AverageRating)> ratingSummaries)
        {
            ratingSummaries.TryGetValue(movie.MovieId, out var summary);
            return new MovieCardViewModel
            {
                MovieId = movie.MovieId,
                Title = movie.Title,
                Genre = movie.Genre,
                ReleaseYear = movie.ReleaseYear,
                DurationMinutes = movie.DurationMinutes,
                Director = movie.Director,
                Synopsis = movie.Synopsis,
                PosterUrl = MovieCardViewModel.SanitizePosterUrl(movie.PosterUrl),
                TotalReviews = summary.TotalReviews,
                AverageRating = summary.AverageRating
            };
        }

        private static IEnumerable<string> SplitGenres(string? genreValue)
        {
            if (string.IsNullOrWhiteSpace(genreValue))
            {
                return Array.Empty<string>();
            }

            return genreValue
                .Split(
                    new[] { ',', '/', '&', ';' },
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Where(genre => !string.IsNullOrWhiteSpace(genre));
        }

        private static bool MovieHasGenre(
            string? movieGenre,
            string selectedGenre)
        {
            if (string.IsNullOrWhiteSpace(movieGenre) ||
                string.IsNullOrWhiteSpace(selectedGenre))
            {
                return false;
            }

            var targetGenre = selectedGenre.Trim();

            return SplitGenres(movieGenre).Any(genre =>
                string.Equals(
                    genre,
                    targetGenre,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static string? NormalizeCatalogSort(string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return null;
            }

            return sort.Trim().ToLowerInvariant() switch
            {
                "top-rated" => "top-rated",
                "newest" => "newest",
                "title" => "title",
                "release-year" => "release-year",
                _ => null
            };
        }

        internal async Task<IReadOnlyList<MovieCardViewModel>> LoadTopRatedCardsAsync(
            int limit,
            int? excludeMovieId = null,
            CancellationToken cancellationToken = default)
        {
            if (limit <= 0)
            {
                return Array.Empty<MovieCardViewModel>();
            }

            if (_db.Database.IsRelational())
            {
                var query =
                    from s in _db.MovieRatingSummaries.AsNoTracking()
                    join m in _db.Movies.AsNoTracking() on s.MovieId equals m.MovieId
                    where s.TotalReviews > 0
                    select new { Movie = m, s.TotalReviews, s.AverageRating };

                if (excludeMovieId.HasValue)
                {
                    var excludedId = excludeMovieId.Value;
                    query = query.Where(x => x.Movie.MovieId != excludedId);
                }

                var rows = await query
                    .OrderByDescending(x => x.AverageRating)
                    .ThenByDescending(x => x.TotalReviews)
                    .ThenByDescending(x => x.Movie.ReleaseYear)
                    .ThenBy(x => x.Movie.MovieId)
                    .Take(limit)
                    .ToListAsync(cancellationToken);

                return rows
                    .Select(r => ToCardViewModel(
                        r.Movie,
                        new Dictionary<int, (long TotalReviews, decimal AverageRating)>
                        {
                            [r.Movie.MovieId] = (r.TotalReviews, r.AverageRating)
                        }))
                    .ToList();
            }

            var ratingsQuery = _db.RatingsReviews.AsNoTracking();
            if (excludeMovieId.HasValue)
            {
                var excludedId = excludeMovieId.Value;
                ratingsQuery = ratingsQuery.Where(r => r.MovieId != excludedId);
            }

            var groupedRatings = await ratingsQuery
                .GroupBy(r => r.MovieId)
                .Select(g => new
                {
                    MovieId = g.Key,
                    TotalReviews = (long)g.Count(),
                    AverageRating = Math.Round((decimal)g.Average(r => r.Rating), 2, MidpointRounding.AwayFromZero)
                })
                .ToListAsync(cancellationToken);

            if (groupedRatings.Count == 0)
            {
                return Array.Empty<MovieCardViewModel>();
            }

            var ratedIds = groupedRatings.Select(g => g.MovieId).ToArray();
            var ratedMoviesById = await _db.Movies
                .AsNoTracking()
                .Where(m => ratedIds.Contains(m.MovieId))
                .ToDictionaryAsync(m => m.MovieId, cancellationToken);

            var summaryLookup = groupedRatings.ToDictionary(
                g => g.MovieId,
                g => (g.TotalReviews, g.AverageRating));

            return groupedRatings
                .Where(g => ratedMoviesById.ContainsKey(g.MovieId))
                .OrderByDescending(g => g.AverageRating)
                .ThenByDescending(g => g.TotalReviews)
                .ThenByDescending(g => ratedMoviesById[g.MovieId].ReleaseYear)
                .ThenBy(g => g.MovieId)
                .Take(limit)
                .Select(g => ToCardViewModel(ratedMoviesById[g.MovieId], summaryLookup))
                .ToList();
        }

        private Task<Dictionary<int, (long TotalReviews, decimal AverageRating)>> LoadRatingSummaryLookupAsync(
            IReadOnlyCollection<int> movieIds,
            CancellationToken cancellationToken)
        {
            return LoadRatingSummariesAsync(_db, movieIds, cancellationToken);
        }

        internal static async Task<Dictionary<int, (long TotalReviews, decimal AverageRating)>> LoadRatingSummariesAsync(
            ApplicationDbContext db,
            IReadOnlyCollection<int> movieIds,
            CancellationToken cancellationToken)
        {
            if (movieIds.Count == 0)
            {
                return new Dictionary<int, (long TotalReviews, decimal AverageRating)>();
            }

            var distinctIds = movieIds as int[] ?? movieIds.Distinct().ToArray();
            if (db.Database.IsRelational())
            {
                var rows = await db.MovieRatingSummaries
                    .AsNoTracking()
                    .Where(s => distinctIds.Contains(s.MovieId))
                    .Select(s => new { s.MovieId, s.TotalReviews, s.AverageRating })
                    .ToListAsync(cancellationToken);

                return rows.ToDictionary(
                    r => r.MovieId,
                    r => (r.TotalReviews, r.AverageRating));
            }

            var aggregated = await db.RatingsReviews
                .AsNoTracking()
                .Where(r => distinctIds.Contains(r.MovieId))
                .GroupBy(r => r.MovieId)
                .Select(g => new
                {
                    MovieId = g.Key,
                    TotalReviews = (long)g.Count(),
                    AverageRating = Math.Round((decimal)g.Average(r => r.Rating), 2, MidpointRounding.AwayFromZero)
                })
                .ToListAsync(cancellationToken);

            return aggregated.ToDictionary(
                r => r.MovieId,
                r => (r.TotalReviews, r.AverageRating));
        }

        private static MovieDto ToMovieDto(
            Movie movie,
            IReadOnlyDictionary<int, (long TotalReviews, decimal AverageRating)> ratingSummaries)
        {
            ratingSummaries.TryGetValue(movie.MovieId, out var summary);
            return new MovieDto(
                movie.MovieId,
                movie.Title,
                movie.Genre,
                movie.ReleaseYear,
                movie.DurationMinutes,
                movie.Director,
                movie.Synopsis,
                MovieCardViewModel.SanitizePosterUrl(movie.PosterUrl),
                summary.TotalReviews,
                summary.AverageRating,
                movie.CreatedAt);
        }

        private static ValidatedMovieInput ValidateMovieInput(MovieUpsertDto input)
        {
            var errors = new Dictionary<string, List<string>>();
            static void AddError(Dictionary<string, List<string>> map, string field, string message)
            {
                if (!map.TryGetValue(field, out var list))
                {
                    list = new List<string>();
                    map[field] = list;
                }
                list.Add(message);
            }

            var title = (input.Title ?? string.Empty).Trim();
            if (title.Length == 0 || title.Length > 200)
            {
                AddError(errors, nameof(MovieUpsertDto.Title), "Title must be between 1 and 200 characters.");
            }

            var genre = (input.Genre ?? string.Empty).Trim();
            if (genre.Length == 0 || genre.Length > 80)
            {
                AddError(errors, nameof(MovieUpsertDto.Genre), "Genre must be between 1 and 80 characters.");
            }

            var maxReleaseYear = DateTime.UtcNow.Year + 5;
            ushort releaseYear = 1888;
            if (!input.ReleaseYear.HasValue || input.ReleaseYear.Value < 1888 || input.ReleaseYear.Value > maxReleaseYear)
            {
                AddError(errors, nameof(MovieUpsertDto.ReleaseYear), $"Release year must be between 1888 and {maxReleaseYear}.");
            }
            else
            {
                releaseYear = (ushort)input.ReleaseYear.Value;
            }

            ushort durationMinutes = 1;
            if (!input.DurationMinutes.HasValue || input.DurationMinutes.Value <= 0 || input.DurationMinutes.Value > 1440)
            {
                AddError(errors, nameof(MovieUpsertDto.DurationMinutes), "Duration must be between 1 and 1,440 minutes.");
            }
            else
            {
                durationMinutes = (ushort)input.DurationMinutes.Value;
            }

            var director = (input.Director ?? string.Empty).Trim();
            if (director.Length == 0 || director.Length > 150)
            {
                AddError(errors, nameof(MovieUpsertDto.Director), "Director must be between 1 and 150 characters.");
            }

            var synopsis = (input.Synopsis ?? string.Empty).Trim();
            if (synopsis.Length == 0 || synopsis.Length > 10000)
            {
                AddError(errors, nameof(MovieUpsertDto.Synopsis), "Synopsis is required and cannot exceed 10,000 characters.");
            }

            string? posterUrl = null;
            if (!string.IsNullOrWhiteSpace(input.PosterUrl))
            {
                var trimmedPoster = input.PosterUrl.Trim();
                if (trimmedPoster.Length > 2048)
                {
                    AddError(errors, nameof(MovieUpsertDto.PosterUrl), "Poster URL cannot exceed 2048 characters.");
                }
                else
                {
                    posterUrl = MovieCardViewModel.SanitizePosterUrl(trimmedPoster);
                    if (posterUrl is null)
                    {
                        AddError(
                            errors,
                            nameof(MovieUpsertDto.PosterUrl),
                            "Poster URL must be a valid root-relative path (/images/...) or an http/https URL.");
                    }
                }
            }

            var readonlyErrors = errors.ToDictionary(k => k.Key, k => k.Value.ToArray());
            var firstField = errors.Keys.FirstOrDefault();
            var firstError = firstField is null ? null : errors[firstField][0];

            return new ValidatedMovieInput(
                title,
                genre,
                releaseYear,
                durationMinutes,
                director,
                synopsis,
                posterUrl,
                readonlyErrors,
                firstField,
                firstError);
        }

        private sealed record ValidatedMovieInput(
            string Title,
            string Genre,
            ushort ReleaseYear,
            ushort DurationMinutes,
            string Director,
            string Synopsis,
            string? PosterUrl,
            IReadOnlyDictionary<string, string[]> Errors,
            string? FirstField,
            string? FirstError);
    }
}
