using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services.Ocr;

/*
 * [TR] Bu dosya ne işe yarar: PaddleOCR (Python tabanlı) kullanarak seçili PDF bölgesinden metin çıkarır.
 * [TR] Neden gerekli: Tesseract'a kıyasla PaddleOCR, özellikle el yazısı ve karmaşık düzenler için
 *      daha yüksek doğruluk sunar. Türkçe dahil çok dilli destek sağlar.
 * [TR] Çalışma akışı:
 *      1. pdftoppm (Poppler) ile PDF sayfasını PNG'ye dönüştürür.
 *      2. System.Drawing ile seçili bölgeyi kırpar (crop).
 *      3. Scripts/paddle_ocr.py scriptini Python ile çalıştırır.
 *      4. Stdout'tan düz metin alır.
 * [TR] İlgili: IOcrService, PaddleOcrOptions, appsettings.json (Ocr bölümü)
 *
 * MODIFICATION NOTES (TR)
 * - Türkçe için lang="turkish" kullanılır (Latin tabanlı PaddleOCR modeli).
 * - GPU desteği için paddle_ocr.py içinde use_gpu=True yapılabilir (CUDA gerekir).
 * - Tam sayfa OCR için region parametresi None geçilerek crop adımı atlanabilir.
 * - Çoklu bölge desteği için metot imzası List<RegionSelectionViewModel> alacak şekilde genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Orta.
 */
[SupportedOSPlatform("windows")]
public class PaddleOcrService : IOcrService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<PaddleOcrService> _logger;

    public PaddleOcrService(
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<PaddleOcrService> logger)
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
        var pythonPath   = ocrCfg["PaddlePythonPath"] ?? "python";
        // [TR] Dil parametresi artık kullanıcıdan alınmıyor; script kendi varsayılan
        //      modelini (en/Latin) kullanır. language parametresi interface uyumu için tutuldu.
        var dpi = int.TryParse(ocrCfg["PdfRasterDpi"], out var d) ? d : 220;

        // [TR] Python script'inin konumunu proje kök dizinine göre belirle.
        var scriptPath = Path.Combine(_env.ContentRootPath, "Scripts", "paddle_ocr.py");
        if (!File.Exists(scriptPath))
            throw new InvalidOperationException($"paddle_ocr.py scripti bulunamadı: {scriptPath}");

        var tempRoot = Path.Combine(_env.ContentRootPath, "Data", "tmp-ocr");
        Directory.CreateDirectory(tempRoot);

        var key      = Guid.NewGuid().ToString("N");
        var rasterBase = Path.Combine(tempRoot, $"pocr_{key}_page");
        var rasterPng  = $"{rasterBase}.png";
        var cropPng    = Path.Combine(tempRoot, $"pocr_{key}_crop.png");

        try
        {
            // [TR] Adım 1: PDF sayfasını yüksek DPI'da PNG'ye dönüştür.
            var page = Math.Max(1, region.PageNumber);
            await RunProcessAsync(
                pdftoppmPath,
                $"-f {page} -singlefile -png -r {dpi} \"{pdfFilePath}\" \"{rasterBase}\"",
                cancellationToken);

            if (!File.Exists(rasterPng))
                throw new InvalidOperationException("PDF sayfası görüntüye dönüştürülemedi (pdftoppm çıktısı yok).");

            // [TR] Adım 2: Kullanıcının seçtiği bölgeyi kırp.
            CropRegion(rasterPng, cropPng, region);

            // [TR] Adım 3: PaddleOCR Python scripti çalıştır ve stdout'u al.
            // Dil argümanı artık gönderilmiyor; script varsayılan modeli kullanır.
            var (stdout, stderr, exitCode) = await RunProcessWithOutputAsync(
                pythonPath,
                $"\"{scriptPath}\" \"{cropPng}\"",
                cancellationToken);

            if (exitCode == 2)
                throw new InvalidOperationException(
                    "PaddleOCR kurulu değil. Kurulum:\n  pip install paddleocr paddlepaddle");

            if (exitCode != 0)
            {
                _logger.LogError("PaddleOCR script hatası. ExitCode={Code} STDERR={Err}", exitCode, stderr);
                throw new InvalidOperationException($"PaddleOCR script hatası (exit {exitCode}): {stderr?.Trim()}");
            }

            var text = stdout?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                text = "[PaddleOCR] Seçili bölgede metin tespit edilemedi.";

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PaddleOCR hatası. Doküman: {Doc}", documentTitle);
            throw;
        }
        finally
        {
            // [TR] Geçici dosyaları temizle; hata olursa yok say.
            TryDelete(rasterPng);
            TryDelete(cropPng);
        }
    }

    /// <summary>
    /// [TR] Normalize (0-1) veya piksel koordinatlara göre bölge kırpma işlemi.
    /// System.Drawing.Common Windows'a özgüdür; [SupportedOSPlatform("windows")] ile işaretlendi.
    /// </summary>
    private static void CropRegion(string sourcePng, string destPng, RegionSelectionViewModel region)
    {
        using var src = System.Drawing.Image.FromFile(sourcePng);
        using var bmp = new System.Drawing.Bitmap(src);

        int x, y, w, h;

        // [TR] Region değerleri 0-1 aralığında ise normalize kabul et; aksi halde piksel.
        if (region.X <= 1 && region.Y <= 1 && region.Width <= 1 && region.Height <= 1)
        {
            x = (int)Math.Round(region.X * bmp.Width);
            y = (int)Math.Round(region.Y * bmp.Height);
            w = (int)Math.Round(region.Width  * bmp.Width);
            h = (int)Math.Round(region.Height * bmp.Height);
        }
        else
        {
            x = (int)Math.Round(region.X);
            y = (int)Math.Round(region.Y);
            w = (int)Math.Round(region.Width);
            h = (int)Math.Round(region.Height);
        }

        x = Math.Clamp(x, 0, Math.Max(0, bmp.Width  - 1));
        y = Math.Clamp(y, 0, Math.Max(0, bmp.Height - 1));
        w = Math.Clamp(w, 1, bmp.Width  - x);
        h = Math.Clamp(h, 1, bmp.Height - y);

        using var cropped = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(cropped))
        {
            g.DrawImage(bmp,
                new System.Drawing.Rectangle(0, 0, w, h),
                new System.Drawing.Rectangle(x, y, w, h),
                System.Drawing.GraphicsUnit.Pixel);
        }
        cropped.Save(destPng, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>
    /// [TR] Sadece başarı/başarısızlık kontrolü için CLI süreci çalıştırır (pdftoppm için).
    /// </summary>
    private static async Task RunProcessAsync(string fileName, string args, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName  = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow  = true,
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
            }
        };
        process.Start();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture,
                    "Komut başarısız: {0} | ExitCode={1} | STDERR={2} | STDOUT={3}",
                    fileName, process.ExitCode, stderr, stdout));
        }
    }

    /// <summary>
    /// [TR] Stdout ve stderr'i birlikte yakalayarak döndürür (PaddleOCR Python script için).
    /// </summary>
    private static async Task<(string? stdout, string? stderr, int exitCode)>
        RunProcessWithOutputAsync(string fileName, string args, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName  = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow  = true,
                RedirectStandardError  = true,
                RedirectStandardOutput = true,
            }
        };
        process.Start();

        // [TR] Stdout ve stderr'i paralel oku; deadlock'u önlemek için eş zamanlı.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (stdout, stderr, process.ExitCode);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
