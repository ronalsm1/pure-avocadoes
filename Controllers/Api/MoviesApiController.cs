using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Dtos;
using PureAvocadoes.Services;

namespace PureAvocadoes.Controllers.Api
{
    [ApiController]
    [Route("api/movies")]
    public class MoviesApiController : ControllerBase
    {
        private readonly MovieService _movieService;

        public MoviesApiController(MovieService movieService)
        {
            _movieService = movieService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? genre,
            [FromQuery] string? releaseYear,
            CancellationToken cancellationToken)
        {
            var result = await _movieService.GetMovieDtosAsync(search, genre, releaseYear, cancellationToken);
            if (!result.Succeeded)
            {
                return BadRequest(new ApiErrorDto(result.ErrorMessage ?? "Invalid query parameters.", result.ErrorField));
            }

            return Ok(result.Data);
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return NotFound(new ApiErrorDto($"Movie with ID {id} was not found."));
            }

            var movie = await _movieService.GetMovieDtoAsync(id, cancellationToken);
            if (movie is null)
            {
                return NotFound(new ApiErrorDto($"Movie with ID {id} was not found."));
            }

            return Ok(movie);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromBody] MovieUpsertDto input,
            CancellationToken cancellationToken)
        {
            var result = await _movieService.CreateMovieAsync(input, cancellationToken);
            return ToActionResult(result, createdAtGetById: true);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] MovieUpsertDto input,
            CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return NotFound(new ApiErrorDto($"Movie with ID {id} was not found."));
            }

            var result = await _movieService.UpdateMovieAsync(id, input, cancellationToken);
            return ToActionResult(result, createdAtGetById: false);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return NotFound(new ApiErrorDto($"Movie with ID {id} was not found."));
            }

            var result = await _movieService.DeleteMovieAsync(id, cancellationToken);
            return result.Outcome switch
            {
                ServiceOutcome.Success => NoContent(),
                ServiceOutcome.NotFound => NotFound(new ApiErrorDto(result.ErrorMessage ?? "Movie not found.")),
                ServiceOutcome.Conflict => Conflict(new ApiErrorDto(result.ErrorMessage ?? "Movie cannot be deleted while reviews reference it.")),
                _ => BadRequest(new ApiErrorDto(result.ErrorMessage ?? "Unable to delete movie."))
            };
        }

        private IActionResult ToActionResult(ServiceResult<MovieDto> result, bool createdAtGetById)
        {
            return result.Outcome switch
            {
                ServiceOutcome.Success when result.Data is not null && createdAtGetById =>
                    CreatedAtAction(nameof(GetById), new { id = result.Data.MovieId }, result.Data),
                ServiceOutcome.Success when result.Data is not null =>
                    Ok(result.Data),
                ServiceOutcome.NotFound =>
                    NotFound(new ApiErrorDto(result.ErrorMessage ?? "Movie not found.")),
                ServiceOutcome.Conflict =>
                    Conflict(new ApiErrorDto(result.ErrorMessage ?? "Conflict while saving movie.", result.ErrorField)),
                _ =>
                    BadRequest(new ApiErrorDto(
                        result.ErrorMessage ?? "Validation failed.",
                        result.ErrorField,
                        result.Errors.Count > 0 ? result.Errors : null))
            };
        }
    }
}
