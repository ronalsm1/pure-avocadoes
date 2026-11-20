using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Services;
using PureAvocadoes.ViewModels;
using System.Data.Common;

namespace PureAvocadoes.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/Users")]
    public class AdminUsersController : Controller
    {
        private readonly AdminUserService _adminUserService;
        private readonly AccountService _accountService;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(
            AdminUserService adminUserService,
            AccountService accountService,
            ILogger<AdminUsersController> logger)
        {
            _adminUserService = adminUserService;
            _accountService = accountService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(
            string? search,
            string? role,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _adminUserService.GetUsersAsync(search, role, cancellationToken);
                if (!result.Succeeded)
                {
                    return View(new AdminUsersIndexViewModel
                    {
                        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim(),
                        ErrorMessage = result.ErrorMessage
                    });
                }

                return View(new AdminUsersIndexViewModel
                {
                    Users = result.Data ?? [],
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim()
                });
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load admin users list ({ExceptionType}).", ex.GetType().Name);
                return View(new AdminUsersIndexViewModel
                {
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim(),
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to connect to the database right now. Please try again shortly."
                });
            }
        }

        [HttpGet("Details/{id:int?}")]
        public async Task<IActionResult> Details(int? id, CancellationToken cancellationToken)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                return UserNotFoundView();
            }

            var detail = await _adminUserService.GetUserDetailsAsync(id.Value, cancellationToken);
            if (detail is null)
            {
                return UserNotFoundView();
            }

            return View(detail);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View(new AdminUserCreateViewModel());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserCreateViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _adminUserService.CreateUserAsync(model.ToCreateDto(), cancellationToken);
            if (!result.Succeeded || result.Data is null)
            {
                ModelState.AddServiceErrors(result, "Unable to save user.");
                return View(model);
            }

            TempData["StatusMessage"] = $"Created account for \"{result.Data.FullName}\" ({result.Data.Role}).";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:int?}")]
        public async Task<IActionResult> Edit(int? id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentAdminId))
            {
                return Challenge();
            }

            if (!id.HasValue || id.Value <= 0)
            {
                return UserNotFoundView();
            }

            var model = await _adminUserService.GetUserForEditAsync(id.Value, currentAdminId, cancellationToken);
            if (model is null)
            {
                return UserNotFoundView();
            }

            return View(model);
        }

        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminUserEditViewModel model, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentAdminId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return UserNotFoundView();
            }

            model.UserId = id;

            if (!ModelState.IsValid)
            {
                await PopulateEditFlagsAsync(model, currentAdminId, cancellationToken);
                return View(model);
            }

            var result = await _adminUserService.UpdateUserAsync(id, currentAdminId, model.ToUpdateDto(), cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                return UserNotFoundView();
            }

            if (!result.Succeeded || result.Data is null)
            {
                ModelState.AddServiceErrors(result, "Unable to save user.");
                await PopulateEditFlagsAsync(model, currentAdminId, cancellationToken);
                if (result.Outcome == ServiceOutcome.Conflict)
                {
                    Response.StatusCode = StatusCodes.Status409Conflict;
                }

                return View(model);
            }

            if (id == currentAdminId)
            {
                var refreshedPrincipal = _accountService.CreateClaimsPrincipal(
                    new AuthenticatedUserInfo(result.Data.UserId, result.Data.FullName, result.Data.Email, result.Data.Role));
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, refreshedPrincipal);
            }

            TempData["StatusMessage"] = $"Updated user \"{result.Data.FullName}\" ({result.Data.Role}).";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Delete/{id:int?}")]
        public async Task<IActionResult> Delete(int? id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentAdminId))
            {
                return Challenge();
            }

            if (!id.HasValue || id.Value <= 0)
            {
                return UserNotFoundView();
            }

            var model = await _adminUserService.GetUserForDeleteAsync(id.Value, currentAdminId, cancellationToken);
            if (model is null)
            {
                return UserNotFoundView();
            }

            return View(model);
        }

        [HttpPost("Delete/{id:int}")]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentAdminId))
            {
                return Challenge();
            }

            if (id <= 0)
            {
                return UserNotFoundView();
            }

            var result = await _adminUserService.DeleteUserAsync(id, currentAdminId, cancellationToken);
            if (result.Outcome == ServiceOutcome.NotFound)
            {
                return UserNotFoundView();
            }

            if (result.Outcome == ServiceOutcome.Conflict)
            {
                var deleteModel = await _adminUserService.GetUserForDeleteAsync(id, currentAdminId, cancellationToken);
                if (deleteModel is null)
                {
                    return UserNotFoundView();
                }

                ViewData["ConflictError"] = result.ErrorMessage ?? "This user account cannot be deleted.";
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View("Delete", deleteModel);
            }

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Unable to delete user.";
                return RedirectToAction(nameof(Index));
            }

            TempData["StatusMessage"] = "User account deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateEditFlagsAsync(
            AdminUserEditViewModel model,
            int currentAdminId,
            CancellationToken cancellationToken)
        {
            var existing = await _adminUserService.GetUserForEditAsync(model.UserId, currentAdminId, cancellationToken);
            if (existing is not null)
            {
                model.IsCurrentUser = existing.IsCurrentUser;
                model.IsOnlyAdmin = existing.IsOnlyAdmin;
            }
        }

        private IActionResult UserNotFoundView()
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }
    }
}
