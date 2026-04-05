using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Services;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Giriş yapmış kullanıcının panosu — özet, son belgeler, aktivite, devam et.
 * [TR] Neden gerekli: [Authorize] ile korumalı uygulama alanına giriş; tek ekranda proje özeti.
 * [TR] İlgili: DocumentService.GetDashboardAsync, Views/Dashboard/Index.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Rol bazlı pano (yönetici genel istatistik).
 * - Bildirimler veya hatırlatıcılar widget’ı.
 * - Zorluk: Kolay.
 */
[Authorize]
public class DashboardController : Controller
{
    private readonly IDocumentService _documentService;

    public DashboardController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>Kullanıcıya özel özet verileri yükler.</summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrEmpty(email))
            return RedirectToAction(nameof(AccountController.Login), "Account");

        var model = await _documentService.GetDashboardAsync(email, cancellationToken);
        return View(model);
    }
}
