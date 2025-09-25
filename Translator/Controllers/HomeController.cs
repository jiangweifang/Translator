using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Translator.Models;

namespace Translator.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TranslationHub _hub;

        public HomeController(ILogger<HomeController> logger, TranslationHub hub)
        {
            _logger = logger;
            _hub = hub;
        }

        public IActionResult Index()
        {
            _hub.Init("zh-CN", "ja", "zh-CN-XiaoxiaoMultilingualNeural");
            return View();
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
