using Microsoft.EntityFrameworkCore;
using PureAvocadoes.Data;
using PureAvocadoes.Dtos;
using PureAvocadoes.Models;
using PureAvocadoes.ViewModels;
using System.Linq.Expressions;

namespace PureAvocadoes.Services
{
    /// Shared review business logic used by both ReviewsController (MVC)
    /// and ReviewsApiController (REST JSON).
    public class ReviewService
    {
        private const int MaxReviewTextLength = 2000;
        private readonly ApplicationDbContext _db;

        public ReviewService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ServiceResult<IReadOnlyList<ReviewDto>>> GetMovieReviewsAsync(
            int movieId,
            CancellationToken cancellationToken = default)
        {
            if (movieId <= 0)
            {
                return ServiceResult<IReadOnlyList<ReviewDto>>.Fail(
                    ServiceOutcome.NotFound,
                    $"Movie with ID {movieId} was not found.");
            }

            var movieExists = await _db.Movies
                .AsNoTracking()
                .AnyAsync(m => m.MovieId == movieId, cancellationToken);

            if (!movieExists)
            {
                return ServiceResult<IReadOnlyList<ReviewDto>>.Fail(
                    ServiceOutcome.NotFound,
                    $"Movie with ID {movieId} was not found.");
            }

            var reviews = await QueryReviewDtosAsync(
                r => r.MovieId == movieId,
                r => r.MovieId == movieId,
                cancellationToken);

            return ServiceResult<IReadOnlyList<ReviewDto>>.Success(reviews);
        }

        public async Task<IReadOnlyList<ReviewDto>> GetUserReviewDtosAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return Array.Empty<ReviewDto>();
            }

            return await QueryReviewDtosAsync(
                r => r.UserId == userId,
                r => r.UserId == userId,
                cancellationToken);
        }

        private Task<List<ReviewDto>> QueryReviewDtosAsync(
            Expression<Func<MovieReviewDetailView, bool>> relationalPredicate,
            Expression<Func<RatingReview, bool>> inMemoryPredicate,
            CancellationToken cancellationToken)
        {
            if (_db.Database.IsRelational())
            {
                return _db.MovieReviewDetails
                    .AsNoTracking()
                    .Where(relationalPredicate)
                    .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                    .ThenByDescending(r => r.ReviewId)
                    .Select(r => new ReviewDto(
                        r.ReviewId,
                        r.MovieId,
                        r.MovieTitle,
                        r.UserId,
                        r.ReviewerName,
                        r.Rating,
                        r.ReviewText,
                        r.CreatedAt,
                        r.UpdatedAt))
                    .ToListAsync(cancellationToken);
            }

            return _db.RatingsReviews
                .AsNoTracking()
                .Where(inMemoryPredicate)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ThenByDescending(r => r.ReviewId)
                .Select(r => new ReviewDto(
                    r.ReviewId,
                    r.MovieId,
                    r.Movie.Title,
                    r.UserId,
                    r.User.FullName,
                    r.Rating,
                    r.ReviewText,
                    r.CreatedAt,
                    r.UpdatedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task<MyReviewsViewModel> GetMyReviewsViewModelAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return new MyReviewsViewModel();
            }

            var items = await _db.RatingsReviews
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ThenByDescending(r => r.ReviewId)
                .Select(r => new MyReviewItemViewModel
                {
                    ReviewId = r.ReviewId,
                    Rating = r.Rating,
                    ReviewText = r.ReviewText,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    Movie = new MovieCardViewModel
                    {
                        MovieId = r.MovieId,
                        Title = r.Movie.Title,
                        Genre = r.Movie.Genre,
                        ReleaseYear = r.Movie.ReleaseYear,
                        PosterUrl = r.Movie.PosterUrl
                    }
                })
                .ToListAsync(cancellationToken);

            return new MyReviewsViewModel
            {
                Reviews = items
            };
        }

        public async Task<ServiceResult<ReviewFormViewModel>> GetReviewFormForEditAsync(
            int reviewId,
            int currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (reviewId <= 0)
            {
                return ServiceResult<ReviewFormViewModel>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            var row = await _db.RatingsReviews
                .AsNoTracking()
                .Where(r => r.ReviewId == reviewId)
                .Select(r => new
                {
                    r.ReviewId,
                    r.UserId,
                    r.MovieId,
                    MovieTitle = r.Movie.Title,
                    r.Rating,
                    r.ReviewText
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return ServiceResult<ReviewFormViewModel>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            if (row.UserId != currentUserId)
            {
                return ServiceResult<ReviewFormViewModel>.Fail(
                    ServiceOutcome.Forbidden,
                    "You do not have permission to edit another user's review.");
            }

            return ServiceResult<ReviewFormViewModel>.Success(new ReviewFormViewModel
            {
                ReviewId = row.ReviewId,
                MovieId = row.MovieId,
                MovieTitle = row.MovieTitle,
                Rating = row.Rating,
                ReviewText = row.ReviewText
            });
        }

        public async Task<ServiceResult<ReviewDeleteViewModel>> GetReviewDeleteViewModelAsync(
            int reviewId,
            int currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (reviewId <= 0)
            {
                return ServiceResult<ReviewDeleteViewModel>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            var row = await _db.RatingsReviews
                .AsNoTracking()
                .Where(r => r.ReviewId == reviewId)
                .Select(r => new
                {
                    r.ReviewId,
                    r.UserId,
                    r.MovieId,
                    MovieTitle = r.Movie.Title,
                    r.Rating,
                    r.ReviewText,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return ServiceResult<ReviewDeleteViewModel>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            if (row.UserId != currentUserId)
            {
                return ServiceResult<ReviewDeleteViewModel>.Fail(
                    ServiceOutcome.Forbidden,
                    "You do not have permission to delete another user's review.");
            }

            return ServiceResult<ReviewDeleteViewModel>.Success(new ReviewDeleteViewModel
            {
                ReviewId = row.ReviewId,
                MovieId = row.MovieId,
                MovieTitle = row.MovieTitle,
                Rating = row.Rating,
                ReviewText = row.ReviewText,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt
            });
        }

        public async Task<ServiceResult<ReviewDto>> CreateReviewAsync(
            int authenticatedUserId,
            ReviewCreateDto input,
            CancellationToken cancellationToken = default)
        {
            if (authenticatedUserId <= 0)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.Forbidden,
                    "Authenticated user identity is required.");
            }

            var errors = ValidateReviewContent(input.Rating, input.ReviewText);
            if (!input.MovieId.HasValue)
            {
                errors[nameof(ReviewCreateDto.MovieId)] = ["Movie selection is required."];
            }

            if (errors.Count > 0)
            {
                var first = errors.First();
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.ValidationError,
                    first.Value[0],
                    first.Key,
                    errors);
            }

            var movieId = input.MovieId!.Value;
            if (movieId <= 0)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.NotFound,
                    $"Movie with ID {movieId} was not found.");
            }

            var movie = await _db.Movies
                .AsNoTracking()
                .Where(m => m.MovieId == movieId)
                .Select(m => new { m.MovieId, m.Title })
                .FirstOrDefaultAsync(cancellationToken);

            if (movie is null)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.NotFound,
                    $"Movie with ID {movieId} was not found.");
            }

            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == authenticatedUserId)
                .Select(u => new { u.UserId, u.FullName })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.Forbidden,
                    "Authenticated user account could not be verified.");
            }

            var duplicateExists = await _db.RatingsReviews
                .AsNoTracking()
                .AnyAsync(r => r.UserId == authenticatedUserId && r.MovieId == movieId, cancellationToken);

            if (duplicateExists)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.Conflict,
                    $"You have already reviewed \"{movie.Title}\". Edit or delete your existing review instead.");
            }

            var review = new RatingReview
            {
                UserId = authenticatedUserId,
                MovieId = movieId,
                Rating = (byte)input.Rating!.Value,
                ReviewText = input.ReviewText.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            _db.RatingsReviews.Add(review);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.Conflict,
                    $"You have already reviewed \"{movie.Title}\". Edit or delete your existing review instead.");
            }

            return ServiceResult<ReviewDto>.Success(new ReviewDto(
                review.ReviewId,
                review.MovieId,
                movie.Title,
                review.UserId,
                user.FullName,
                review.Rating,
                review.ReviewText,
                review.CreatedAt,
                review.UpdatedAt));
        }

        public async Task<ServiceResult<ReviewDto>> UpdateReviewAsync(
            int reviewId,
            int authenticatedUserId,
            ReviewUpdateDto input,
            CancellationToken cancellationToken = default)
        {
            if (reviewId <= 0)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            var review = await _db.RatingsReviews
                .Include(r => r.Movie)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ReviewId == reviewId, cancellationToken);

            if (review is null)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            if (review.UserId != authenticatedUserId)
            {
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.Forbidden,
                    "You do not have permission to edit another user's review.");
            }

            var errors = ValidateReviewContent(input.Rating, input.ReviewText);
            if (errors.Count > 0)
            {
                var first = errors.First();
                return ServiceResult<ReviewDto>.Fail(
                    ServiceOutcome.ValidationError,
                    first.Value[0],
                    first.Key,
                    errors);
            }

            review.Rating = (byte)input.Rating!.Value;
            review.ReviewText = input.ReviewText.Trim();
            review.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return ServiceResult<ReviewDto>.Success(new ReviewDto(
                review.ReviewId,
                review.MovieId,
                review.Movie.Title,
                review.UserId,
                review.User.FullName,
                review.Rating,
                review.ReviewText,
                review.CreatedAt,
                review.UpdatedAt));
        }

        public async Task<ServiceResult<int>> DeleteReviewAsync(
            int reviewId,
            int authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            if (reviewId <= 0)
            {
                return ServiceResult<int>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            var review = await _db.RatingsReviews
                .FirstOrDefaultAsync(r => r.ReviewId == reviewId, cancellationToken);

            if (review is null)
            {
                return ServiceResult<int>.Fail(
                    ServiceOutcome.NotFound,
                    $"Review with ID {reviewId} was not found.");
            }

            if (review.UserId != authenticatedUserId)
            {
                return ServiceResult<int>.Fail(
                    ServiceOutcome.Forbidden,
                    "You do not have permission to delete another user's review.");
            }

            var movieId = review.MovieId;
            _db.RatingsReviews.Remove(review);
            await _db.SaveChangesAsync(cancellationToken);

            return ServiceResult<int>.Success(movieId);
        }

        private static Dictionary<string, string[]> ValidateReviewContent(int? rating, string? reviewText)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!rating.HasValue || rating.Value < 1 || rating.Value > 5)
            {
                errors[nameof(ReviewCreateDto.Rating)] = ["Rating must be between 1 and 5."];
            }

            var trimmedText = reviewText?.Trim() ?? string.Empty;
            if (trimmedText.Length == 0)
            {
                errors[nameof(ReviewCreateDto.ReviewText)] = ["Review text is required."];
            }
            else if (trimmedText.Length > MaxReviewTextLength)
            {
                errors[nameof(ReviewCreateDto.ReviewText)] = ["Review text cannot exceed 2,000 characters."];
            }

            return errors;
        }
    }
}
