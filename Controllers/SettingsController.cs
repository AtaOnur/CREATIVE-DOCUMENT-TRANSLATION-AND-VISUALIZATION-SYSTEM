using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Hesap ve AI tercihleri ayar ekranini yonetir.
 * [TR] Neden gerekli: Kullanici varsayilan model/stil/tema tercihlerini basit sekilde saklayabilsin.
 * [TR] Ilgili: DocumentService.GetSettingsAsync/UpdateSettingsAsync, Views/Settings/Index.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Cok dilli UI, bildirim ve API key ayarlari ileride eklenebilir.
 * - Gelismis admin/security paneli bu surumde yoktur.
 * - Genel resim OCR destegi bu surumde yer almamaktadir.
 * - Zorluk: Kolay.
 */
[Authorize]
public class SettingsController : Controller
{
    private readonly IDocumentService _documentService;

    public SettingsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var model = await _documentService.GetSettingsAsync(email, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(SettingsViewModel model, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var baseline = await _documentService.GetSettingsAsync(email, cancellationToken);
        model.AvailableAiModels = baseline.AvailableAiModels;

        if (!ModelState.IsValid)
            return View("Index", model);

        var result = await _documentService.UpdateSettingsAsync(email, model, cancellationToken);
        TempData["SettingsMessage"] = result.Ok ? "Ayarlar kaydedildi." : (result.ErrorMessage ?? "Ayarlar kaydedilemedi.");
        return RedirectToAction(nameof(Index));
    }
}

