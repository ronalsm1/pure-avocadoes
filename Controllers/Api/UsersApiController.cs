using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Dtos;
using PureAvocadoes.Services;

namespace PureAvocadoes.Controllers.Api
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/users")]
    public class UsersApiController : ControllerBase
    {
        private readonly AdminUserService _adminUserService;

        public UsersApiController(AdminUserService adminUserService)
        {
            _adminUserService = adminUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? role,
            CancellationToken cancellationToken)
        {
            var result = await _adminUserService.GetUsersAsync(search, role, cancellationToken);
            if (!result.Succeeded)
            {
                return BadRequest(new ApiErrorDto(result.ErrorMessage ?? "Invalid query parameters.", result.ErrorField));
            }

            return Ok(result.Data);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return NotFound(new ApiErrorDto($"User with ID {id} was not found."));
            }

            var user = await _adminUserService.GetUserDetailsAsync(id, cancellationToken);
            if (user is null)
            {
                return NotFound(new ApiErrorDto($"User with ID {id} was not found."));
            }

            return Ok(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [FromBody] UserCreateDto input,
            CancellationToken cancellationToken)
        {
            var result = await _adminUserService.CreateUserAsync(input, cancellationToken);
            return ToActionResult(result, createdAtGetById: true);
        }

        [HttpPut("{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UserUpdateDto input,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentAdminId))
            {
                return Unauthorized(new ApiErrorDto("Authentication is required."));
            }

            if (id <= 0)
            {
                return NotFound(new ApiErrorDto($"User with ID {id} was not found."));
            }

            var result = await _adminUserService.UpdateUserAsync(id, currentAdminId, input, cancellationToken);
            return ToActionResult(result, createdAtGetById: false);
        }

        [HttpDelete("{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentAdminId))
            {
                return Unauthorized(new ApiErrorDto("Authentication is required."));
            }

            if (id <= 0)
            {
                return NotFound(new ApiErrorDto($"User with ID {id} was not found."));
            }

            var result = await _adminUserService.DeleteUserAsync(id, currentAdminId, cancellationToken);
            return result.Outcome switch
            {
                ServiceOutcome.Success => NoContent(),
                ServiceOutcome.NotFound => NotFound(new ApiErrorDto(result.ErrorMessage ?? $"User with ID {id} was not found.")),
                ServiceOutcome.Conflict => Conflict(new ApiErrorDto(result.ErrorMessage ?? "User account cannot be deleted.")),
                _ => BadRequest(new ApiErrorDto(result.ErrorMessage ?? "Unable to delete user."))
            };
        }

        private IActionResult ToActionResult(ServiceResult<UserDto> result, bool createdAtGetById)
        {
            return result.Outcome switch
            {
                ServiceOutcome.Success when result.Data is not null && createdAtGetById =>
                    CreatedAtAction(nameof(GetById), new { id = result.Data.UserId }, result.Data),
                ServiceOutcome.Success when result.Data is not null =>
                    Ok(result.Data),
                ServiceOutcome.NotFound =>
                    NotFound(new ApiErrorDto(result.ErrorMessage ?? "User was not found.")),
                ServiceOutcome.Conflict =>
                    Conflict(new ApiErrorDto(result.ErrorMessage ?? "Conflict while saving user.", result.ErrorField)),
                _ =>
                    BadRequest(new ApiErrorDto(
                        result.ErrorMessage ?? "Validation failed.",
                        result.ErrorField,
                        result.Errors.Count > 0 ? result.Errors : null))
            };
        }
    }
}
