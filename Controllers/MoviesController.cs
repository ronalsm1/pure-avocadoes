using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Services;
using PureAvocadoes.ViewModels;
using System.Data.Common;

namespace PureAvocadoes.Controllers
{
    public class MoviesController : Controller
    {
        private readonly MovieService _movieService;
        private readonly ILogger<MoviesController> _logger;

        public MoviesController(MovieService movieService, ILogger<MoviesController> logger)
        {
            _movieService = movieService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? genre,
            string? releaseYear,
            string? sort,
            CancellationToken cancellationToken)
        {
            try
            {
                var model = await _movieService.GetCatalogAsync(search, genre, releaseYear, sort, cancellationToken);
                return View(model);
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load movie catalog due to database error ({ExceptionType}).", ex.GetType().Name);
                return View(new MovieListViewModel
                {
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    Genre = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim(),
                    Sort = string.IsNullOrWhiteSpace(sort) ? null : sort.Trim().ToLowerInvariant(),
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to connect to the movie catalog right now. Please try again shortly."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int? id, CancellationToken cancellationToken)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("NotFound");
            }

            int? currentUserId = User.TryGetUserId(out var parsedUserId) ? parsedUserId : null;

            try
            {
                var model = await _movieService.GetMovieDetailsAsync(id.Value, currentUserId, cancellationToken);
                if (model is null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                    return View("NotFound");
                }

                return View(model);
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load movie details for MovieId {MovieId} ({ExceptionType}).", id.Value, ex.GetType().Name);
                return View(new MovieDetailsViewModel
                {
                    Movie = new MovieCardViewModel { MovieId = id.Value, Title = "Movie Details" },
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to load movie details from the database right now. Please try again shortly."
                });
            }
        }
    }
}
