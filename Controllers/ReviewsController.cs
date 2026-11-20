using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Services;
using PureAvocadoes.ViewModels;
using System.Data.Common;

namespace PureAvocadoes.Controllers
{
    [Authorize]
    [Route("Reviews")]
    public class ReviewsController : Controller
    {
        private readonly ReviewService _reviewService;
        private readonly MovieService _movieService;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController(
            ReviewService reviewService,
            MovieService movieService,
            ILogger<ReviewsController> logger)
        {
            _reviewService = reviewService;
            _movieService = movieService;
            _logger = logger;
        }

        [HttpGet("Mine")]
        public async Task<IActionResult> Mine(CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Challenge();
            }

            try
            {
                var model = await _reviewService.GetMyReviewsViewModelAsync(userId, cancellationToken);
                return View(model);
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load user reviews for UserId {UserId} ({ExceptionType}).", userId, ex.GetType().Name);
                return View(new MyReviewsViewModel
                {
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to load your reviews from the database right now. Please try again shortly."
                });
            }
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewFormViewModel model, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Challenge();
            }

            if (model.MovieId <= 0)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            if (!ModelState.IsValid)
            {
                return await RenderMovieDetailsWithValidationAsync(model, userId, cancellationToken);
            }

            var result = await _reviewService.CreateReviewAsync(userId, model.ToCreateDto(), cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            if (result.Outcome == ServiceOutcome.Conflict)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "You have already reviewed this movie.";
                return RedirectToAction("Details", "Movies", new { id = model.MovieId });
            }

            if (!result.Succeeded || result.Data is null)
            {
                ModelState.AddServiceErrors(result);
                return await RenderMovieDetailsWithValidationAsync(model, userId, cancellationToken);
            }

            TempData["StatusMessage"] = $"Published your review for \"{result.Data.MovieTitle}\".";
            return RedirectToAction("Details", "Movies", new { id = model.MovieId });
        }

        [HttpGet("Edit/{id:int?}")]
        public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Challenge();
            }

            if (!id.HasValue || id.Value <= 0)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            var result = await _reviewService.GetReviewFormForEditAsync(id.Value, userId, cancellationToken);
            return result.Outcome switch
            {
                ServiceOutcome.Success when result.Data is not null => View(result.Data),
                ServiceOutcome.Forbidden => Forbid(),
                _ => NotFoundView()
            };
        }

        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ReviewFormViewModel model, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return NotFoundView();
            }

            model.ReviewId = id;

            if (!ModelState.IsValid)
            {
                var ownershipCheck = await _reviewService.GetReviewFormForEditAsync(id, userId, cancellationToken);
                if (ownershipCheck.Outcome == ServiceOutcome.NotFound)
                {
                    return NotFoundView();
                }

                if (ownershipCheck.Outcome == ServiceOutcome.Forbidden)
                {
                    return Forbid();
                }

                if (ownershipCheck.Data is not null)
                {
                    model.MovieId = ownershipCheck.Data.MovieId;
                    model.MovieTitle = ownershipCheck.Data.MovieTitle;
                }

                model.ReviewText ??= string.Empty;
                return View(model);
            }

            var result = await _reviewService.UpdateReviewAsync(id, userId, model.ToUpdateDto(), cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                return NotFoundView();
            }

            if (result.Outcome == ServiceOutcome.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded || result.Data is null)
            {
                ModelState.AddServiceErrors(result);
                var existing = await _reviewService.GetReviewFormForEditAsync(id, userId, cancellationToken);
                if (existing.Data is not null)
                {
                    model.MovieId = existing.Data.MovieId;
                    model.MovieTitle = existing.Data.MovieTitle;
                }

                return View(model);
            }

            TempData["StatusMessage"] = $"Updated your review for \"{result.Data.MovieTitle}\".";
            return RedirectToAction(nameof(Mine));
        }

        [HttpGet("Delete/{id:int?}")]
        public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Challenge();
            }

            if (!id.HasValue || id.Value <= 0)
            {
                return NotFoundView();
            }

            var result = await _reviewService.GetReviewDeleteViewModelAsync(id.Value, userId, cancellationToken);
            return result.Outcome switch
            {
                ServiceOutcome.Success when result.Data is not null => View(result.Data),
                ServiceOutcome.Forbidden => Forbid(),
                _ => NotFoundView()
            };
        }

        [HttpPost("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return NotFoundView();
            }

            var result = await _reviewService.DeleteReviewAsync(id, userId, cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                return NotFoundView();
            }

            if (result.Outcome == ServiceOutcome.Forbidden)
            {
                return Forbid();
            }

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Unable to delete review.";
                return RedirectToAction(nameof(Mine));
            }

            TempData["StatusMessage"] = "Your review has been deleted.";
            return RedirectToAction(nameof(Mine));
        }

        private async Task<IActionResult> RenderMovieDetailsWithValidationAsync(
            ReviewFormViewModel model,
            int userId,
            CancellationToken cancellationToken)
        {
            var details = await _movieService.GetMovieDetailsAsync(model.MovieId, userId, cancellationToken);
            if (details is null)
            {
                return NotFoundView();
            }

            ViewData["ReviewRatingDraft"] = model.Rating;
            ViewData["ReviewTextDraft"] = model.ReviewText ?? string.Empty;
            return View("~/Views/Movies/Details.cshtml", details);
        }

        private IActionResult NotFoundView()
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("~/Views/Movies/NotFound.cshtml");
        }
    }
}
