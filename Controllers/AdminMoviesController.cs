using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Services;
using PureAvocadoes.ViewModels;
using System.Data.Common;

namespace PureAvocadoes.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Movies")]
    public class AdminMoviesController : Controller
    {
        private readonly MovieService _movieService;
        private readonly ILogger<AdminMoviesController> _logger;

        public AdminMoviesController(MovieService movieService, ILogger<AdminMoviesController> logger)
        {
            _movieService = movieService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
        {
            try
            {
                var catalog = await _movieService.GetCatalogAsync(search, null, null, cancellationToken);
                return View(new AdminMoviesIndexViewModel
                {
                    Movies = catalog.Movies,
                    Search = catalog.Search
                });
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load admin movies list ({ExceptionType}).", ex.GetType().Name);
                return View(new AdminMoviesIndexViewModel
                {
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to connect to the database right now. Please try again shortly."
                });
            }
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View(new MovieFormViewModel());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MovieFormViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _movieService.CreateMovieAsync(model.ToUpsertDto(), cancellationToken);
            if (!result.Succeeded || result.Data is null)
            {
                ModelState.AddServiceErrors(result, "Unable to save movie.");
                return View(model);
            }

            TempData["StatusMessage"] = $"Added \"{result.Data.Title}\" to the movie catalog.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int?}")]
        public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            var model = await _movieService.GetMovieFormAsync(id.Value, cancellationToken);
            if (model is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            return View(model);
        }

        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MovieFormViewModel model, CancellationToken cancellationToken)
        {
            model.MovieId = id;
            if (id <= 0)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _movieService.UpdateMovieAsync(id, model.ToUpsertDto(), cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            if (!result.Succeeded || result.Data is null)
            {
                ModelState.AddServiceErrors(result, "Unable to save movie.");
                return View(model);
            }

            TempData["StatusMessage"] = $"Updated \"{result.Data.Title}\".";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Delete/{id:int?}")]
        public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            var model = await _movieService.GetMovieDeleteInfoAsync(id.Value, cancellationToken);
            if (model is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            return View(model);
        }

        [HttpPost("Delete/{id:int}")]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            var result = await _movieService.DeleteMovieAsync(id, cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("~/Views/Movies/NotFound.cshtml");
            }

            if (result.Outcome == ServiceOutcome.Conflict)
            {
                var deleteInfo = await _movieService.GetMovieDeleteInfoAsync(id, cancellationToken);
                if (deleteInfo is null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                    return View("~/Views/Movies/NotFound.cshtml");
                }

                ViewData["ConflictError"] = result.ErrorMessage
                    ?? "Cannot delete this movie because dependent audience reviews exist.";
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View("Delete", deleteInfo);
            }

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Unable to delete movie.";
                return RedirectToAction(nameof(Index));
            }

            TempData["StatusMessage"] = "Movie deleted from catalog.";
            return RedirectToAction(nameof(Index));
        }
    }
}
