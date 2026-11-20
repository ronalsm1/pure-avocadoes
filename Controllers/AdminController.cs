using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Services;
using PureAvocadoes.ViewModels;
using System.Data.Common;

namespace PureAvocadoes.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Admin")]
    public class AdminController : Controller
    {
        private readonly AdminDashboardService _dashboardService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            AdminDashboardService dashboardService,
            ILogger<AdminController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            try
            {
                var model = await _dashboardService.GetDashboardAsync(cancellationToken);
                return View(model);
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load admin dashboard summary ({ExceptionType}).", ex.GetType().Name);
                return View(new AdminDashboardViewModel
                {
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to connect to the database right now. Please try again shortly."
                });
            }
        }
    }
}
