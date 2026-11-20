using Microsoft.AspNetCore.Mvc;
using PureAvocadoes.Models;
using PureAvocadoes.Services;
using PureAvocadoes.ViewModels;
using System.Data.Common;
using System.Diagnostics;

namespace PureAvocadoes.Controllers
{
    public class HomeController : Controller
    {
        private readonly MovieService _movieService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(MovieService movieService, ILogger<HomeController> logger)
        {
            _movieService = movieService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            try
            {
                var model = await _movieService.GetHomePageDataAsync(recentLimit: 12, cancellationToken: cancellationToken);
                return View(model);
            }
            catch (DbException ex)
            {
                _logger.LogWarning("Unable to load Home page catalog data due to database error ({ExceptionType}).", ex.GetType().Name);
                return View(new HomeIndexViewModel
                {
                    HasDatabaseError = true,
                    ErrorMessage = "Unable to connect to the movie catalog right now. Please try again shortly."
                });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
