using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;
using pdf_bitirme.Services.Ai;
using pdf_bitirme.Services.Ocr;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Belge listesi, yükleme (GET/POST), detay, silme — PDF MVP.
 * [TR] Neden gerekli: CreativeDoc çekirdeği; seçili bölge OCR ve AI workspace bu controller ile bağlıdır.
 * [TR] İlgili: DocumentService, Views/Documents/*, Gemini TTS (OCR metni seslendirme)
 *
 * MODIFICATION NOTES (TR)
 * - Toplu silme, dışa aktarma.
 * - Belge önizleme ve PDF içi bölge seçimi; OCR yalnızca seçili bölgede çalışır.
 * - Ocr:UseMock=false ayarıyla gerçek Tesseract OCR servisine geçiş desteklenir.
 * - NarrateOcrSpeech: OCR textarea metnini Gemini TTS ile sese çevirir (Paddle/Tesseract OCR’dan bağımsız).
 * - Details: banlı belge → Banned.cshtml; ilk belge → ShowWorkspaceGuide.
 * - DismissWorkspaceGuide: workspace rehberi kapatılınca user_settings.WorkspaceGuideCompleted güncellenir.
 * - Genel görüntü OCR future work.
 * - Zorluk: Orta.
 */
[Authorize]
public class DocumentsController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly IOcrService _ocrService;
    /// <summary>OCR çıktısını Gemini TTS ile sese çeviren servis (workspace “seslendir” butonu).</summary>
    private readonly IGeminiTtsSpeechService _geminiTtsSpeech;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// [TR] OCR motoru seçimi + çalışma alanı OCR metninin TTS için bağı — Gemini TTS burada enjekte edilir.
    /// </summary>
    public DocumentsController(
        IDocumentService documentService,
        IOcrService ocrService,
        IGeminiTtsSpeechService geminiTtsSpeech,
        IServiceProvider services,
        IConfiguration configuration)
    {
        _documentService = documentService;
        _ocrService = ocrService;
        _geminiTtsSpeech = geminiTtsSpeech;
        _services = services;
        _configuration = configuration;
    }

    /// <summary>
    /// [TR] İstek gövdesinde belirtilen "Engine" değerine göre uygun OCR servisini döner.
    /// Geçersiz/boş motor isteklerinde appsettings varsayılanı (_ocrService) kullanılır.
    /// Windows dışı ortamlarda yine varsayılana düşer (concrete servisler kayıtlı değil).
    /// </summary>
    /// <summary>
    /// [TR] İstemciden gelen base64 görsel verisini byte dizisine dönüştürür.
    /// "data:image/png;base64,..." prefix'i varsa temizlenir; geçersiz/boş veri null döner.
    /// </summary>
    private static byte[]? TryDecodeImage(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        var raw = base64.Trim();
        var commaIdx = raw.IndexOf(',');
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx > 0)
            raw = raw[(commaIdx + 1)..];
        try
        {
            return Convert.FromBase64String(raw);
        }
        catch
        {
            return null;
        }
    }

    private IOcrService ResolveOcrService(string? requestedEngine)
    {
        if (string.IsNullOrWhiteSpace(requestedEngine))
            return _ocrService;

        if (!OperatingSystem.IsWindows())
            return _ocrService;

        // [TR] PaddleOcrService / TesseractCliOcrService Windows-only sınıflar;
        //      yukarıdaki IsWindows kontrolü ile koruma var, CA1416 pragma ile susturulur.
#pragma warning disable CA1416
        if (string.Equals(requestedEngine, "Paddle", StringComparison.OrdinalIgnoreCase))
        {
            var paddle = _services.GetService<PaddleOcrService>();
            if (paddle != null) return paddle;
        }
        else if (string.Equals(requestedEngine, "Tesseract", StringComparison.OrdinalIgnoreCase))
        {
            var tess = _services.GetService<TesseractCliOcrService>();
            if (tess != null) return tess;
        }
#pragma warning restore CA1416

        return _ocrService;
    }

    /// <summary>Filtrelenebilir belge listesi.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var model = await _documentService.ListAsync(email, q, status, cancellationToken);
        return View(model);
    }

    /// <summary>Yükleme formu.</summary>
    [HttpGet]
    public IActionResult Upload()
    {
        return View(new UploadViewModel());
    }

    /// <summary>PDF doğrulama ve diske kayıt.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(UploadViewModel model, CancellationToken cancellationToken)
    {
        if (model.File == null || model.File.Length == 0)
            ModelState.AddModelError(nameof(model.File), "Please select a PDF file.");

        if (!model.CopyrightResponsibilityAccepted)
        {
            ModelState.AddModelError(
                nameof(model.CopyrightResponsibilityAccepted),
                "You must accept responsibility for copyright, permissions, and any changes made to this document before uploading.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var email = User.Identity!.Name!;
        var (ok, err) = await _documentService.UploadAsync(email, model.File!, model.Title, cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, err ?? "Upload could not be completed.");
            return View(model);
        }

        TempData["DocumentsMessage"] = "Document uploaded successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Belge çalışma alanı (PDF viewer + seçim overlay + OCR placeholder).</summary>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var access = await _documentService.GetOwnerDocumentAccessAsync(email, id, cancellationToken);
        if (access.Status == OwnerDocumentAccessStatus.NotFound)
            return NotFound();
        if (access.Status == OwnerDocumentAccessStatus.Banned)
        {
            return View("Banned", new DocumentBannedViewModel
            {
                Id = id,
                Title = access.Title,
                BanReason = access.BanReason,
            });
        }

        var model = await _documentService.GetWorkspaceAsync(email, id, cancellationToken);
        if (model == null)
            return NotFound();

        model.PdfEndpointUrl = Url.Action(nameof(Pdf), new { id }) ?? string.Empty;
        model.PagePreviewEndpointUrl = Url.Action(nameof(PagePreview), new { id }) ?? string.Empty;
        model.ExtractTextEndpointUrl = Url.Action(nameof(ExtractText)) ?? string.Empty;
        model.SaveOcrEndpointUrl = Url.Action(nameof(SaveOcrText)) ?? string.Empty;
        // [TR] OCR textarea’daki yazı NarrateOcrSpeech ile gönderilir; OCR motor seçimi ile ilgisi yoktur (sadece metin).
        model.NarrateSpeechEndpointUrl = Url.Action(nameof(NarrateOcrSpeech)) ?? string.Empty;
        return View(model);
    }

    /// <summary>First-time workspace onboarding guide — shown once per user on their first document.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissWorkspaceGuide(CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        await _documentService.MarkWorkspaceGuideCompletedAsync(email, cancellationToken);
        return Ok(new { ok = true });
    }

    /// <summary>Workspace içindeki PDF görüntüleme isteği için dosya akışı döner.</summary>
    [HttpGet]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var (stream, contentType, fileName) = await _documentService.OpenPdfAsync(email, id, cancellationToken);
        if (stream == null || contentType == null || fileName == null)
            return NotFound();

        // [TR] Dosyayi indirme eki yerine tarayicida inline gosteriyoruz.
        // Aksi halde sayfa yenilemelerinde PDF tekrar tekrar indirilmeye calisir.
        return File(stream, contentType, enableRangeProcessing: true);
    }

    /// <summary>PDF sayfasını PNG olarak döndürür (pdf.js render sorunu için fallback).</summary>
    [HttpGet]
    public async Task<IActionResult> PagePreview(Guid id, int page = 1, int dpi = 170, CancellationToken cancellationToken = default)
    {
        var email = User.Identity!.Name!;
        var pdfPath = await _documentService.GetPdfPhysicalPathAsync(email, id, cancellationToken);
        if (string.IsNullOrWhiteSpace(pdfPath))
            return NotFound();

        var pdftoppmPath = _configuration["Ocr:PdfToPpmPath"] ?? "pdftoppm";
        var tempRoot = Path.Combine(Path.GetTempPath(), "pdf_bitirme_preview");
        Directory.CreateDirectory(tempRoot);
        var key = Guid.NewGuid().ToString("N");
        var outBase = Path.Combine(tempRoot, $"preview_{key}");
        var outPng = $"{outBase}.png";

        try
        {
            var safePage = Math.Max(1, page);
            var safeDpi = Math.Clamp(dpi, 100, 280);
            var args = $"-f {safePage} -singlefile -png -r {safeDpi} \"{pdfPath}\" \"{outBase}\"";

            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pdftoppmPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !System.IO.File.Exists(outPng))
                return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(outPng, cancellationToken);
            return File(bytes, "image/png");
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(outPng))
                    System.IO.File.Delete(outPng);
            }
            catch
            {
                // ignore temp cleanup failure
            }
        }
    }

    /// <summary>Seçili PDF bölgesi için mock OCR tetikler ve sonucu kaydeder.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtractText([FromBody] OcrExtractRequestViewModel request, CancellationToken cancellationToken)
    {
        if (request.DocumentId == Guid.Empty)
            return BadRequest(new { ok = false, message = "Document ID is required." });

        if (request.Region == null || request.Region.Width <= 0 || request.Region.Height <= 0)
            return BadRequest(new { ok = false, message = "A valid region selection is required." });

        var email = User.Identity!.Name!;
        var workspace = await _documentService.GetWorkspaceAsync(email, request.DocumentId, cancellationToken);
        if (workspace == null)
            return NotFound(new { ok = false, message = "Document not found." });

        // [TR] Kullanıcı UI'da motor seçtiyse onu kullan; aksi halde appsettings varsayılanı.
        var ocrService = ResolveOcrService(request.Engine);

        string text;
        try
        {
            // [TR] Yeni akış: tarayıcı bölgeyi zaten kırpıp PNG olarak gönderdiyse
            //      sayfayı pdftoppm ile rasterize etmeye gerek yok — Poppler bağımlılığı kalkar.
            //      Bu, "pdftoppm bulunamadı" hatasını ortadan kaldırır.
            var imageBytes = TryDecodeImage(request.ImageBase64);
            if (imageBytes != null && imageBytes.Length > 0)
            {
                text = await ocrService.ExtractFromImageBytesAsync(
                    imageBytes, workspace.Title, request.Language, cancellationToken);
            }
            else
            {
                // [TR] Geriye dönük uyumluluk: tarayıcı görsel göndermediyse eski PDF
                //      tabanlı yola düş (pdftoppm gereklidir).
                var pdfPath = await _documentService.GetPdfPhysicalPathAsync(email, request.DocumentId, cancellationToken);
                if (string.IsNullOrWhiteSpace(pdfPath))
                    return BadRequest(new { ok = false, message = "PDF file path could not be found for OCR." });

                text = await ocrService.ExtractFromPdfRegionAsync(
                    pdfPath, workspace.Title, request.Region, request.Language, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // [TR] OCR motoru hatası (Python bulunamadı, model indirilemedi, pdftoppm hatası vb.)
            // Kullanıcıya anlaşılır mesaj döner; detay logda kalır.
            var shortMsg = ex.Message.Length > 300 ? ex.Message[..300] + "..." : ex.Message;
            return BadRequest(new { ok = false, message = $"OCR error: {shortMsg}" });
        }

        var save = await _documentService.SaveOcrResultAsync(email, request.DocumentId, request.Region, text, cancellationToken);
        if (!save.Ok)
            return BadRequest(new { ok = false, message = save.ErrorMessage ?? "OCR sonucu kaydedilemedi." });

        return Json(new
        {
            ok = true,
            message = "OCR result generated successfully.",
            ocrResultId = save.OcrResultId,
            text = save.ExtractedText ?? string.Empty,
        });
    }

    /// <summary>
    /// [TR] Workspace OCR metin kutusundaki içeriği Gemini TTS ile ses olarak döner (Content-Type MIME dinamiktir).
    /// Paddle veya Tesseract’un çalışma durumundan bağımsızdır; yalnızca gönderilen düz metin seslendirilir.
    /// Hata halinde JSON { ok:false, message } ile BadRequest kullanılır.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NarrateOcrSpeech(
        [FromBody] NarrateFromOcrRequestViewModel request,
        CancellationToken cancellationToken)
    {
        var plain = request?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(plain))
            return BadRequest(new { ok = false, message = "OCR text is required for narration." });
        var documentId = request!.DocumentId;
        if (documentId == Guid.Empty)
            documentId = TryGetDocumentIdFromReferer() ?? Guid.Empty;
        if (documentId == Guid.Empty)
            return BadRequest(new { ok = false, message = "Document ID is required for audio recording." });

        try
        {
            var textToSpeak = plain;
            var targetLang = (request.TargetLanguage ?? "English").Trim();
            if (!string.IsNullOrWhiteSpace(targetLang) &&
                !targetLang.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                var workspace = await _documentService.GetWorkspaceAsync(User.Identity!.Name!, documentId, cancellationToken);
                var ai = _services.GetRequiredService<IAiService>();
                var translated = await ai.ProcessAsync(
                    workspace?.Title ?? "Document",
                    new AiProcessRequestViewModel
                    {
                        DocumentId = documentId,
                        OperationType = "Translate",
                        TargetLanguage = targetLang,
                        InputText = plain,
                        ModelName = _configuration["Ai:Gemini:DefaultModel"] ?? "gemini-2.5-flash",
                        Style = "Formal",
                    },
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(translated.OutputText))
                    textToSpeak = translated.OutputText.Trim();
            }

            var ttsLangCode = MapLanguageToTtsCode(targetLang);
            var result = await _geminiTtsSpeech.SynthesizeAsync(textToSpeak, ttsLangCode, cancellationToken);
            var save = await _documentService.SaveNarrationResultAsync(
                User.Identity!.Name!,
                documentId,
                textToSpeak,
                result.AudioBytes,
                result.ContentType,
                cancellationToken);
            if (!save.Ok)
                return BadRequest(new { ok = false, message = save.ErrorMessage ?? "Audio recording could not be added to the notebook." });

            Response.Headers.CacheControl = "no-store";
            if (save.AiResultId.HasValue)
                Response.Headers["X-Ai-Result-Id"] = save.AiResultId.Value.ToString();
            if (!string.IsNullOrWhiteSpace(save.AudioUrl))
                Response.Headers["X-Audio-Url"] = save.AudioUrl;
            return File(result.AudioBytes, result.ContentType);
        }
        catch (Exception ex)
        {
            var shortMsg = ex.Message.Length > 400 ? ex.Message[..400] + "..." : ex.Message;
            return BadRequest(new { ok = false, message = $"Narration error: {shortMsg}" });
        }
    }

    /// <summary>
    /// [TR] Eski cache'lenmiş istemci yalnızca { text } gönderirse Details/{id} referer'ından belge kimliğini çıkarır.
    /// </summary>
    private Guid? TryGetDocumentIdFromReferer()
    {
        var referer = Request.Headers.Referer.ToString();
        if (string.IsNullOrWhiteSpace(referer) || !Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            return null;

        var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
        return Guid.TryParse(lastSegment, out var id) ? id : null;
    }

    /// <summary>Gemini TTS speechConfig.languageCode için UI dil adını BCP-47 koduna çevirir.</summary>
    private static string? MapLanguageToTtsCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return null;

        return language.Trim().ToLowerInvariant() switch
        {
            "english" => "en-US",
            "turkish" => "tr-TR",
            "german" => "de-DE",
            "french" => "fr-FR",
            "spanish" => "es-ES",
            "italian" => "it-IT",
            "portuguese" => "pt-BR",
            "russian" => "ru-RU",
            "arabic" => "ar-XA",
            "chinese" => "cmn-CN",
            "japanese" => "ja-JP",
            "korean" => "ko-KR",
            _ => null,
        };
    }

    /// <summary>Düzenlenmiş OCR metnini veritabanına kaydeder.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOcrText([FromBody] OcrSaveRequestViewModel request, CancellationToken cancellationToken)
    {
        if (request.OcrResultId == Guid.Empty)
            return BadRequest(new { ok = false, message = "OCR result ID is required." });

        var email = User.Identity!.Name!;
        var result = await _documentService.UpdateOcrTextAsync(email, request.OcrResultId, request.Text, cancellationToken);
        if (!result.Ok)
            return BadRequest(new { ok = false, message = result.ErrorMessage ?? "OCR metni kaydedilemedi." });

        return Json(new { ok = true, message = "OCR text saved." });
    }

    /// <summary>Belge ve dosyayı kaldırır.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var (ok, err) = await _documentService.DeleteAsync(email, id, cancellationToken);
        if (!ok)
            TempData["DocumentsError"] = err ?? "Could not be deleted.";
        else
            TempData["DocumentsMessage"] = "Document deleted.";

        return RedirectToAction(nameof(Index));
    }
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Workspace controller notu:
 * - Details artık çalışma alanı ViewModel’i döndürür.
 * - Pdf endpoint’i, yalnızca oturum sahibinin belge dosyasına erişim sağlar.
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte PDF sayfa küçük resim endpoint’leri eklenebilir.
 * - Çoklu seçim ve annotation kayıt API’leri ayrı action’lar olabilir.
 * - NarrateOcrSpeech: OCR metni → Gemini TTS; ApiKey için Ai:Gemini kullanıcı sırlarında tutulması önerilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * -----------------------------------------------------------------------------
 */
