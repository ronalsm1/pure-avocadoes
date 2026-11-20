using Microsoft.AspNetCore.Mvc.ModelBinding;
using PureAvocadoes.Services;
using System.Globalization;
using System.Security.Claims;

namespace PureAvocadoes.Controllers
{
    /// Shared claim-parsing and ModelState error-mapping helpers for MVC and API controllers.
    internal static class ControllerExtensions
    {
        internal static bool TryGetUserId(this ClaimsPrincipal? principal, out int userId)
        {
            userId = 0;
            return principal?.Identity?.IsAuthenticated == true
                && int.TryParse(
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out userId)
                && userId > 0;
        }

        internal static void AddServiceErrors(
            this ModelStateDictionary modelState,
            ServiceResult result,
            string? fallbackMessage = null)
        {
            if (result.Errors.Count > 0)
            {
                foreach (var entry in result.Errors)
                {
                    foreach (var message in entry.Value)
                    {
                        modelState.AddModelError(entry.Key, message);
                    }
                }

                return;
            }

            var messageToAdd = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ErrorMessage
                : fallbackMessage;

            if (!string.IsNullOrWhiteSpace(messageToAdd))
            {
                modelState.AddModelError(result.ErrorField ?? string.Empty, messageToAdd);
            }
        }
    }
}
