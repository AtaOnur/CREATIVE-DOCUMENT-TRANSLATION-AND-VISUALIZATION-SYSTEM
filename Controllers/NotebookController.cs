using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Kaydedilen AI sonuçları için notebook/history ekranlarını yönetir.
 * [TR] Neden gerekli: Kullanıcının AI çıktıları üzerinde arama, filtre, not düzenleme ve silme işlemleri.
 * [TR] İlgili: DocumentService notebook metotları, Views/Notebook/*
 *
 * MODIFICATION NOTES (TR)
 * - Klasor, etiket, favori ve export ozellikleri ileride eklenebilir.
 * - Isbirligi (collaboration) bu surumde yoktur.
 * - Genel resim OCR destegi bu surumde yer almamaktadir.
 * - Zorluk: Orta.
 */
[Authorize]
public class NotebookController : Controller
{
    private readonly IDocumentService _documentService;

    public NotebookController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? operation, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var model = await _documentService.GetNotebookAsync(email, q, operation, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var model = await _documentService.GetNotebookDetailsAsync(email, id, cancellationToken);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var model = await _documentService.GetNotebookEditAsync(email, id, cancellationToken);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(NotebookEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = User.Identity!.Name!;
        var result = await _documentService.UpdateNotebookEntryAsync(email, model, cancellationToken);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Notebook kaydi guncellenemedi.");
            return View(model);
        }

        TempData["NotebookMessage"] = "Notebook kaydi guncellendi.";
        return RedirectToAction(nameof(Details), new { id = model.AiResultId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var result = await _documentService.DeleteNotebookEntryAsync(email, id, cancellationToken);
        TempData["NotebookMessage"] = result.Ok ? "Notebook kaydi silindi." : (result.ErrorMessage ?? "Kayit silinemedi.");
        return RedirectToAction(nameof(Index));
    }
}

