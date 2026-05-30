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
 * - Result: Admin rolünde GetAiResultPageByIdAsync ile başka kullanıcının sonucu salt okunur görüntülenir.
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
        // [TR] "Explanation": kullanıcının seçtiği metin ve/veya "Görsel Seç"
        //      ile yakaladığı görseli analiz eder. Çıktı: içerik nedir + ne anlatıyor.
        //      Multimodal (Gemini) için görsel + metin birlikte değerlendirilir;
        //      sadece metin destekleyen sağlayıcılarda metin analizine düşer.
        "Translate", "Summarize", "Rewrite", "CreativeWrite", "Visualize", "Explanation", "Math"
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
            return BadRequest(new { ok = false, message = "Task parameter is required." });

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
            return BadRequest(new { ok = false, message = "Document ID is required." });

        if (!SupportedOps.Contains(request.OperationType ?? string.Empty))
            return BadRequest(new { ok = false, message = "Invalid AI operation type." });

        // [TR] Artık üç girdiden en az biri yeterli:
        //      - InputText (OCR/serbest metin)
        //      - CustomInstruction (kullanıcı prompt'u)
        //      - InputImageBase64 ("Görsel Seç" ile yakalanmış multimodal görsel)
        var hasText = !string.IsNullOrWhiteSpace(request.InputText);
        var hasInstruction = !string.IsNullOrWhiteSpace(request.CustomInstruction);
        var hasImage = !string.IsNullOrWhiteSpace(request.InputImageBase64);
        if (!hasText && !hasInstruction && !hasImage)
            return BadRequest(new { ok = false, message = "Text, image, or instruction is required for processing." });

        var email = User.Identity!.Name!;
        var workspace = await _documentService.GetWorkspaceAsync(email, request.DocumentId, cancellationToken);
        if (workspace == null)
            return NotFound(new { ok = false, message = "Document not found." });

        AiServiceResult aiResult;
        try
        {
            aiResult = await _aiService.ProcessAsync(workspace.Title, request, cancellationToken);
        }
        catch (Exception ex)
        {
            // [TR] Gemini API hatası veya bağlantı sorunu; kullanıcıya anlamlı mesaj döner.
            var msg = ex.InnerException?.Message ?? ex.Message;
            return BadRequest(new { ok = false, message = $"AI operation failed: {msg}" });
        }

        var save = await _documentService.SaveAiResultAsync(email, request.DocumentId, request, aiResult, cancellationToken);
        if (!save.Ok || save.AiResultId == null)
            return BadRequest(new { ok = false, message = save.ErrorMessage ?? "AI sonucu kaydedilemedi." });

        return Json(new
        {
            ok = true,
            message = "AI operation completed.",
            aiResultId = save.AiResultId,
            outputText = aiResult.OutputText,
            outputImageUrl = aiResult.OutputImageUrl,
            resultUrl = Url.Action(nameof(Result), new { id = save.AiResultId }) ?? string.Empty,
        });
    }

    [HttpGet]
    public async Task<IActionResult> Result(Guid id, CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole("Admin");
        var model = isAdmin
            ? await _documentService.GetAiResultPageByIdAsync(id, cancellationToken)
            : await _documentService.GetAiResultPageAsync(User.Identity!.Name!, id, cancellationToken);
        if (model == null)
            return NotFound();

        ViewBag.IsAdminModerationView = isAdmin;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var result = await _documentService.MarkAiResultSavedAsync(email, id, cancellationToken);
        TempData["AiResultMessage"] = result.Ok ? "AI result saved." : (result.ErrorMessage ?? "AI result could not be saved.");
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
            TempData["AiResultMessage"] = save.ErrorMessage ?? "Regeneration failed.";
            return RedirectToAction(nameof(Result), new { id });
        }

        TempData["AiResultMessage"] = "AI result regenerated.";
        return RedirectToAction(nameof(Result), new { id = save.AiResultId.Value });
    }
}

