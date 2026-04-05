using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Genel MVC denetleyicisi — ana sayfa, gizlilik, hata sayfası.
 * [TR] Neden gerekli: Varsayılon şablonun çekirdeği; Index artık bitirme tanıtımı gösterir.
 * [TR] İlgili: Views/Home/Index.cshtml, Privacy.cshtml, Shared/Error.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - App/Dashboard veya Document işlemleri için ayrı denetleyiciler eklenecek.
 * - Anonim erişim kısıtları Authorize attribute ile.
 * - Zorluk: Kolay.
 */
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    /// <summary>Bitirme projesi tanıtım ana sayfası.</summary>
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>Gizlilik / KVKK yer tutucu.</summary>
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
