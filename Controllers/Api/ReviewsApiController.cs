using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Dtos;
using PureAvocadoes.Services;

namespace PureAvocadoes.Controllers.Api
{
    [ApiController]
    [Route("api/reviews")]
    public class ReviewsApiController : ControllerBase
    {
        private readonly ReviewService _reviewService;

        public ReviewsApiController(ReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpGet("movie/{movieId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByMovie(int movieId, CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetMovieReviewsAsync(movieId, cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                return NotFound(new ApiErrorDto(result.ErrorMessage ?? $"Movie with ID {movieId} was not found."));
            }

            return Ok(result.Data);
        }

        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized(new ApiErrorDto("Authentication is required."));
            }

            var reviews = await _reviewService.GetUserReviewDtosAsync(userId, cancellationToken);
            return Ok(reviews);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromBody] ReviewCreateDto input,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized(new ApiErrorDto("Authentication is required."));
            }

            var result = await _reviewService.CreateReviewAsync(userId, input, cancellationToken);
            return result.Outcome switch
            {
                ServiceOutcome.Success when result.Data is not null =>
                    Created($"/api/reviews/movie/{result.Data.MovieId}", result.Data),
                ServiceOutcome.NotFound =>
                    NotFound(new ApiErrorDto(result.ErrorMessage ?? "Movie was not found.")),
                ServiceOutcome.Conflict =>
                    Conflict(new ApiErrorDto(result.ErrorMessage ?? "You have already reviewed this movie.")),
                ServiceOutcome.Forbidden =>
                    StatusCode(StatusCodes.Status403Forbidden, new ApiErrorDto(result.ErrorMessage ?? "Access denied.")),
                _ =>
                    BadRequest(new ApiErrorDto(
                        result.ErrorMessage ?? "Invalid review input.",
                        result.ErrorField,
                        result.Errors.Count > 0 ? result.Errors : null))
            };
        }

        [HttpPut("{id:int}")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] ReviewUpdateDto input,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized(new ApiErrorDto("Authentication is required."));
            }

            var result = await _reviewService.UpdateReviewAsync(id, userId, input, cancellationToken);
            return result.Outcome switch
            {
                ServiceOutcome.Success when result.Data is not null =>
                    Ok(result.Data),
                ServiceOutcome.NotFound =>
                    NotFound(new ApiErrorDto(result.ErrorMessage ?? $"Review with ID {id} was not found.")),
                ServiceOutcome.Forbidden =>
                    StatusCode(StatusCodes.Status403Forbidden, new ApiErrorDto(result.ErrorMessage ?? "You do not have permission to edit this review.")),
                _ =>
                    BadRequest(new ApiErrorDto(
                        result.ErrorMessage ?? "Invalid review input.",
                        result.ErrorField,
                        result.Errors.Count > 0 ? result.Errors : null))
            };
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized(new ApiErrorDto("Authentication is required."));
            }

            var result = await _reviewService.DeleteReviewAsync(id, userId, cancellationToken);
            return result.Outcome switch
            {
                ServiceOutcome.Success => NoContent(),
                ServiceOutcome.NotFound =>
                    NotFound(new ApiErrorDto(result.ErrorMessage ?? $"Review with ID {id} was not found.")),
                ServiceOutcome.Forbidden =>
                    StatusCode(StatusCodes.Status403Forbidden, new ApiErrorDto(result.ErrorMessage ?? "You do not have permission to delete this review.")),
                _ => BadRequest(new ApiErrorDto(result.ErrorMessage ?? "Unable to delete review."))
            };
        }
    }
}
