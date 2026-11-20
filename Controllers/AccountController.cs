using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Services;
using PureAvocadoes.ViewModels;
using System.Data.Common;

namespace PureAvocadoes.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountService _accountService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AccountService accountService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectAfterAuth(returnUrl);
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accountService.AuthenticateAsync(model.Email, model.Password, cancellationToken);
            if (!result.Succeeded || result.User is null)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid email or password.");
                return View(model);
            }

            var principal = _accountService.CreateClaimsPrincipal(result.User);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectAfterAuth(model.ReturnUrl);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectAfterAuth(returnUrl);
            }

            return View(new RegisterViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accountService.RegisterAsync(model, cancellationToken);
            if (!result.Succeeded || result.User is null)
            {
                ModelState.AddModelError(result.ErrorField ?? string.Empty, result.ErrorMessage ?? "Registration failed.");
                return View(model);
            }

            var principal = _accountService.CreateClaimsPrincipal(result.User);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            TempData["StatusMessage"] = "Welcome to Pure Avocadoes! Your account is ready.";
            return RedirectAfterAuth(model.ReturnUrl);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile(
            int? id,
            [FromQuery] int? userId,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentUserId))
            {
                return Challenge();
            }

            if ((id.HasValue && id.Value != currentUserId)
                || (userId.HasValue && userId.Value != currentUserId))
            {
                return Forbid();
            }

            try
            {
                var pageModel = await _accountService.GetProfilePageAsync(currentUserId, cancellationToken);
                if (pageModel is null)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return RedirectToAction(nameof(Login));
                }

                return View(pageModel);
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load profile for UserId {UserId} ({ExceptionType}).", currentUserId, ex.GetType().Name);
                return View(new ProfilePageViewModel
                {
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to load your profile from the database right now. Please try again shortly."
                });
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(
            int? id,
            ProfileUpdateViewModel model,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentUserId))
            {
                return Challenge();
            }

            if (id.HasValue && id.Value != currentUserId)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var invalidPage = await BuildProfilePageFallbackAsync(currentUserId, model, cancellationToken);
                return View("Profile", invalidPage);
            }

            var result = await _accountService.UpdateOwnProfileAsync(currentUserId, model, cancellationToken);
            if (!result.Succeeded || result.Data is null)
            {
                ModelState.AddServiceErrors(result);
                var errorPage = await BuildProfilePageFallbackAsync(currentUserId, model, cancellationToken);
                return View("Profile", errorPage);
            }

            var refreshedPrincipal = _accountService.CreateClaimsPrincipal(result.Data);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, refreshedPrincipal);

            TempData["StatusMessage"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            int? id,
            ChangePasswordViewModel model,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out var currentUserId))
            {
                return Challenge();
            }

            if (id.HasValue && id.Value != currentUserId)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                var invalidPage = await BuildProfilePageFallbackAsync(currentUserId, null, cancellationToken);
                return View("Profile", invalidPage);
            }

            var result = await _accountService.ChangeOwnPasswordAsync(currentUserId, model, cancellationToken);
            if (!result.Succeeded)
            {
                ModelState.AddServiceErrors(result);
                var errorPage = await BuildProfilePageFallbackAsync(currentUserId, null, cancellationToken);
                return View("Profile", errorPage);
            }

            TempData["StatusMessage"] = "Your password has been changed.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<ProfilePageViewModel> BuildProfilePageFallbackAsync(
            int currentUserId,
            ProfileUpdateViewModel? draftUpdate,
            CancellationToken cancellationToken)
        {
            var pageModel = await _accountService.GetProfilePageAsync(currentUserId, cancellationToken)
                ?? new ProfilePageViewModel();

            if (draftUpdate is not null)
            {
                pageModel.UpdateForm = new ProfileUpdateViewModel
                {
                    FullName = draftUpdate.FullName ?? string.Empty,
                    Email = draftUpdate.Email ?? string.Empty
                };
            }

            return pageModel;
        }

        private IActionResult RedirectAfterAuth(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
