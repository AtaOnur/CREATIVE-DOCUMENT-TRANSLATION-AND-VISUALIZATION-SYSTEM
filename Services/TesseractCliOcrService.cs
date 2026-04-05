using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Gerçek OCR için Tesseract CLI + Poppler (pdftoppm) kullanan servis.
 * [TR] Neden gerekli: API key olmadan lokal OCR ile seçili PDF bölgesinden metin çıkarımı yapmak.
 * [TR] İlgili: IOcrService, DocumentsController.ExtractText, appsettings.json (Ocr ayarları)
 *
 * MODIFICATION NOTES (TR)
 * - OCR kalite artırımı için preprocess (binarize, deskew) adımı eklenebilir.
 * - Çoklu bölge OCR için tek çağrıda dizi desteği eklenebilir.
 * - Tam sayfa OCR seçeneği eklenebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Orta.
 */
[SupportedOSPlatform("windows")]
public class TesseractCliOcrService : IOcrService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<TesseractCliOcrService> _logger;

    public TesseractCliOcrService(
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<TesseractCliOcrService> logger)
    {
        _env = env;
        _config = config;
        _logger = logger;
    }

    public async Task<string> ExtractFromPdfRegionAsync(
        string pdfFilePath,
        string documentTitle,
        RegionSelectionViewModel region,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfFilePath) || !File.Exists(pdfFilePath))
            throw new InvalidOperationException("OCR için PDF dosya yolu bulunamadı.");

        var ocrCfg = _config.GetSection("Ocr");
        var pdftoppmPath = ocrCfg["PdfToPpmPath"] ?? "pdftoppm";
        var tesseractPath = ocrCfg["TesseractPath"] ?? "tesseract";
        // [TR] Dil parametresi: kullanıcı UI'dan geçirdiyse onu kullan; yoksa appsettings varsayılanı.
        var effectiveLang = !string.IsNullOrWhiteSpace(language) ? language : (ocrCfg["Language"] ?? "tur+eng");
        var dpi = int.TryParse(ocrCfg["PdfRasterDpi"], out var parsedDpi) ? parsedDpi : 220;
        var tempRoot = Path.Combine(_env.ContentRootPath, "Data", "tmp-ocr");
        Directory.CreateDirectory(tempRoot);

        var key = Guid.NewGuid().ToString("N");
        var rasterBase = Path.Combine(tempRoot, $"ocr_{key}_page");
        var rasterPng = $"{rasterBase}.png";
        var cropPng = Path.Combine(tempRoot, $"ocr_{key}_crop.png");
        var outBase = Path.Combine(tempRoot, $"ocr_{key}_out");
        var outTxt = $"{outBase}.txt";

        try
        {
            var page = Math.Max(1, region.PageNumber);

            await RunProcessAsync(
                pdftoppmPath,
                $"-f {page} -singlefile -png -r {dpi} \"{pdfFilePath}\" \"{rasterBase}\"",
                cancellationToken);

            if (!File.Exists(rasterPng))
                throw new InvalidOperationException("PDF sayfası görüntüye dönüştürülemedi.");

            CropRegion(rasterPng, cropPng, region);

            await RunProcessAsync(
                tesseractPath,
                $"\"{cropPng}\" \"{outBase}\" -l {effectiveLang}",
                cancellationToken);

            if (!File.Exists(outTxt))
                throw new InvalidOperationException("Tesseract çıktı dosyası üretilemedi.");

            var text = await File.ReadAllTextAsync(outTxt, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
                text = "[Tesseract OCR] Metin tespit edilemedi veya bölge boş.";

            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract OCR hatası. Doküman: {Document}", documentTitle);
            throw;
        }
        finally
        {
            TryDelete(rasterPng);
            TryDelete(cropPng);
            TryDelete(outTxt);
        }
    }

    /// <summary>
    /// [TR] Seçili bölge koordinatına göre sayfa görüntüsünden crop alır.
    /// </summary>
    private static void CropRegion(string sourcePng, string destPng, RegionSelectionViewModel region)
    {
        using var src = System.Drawing.Image.FromFile(sourcePng);
        using var bmp = new System.Drawing.Bitmap(src);

        int x;
        int y;
        int w;
        int h;

        // [TR] Region değerleri 0-1 ise normalize kabul edilir; aksi halde piksel olarak yorumlanır.
        if (region.X <= 1 && region.Y <= 1 && region.Width <= 1 && region.Height <= 1)
        {
            x = (int)Math.Round(region.X * bmp.Width);
            y = (int)Math.Round(region.Y * bmp.Height);
            w = (int)Math.Round(region.Width * bmp.Width);
            h = (int)Math.Round(region.Height * bmp.Height);
        }
        else
        {
            x = (int)Math.Round(region.X);
            y = (int)Math.Round(region.Y);
            w = (int)Math.Round(region.Width);
            h = (int)Math.Round(region.Height);
        }

        x = Math.Clamp(x, 0, Math.Max(0, bmp.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, bmp.Height - 1));
        w = Math.Clamp(w, 1, bmp.Width - x);
        h = Math.Clamp(h, 1, bmp.Height - y);

        using var cropped = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(cropped))
        {
            g.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, w, h), new System.Drawing.Rectangle(x, y, w, h), System.Drawing.GraphicsUnit.Pixel);
        }
        cropped.Save(destPng, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>
    /// [TR] CLI komutlarını güvenli şekilde çalıştırır ve hata kodunda exception fırlatır.
    /// </summary>
    private static async Task RunProcessAsync(string fileName, string args, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var msg = string.Format(
                CultureInfo.InvariantCulture,
                "Komut basarisiz: {0} {1} | ExitCode={2} | STDERR={3} | STDOUT={4}",
                fileName, args, process.ExitCode, stderr, stdout);
            throw new InvalidOperationException(msg);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore temp cleanup failure
        }
    }
}

