using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Genel MVC denetleyicisi — ana sayfa, gizlilik, SSS, hata sayfası.
 * [TR] Neden gerekli: Anonim ziyaretçiye landing (Views/Home/Index) ve statik bilgi sayfalarını sunar.
 * [TR] İlgili: Views/Home/Index.cshtml, Privacy.cshtml, Faq.cshtml, Shared/Error.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Index landing içeriği workspace AI, rehber, admin moderasyon ve notebook özelliklerini tanıtır.
 * - Views/Home/Index.cshtml + site.css .home-* ile modern landing düzeni (hero, adımlar, özellik ızgarası).
 * - Oturum açmış kullanıcıya Index.cshtml içinde Dashboard/Upload CTA gösterilir (controller değişikliği gerekmez).
 * - App/Dashboard veya Document işlemleri ayrı denetleyicilerdedir.
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

    /// <summary>Sıkça Sorulan Sorular sayfası.</summary>
    // [TR] Kullanıcıların sisteme dair en sık karşılaştıkları
    //      soruları tek sayfada toplar (OCR, AI modelleri,
    //      dosya limitleri, hesap yönetimi vb.).
    public IActionResult Faq()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
