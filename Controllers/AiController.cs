using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: AI işlem isteklerini alır, mock servisi çağırır ve sonuç sayfasını döner.
 * [TR] Neden gerekli: Workspace AI panelini controller üzerinden sade bir MVC akışına bağlar.
 * [TR] İlgili: IAiService, DocumentService, Views/Ai/Result.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - İleride gerçek model sağlayıcısı ile prompt şablonları burada genişletilebilir.
 * - Regenerate için alternatif model deneme seçeneği eklenebilir.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Orta.
 */
[Authorize]
public class AiController : Controller
{
    private static readonly HashSet<string> SupportedOps =
    [
        "Translate", "Summarize", "Rewrite", "CreativeWrite", "Visualize"
    ];

    private readonly IDocumentService _documentService;
    private readonly IAiService _aiService;

    public AiController(IDocumentService documentService, IAiService aiService)
    {
        _documentService = documentService;
        _aiService = aiService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process([FromBody] AiProcessRequestViewModel request, CancellationToken cancellationToken)
    {
        if (request.DocumentId == Guid.Empty)
            return BadRequest(new { ok = false, message = "Belge kimliği zorunlu." });

        if (!SupportedOps.Contains(request.OperationType ?? string.Empty))
            return BadRequest(new { ok = false, message = "Geçersiz AI işlem tipi." });

        if (string.IsNullOrWhiteSpace(request.InputText))
            return BadRequest(new { ok = false, message = "İşlenecek metin boş olamaz." });

        var email = User.Identity!.Name!;
        var workspace = await _documentService.GetWorkspaceAsync(email, request.DocumentId, cancellationToken);
        if (workspace == null)
            return NotFound(new { ok = false, message = "Belge bulunamadı." });

        AiServiceResult aiResult;
        try
        {
            aiResult = await _aiService.ProcessAsync(workspace.Title, request, cancellationToken);
        }
        catch (Exception ex)
        {
            // [TR] Gemini API hatası veya bağlantı sorunu; kullanıcıya anlamlı mesaj döner.
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { ok = false, message = $"AI işlemi başarısız: {msg}" });
        }

        var save = await _documentService.SaveAiResultAsync(email, request.DocumentId, request, aiResult, cancellationToken);
        if (!save.Ok || save.AiResultId == null)
            return BadRequest(new { ok = false, message = save.ErrorMessage ?? "AI sonucu kaydedilemedi." });

        return Json(new
        {
            ok = true,
            message = "AI işlemi tamamlandı.",
            aiResultId = save.AiResultId,
            outputText = aiResult.OutputText,
            outputImageUrl = aiResult.OutputImageUrl,
            resultUrl = Url.Action(nameof(Result), new { id = save.AiResultId }) ?? string.Empty,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Result(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var model = await _documentService.GetAiResultPageAsync(email, id, cancellationToken);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var result = await _documentService.MarkAiResultSavedAsync(email, id, cancellationToken);
        TempData["AiResultMessage"] = result.Ok ? "AI sonucu kaydedildi." : (result.ErrorMessage ?? "AI sonucu kaydedilemedi.");
        return RedirectToAction(nameof(Result), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var request = await _documentService.GetAiRequestFromResultAsync(email, id, cancellationToken);
        if (request == null)
            return NotFound();

        var workspace = await _documentService.GetWorkspaceAsync(email, request.DocumentId, cancellationToken);
        if (workspace == null)
            return NotFound();

        var aiResult = await _aiService.ProcessAsync(workspace.Title, request, cancellationToken);
        var save = await _documentService.SaveAiResultAsync(email, request.DocumentId, request, aiResult, cancellationToken);
        if (!save.Ok || save.AiResultId == null)
        {
            TempData["AiResultMessage"] = save.ErrorMessage ?? "Yeniden üretim başarısız.";
            return RedirectToAction(nameof(Result), new { id });
        }

        TempData["AiResultMessage"] = "AI sonucu yeniden üretildi.";
        return RedirectToAction(nameof(Result), new { id = save.AiResultId.Value });
    }
}

