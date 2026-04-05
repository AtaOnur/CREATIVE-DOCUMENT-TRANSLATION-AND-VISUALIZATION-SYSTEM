using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Belge listesi, yükleme (GET/POST), detay, silme — PDF MVP.
 * [TR] Neden gerekli: CreativeDoc çekirdeği; OCR/AI bu aşamada yok, sadece depolama ve meta veri.
 * [TR] İlgili: DocumentService, Views/Documents/*
 *
 * MODIFICATION NOTES (TR)
 * - Toplu silme, dışa aktarma.
 * - Belge önizleme ve PDF içi bölge seçimi sonraki modül (OCR yalnızca seçili bölge).
 * - Ocr:UseMock=false ayarıyla gerçek Tesseract OCR servisine geçiş desteklenir.
 * - Genel görüntü OCR future work.
 * - Zorluk: Orta.
 */
[Authorize]
public class DocumentsController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly IOcrService _ocrService;
    private readonly IConfiguration _configuration;

    public DocumentsController(IDocumentService documentService, IOcrService ocrService, IConfiguration configuration)
    {
        _documentService = documentService;
        _ocrService = ocrService;
        _configuration = configuration;
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
            ModelState.AddModelError(nameof(model.File), "Lütfen bir PDF dosyası seçin.");

        if (!ModelState.IsValid)
            return View(model);

        var email = User.Identity!.Name!;
        var (ok, err) = await _documentService.UploadAsync(email, model.File!, model.Title, cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, err ?? "Yükleme tamamlanamadı.");
            return View(model);
        }

        TempData["DocumentsMessage"] = "Belge başarıyla yüklendi.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Belge çalışma alanı (PDF viewer + seçim overlay + OCR placeholder).</summary>
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var model = await _documentService.GetWorkspaceAsync(email, id, cancellationToken);
        if (model == null)
            return NotFound();

        model.PdfEndpointUrl = Url.Action(nameof(Pdf), new { id }) ?? string.Empty;
        model.PagePreviewEndpointUrl = Url.Action(nameof(PagePreview), new { id }) ?? string.Empty;
        model.ExtractTextEndpointUrl = Url.Action(nameof(ExtractText)) ?? string.Empty;
        model.SaveOcrEndpointUrl = Url.Action(nameof(SaveOcrText)) ?? string.Empty;
        return View(model);
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
            return BadRequest(new { ok = false, message = "Belge kimliği zorunlu." });

        if (request.Region == null || request.Region.Width <= 0 || request.Region.Height <= 0)
            return BadRequest(new { ok = false, message = "Geçerli bir bölge seçimi gerekli." });

        var email = User.Identity!.Name!;
        var workspace = await _documentService.GetWorkspaceAsync(email, request.DocumentId, cancellationToken);
        if (workspace == null)
            return NotFound(new { ok = false, message = "Belge bulunamadı." });

        var pdfPath = await _documentService.GetPdfPhysicalPathAsync(email, request.DocumentId, cancellationToken);
        if (string.IsNullOrWhiteSpace(pdfPath))
            return BadRequest(new { ok = false, message = "OCR için PDF dosya yolu bulunamadı." });

        string text;
        try
        {
            // [TR] OCR servisine dil kodunu ilet; null ise appsettings varsayılanı kullanılır.
            text = await _ocrService.ExtractFromPdfRegionAsync(
                pdfPath, workspace.Title, request.Region,
                request.Language,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // [TR] OCR motoru hatası (Python bulunamadı, model indirilemedi, pdftoppm hatası vb.)
            // Kullanıcıya anlaşılır mesaj döner; detay logda kalır.
            var shortMsg = ex.Message.Length > 300 ? ex.Message[..300] + "..." : ex.Message;
            return BadRequest(new { ok = false, message = $"OCR hatası: {shortMsg}" });
        }

        var save = await _documentService.SaveOcrResultAsync(email, request.DocumentId, request.Region, text, cancellationToken);
        if (!save.Ok)
            return BadRequest(new { ok = false, message = save.ErrorMessage ?? "OCR sonucu kaydedilemedi." });

        return Json(new
        {
            ok = true,
            message = "OCR sonucu başarıyla üretildi.",
            ocrResultId = save.OcrResultId,
            text = save.ExtractedText ?? string.Empty,
        });
    }

    /// <summary>Düzenlenmiş OCR metnini veritabanına kaydeder.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOcrText([FromBody] OcrSaveRequestViewModel request, CancellationToken cancellationToken)
    {
        if (request.OcrResultId == Guid.Empty)
            return BadRequest(new { ok = false, message = "OCR sonuç kimliği zorunlu." });

        var email = User.Identity!.Name!;
        var result = await _documentService.UpdateOcrTextAsync(email, request.OcrResultId, request.Text, cancellationToken);
        if (!result.Ok)
            return BadRequest(new { ok = false, message = result.ErrorMessage ?? "OCR metni kaydedilemedi." });

        return Json(new { ok = true, message = "OCR metni kaydedildi." });
    }

    /// <summary>Belge ve dosyayı kaldırır.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity!.Name!;
        var (ok, err) = await _documentService.DeleteAsync(email, id, cancellationToken);
        if (!ok)
            TempData["DocumentsError"] = err ?? "Silinemedi.";
        else
            TempData["DocumentsMessage"] = "Belge silindi.";

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
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * -----------------------------------------------------------------------------
 */
