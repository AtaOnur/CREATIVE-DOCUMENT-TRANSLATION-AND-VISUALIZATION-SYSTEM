using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;
using pdf_bitirme.Services.Ai;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: AI işlem isteklerini alır, servisi çağırır ve sonuç sayfasını döner.
 * [TR] Neden gerekli: Workspace AI panelini controller üzerinden sade bir MVC akışına bağlar.
 * [TR] İlgili: IAiService, MultiProviderAiService, DocumentService, Views/Ai/Result.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - ModelsForTask endpoint'i UI'ın task'e göre model listesini dinamik yüklemesini sağlar.
 * - Yeni task tipi eklemek: SupportedOps'a yeni değer eklenir.
 * - Yeni sağlayıcı: MultiProviderAiService'e yeni dal eklenir, bu dosyaya dokunulmaz.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Orta.
 *
 * JÜRI MODİFİKASYON NOTLARI (TR)
 * - "Model listesi nereden geliyor?" → appsettings.json → Ai:Models → AiOptions üzerinden.
 * - "Yeni task eklemek?" → SupportedOps + appsettings + UI dropdown güncellenir.
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
    private readonly AiOptions _aiOptions;

    public AiController(
        IDocumentService documentService,
        IAiService aiService,
        IOptions<AiOptions> aiOptions)
    {
        _documentService = documentService;
        _aiService = aiService;
        _aiOptions = aiOptions.Value;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /Ai/ModelsForTask?task=Translate
    // [TR] İstemcinin seçtiği task için uygun model listesini JSON olarak döner.
    //      UI bu endpoint'i task değiştiğinde fetch ile çağırır ve model dropdown'ı günceller.
    //
    // Örnek yanıt:
    // [
    //   { "id": "gemini-2.5-flash", "label": "Gemini 2.5 Flash", "provider": "Gemini" },
    //   { "id": "facebook/nllb-200-distilled-600M", "label": "NLLB-200 ...", "provider": "HuggingFace" }
    // ]
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult ModelsForTask([FromQuery] string task)
    {
        if (string.IsNullOrWhiteSpace(task))
            return BadRequest(new { ok = false, message = "Task parametresi gerekli." });

        var models = _aiOptions.Models
            .Where(m => m.Tasks.Contains(task, StringComparer.OrdinalIgnoreCase))
            .Select(m => new { id = m.Id, label = m.Label, provider = m.Provider })
            .ToList();

        return Ok(models);
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

