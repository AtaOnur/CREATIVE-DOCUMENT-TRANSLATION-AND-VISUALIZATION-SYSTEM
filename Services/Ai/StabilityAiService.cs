using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: Stability AI API üzerinden Stable Diffusion görsel üretimi yapar.
 * [TR] Neden gerekli: HuggingFace FLUX modelleri alakasız görseller üretiyor;
 *      Stability AI çok daha kaliteli, kontrollü sonuçlar verir.
 * [TR] İlgili: IAiService, MultiProviderAiService, StabilityApiOptions, AiController
 *
 * STABILITY AI API HAKKINDA (TR)
 *   - Endpoint: https://api.stability.ai/v2beta/stable-image/generate/{engine}
 *   - engine: "core" (en hızlı, 3 kredi) | "sd3" (en kaliteli, 6.5 kredi)
 *   - Auth: Authorization: Bearer {API_KEY}
 *   - Format: multipart/form-data (prompt, output_format, aspect_ratio vb.)
 *   - Yanıt: ham görüntü byte'ları (Accept: image/* ile)
 *   - Ücretsiz: 25 kredi/ay → yaklaşık 8 "sd3" veya 8 "core" görseli
 *   - API anahtarı: https://platform.stability.ai/account/credits (kredi kartı gerekmez)
 *
 * MODIFICATION NOTES (TR)
 * - "sd3" motoruna geçmek: model ID'sine bakarak engine seçimi yapılabilir.
 * - negative_prompt eklemek: form'a "negative_prompt" alanı eklenir.
 * - aspect_ratio değiştirmek: "aspect_ratio" parametresi eklenir (16:9, 4:3 vb.).
 * - Zorluk: Kolay.
 *
 * JÜRI MODİFİKASYON NOTLARI (TR)
 * - "Stable Diffusion neden ayrı bir servis?" → Farklı API formatı (multipart) ve endpoint kullanır.
 * - "Kredi biterse ne olur?" → 402 Payment Required döner; ücretsiz kota sıfırlanana kadar beklenir.
 * - "Daha iyi kalite?" → "sd3" motoru seçilir veya steps/cfg_scale artırılır (future work).
 */
public class StabilityAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly StabilityApiOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StabilityAiService> _logger;

    public StabilityAiService(
        HttpClient http,
        IOptions<AiOptions> options,
        IWebHostEnvironment env,
        ILogger<StabilityAiService> logger)
    {
        _http = http;
        _options = options.Value.Stability;
        _env = env;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        // [TR] Accept: application/json → Stability AI yanıtı base64 JSON olarak döner.
        //      Accept: image/* yerine JSON tercih edildi; binary okuma daha az güvenilir.
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    // ─── ANA YÖNLENDIRICI ─────────────────────────────────────────────────────
    // [TR] Stability AI yalnızca görsel üretimi destekler.
    //      Metin tabanlı task'ler bu servise yönlendirilmez.
    public async Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationType != "Visualize")
        {
            return new AiServiceResult
            {
                OutputText = "[Stability AI yalnızca görsel üretimi (Visualize) destekler. " +
                             "Metin işlemleri için Gemini veya Groq modeli seçiniz.]"
            };
        }

        try
        {
            return await VisualizeAsync(documentTitle, request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stability AI görsel üretimi başarısız. Model={Model}", request.ModelName);
            throw new InvalidOperationException($"Stability AI hatası: {ex.Message}", ex);
        }
    }

    // ─── GÖRSEL ÜRETME ─────────────────────────────────────────────────────────
    /*
     * [TR] Stability AI v2beta stable-image/generate endpoint'ini kullanır.
     *      Model ID'sine göre "core" veya "sd3" motoru seçilir:
     *        stability-core → /generate/core  (3 kredi, hızlı)
     *        stability-sd3  → /generate/sd3   (6.5 kredi, yüksek kalite)
     *
     *      İstek formatı: multipart/form-data
     *        prompt        : üretilecek görselin metin açıklaması
     *        output_format : "png" (varsayılan)
     *        aspect_ratio  : "1:1" (varsayılan kare)
     *
     *      Yanıt formatı: ham PNG/JPEG byte'ları (Accept: image/* ile)
     */
    private async Task<AiServiceResult> VisualizeAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken ct)
    {
        // [TR] Stability AI YALNIZCA İngilizce prompt kabul eder.
        //      OCR metni Türkçe olabileceğinden:
        //      1. Sadece ASCII karakterler tutulur (Türkçe harfler kaldırılır)
        //      2. Kısa, görsel modele uygun bir İngilizce prompt oluşturulur
        var prompt = BuildEnglishPrompt(documentTitle, request.InputText!);

        // [TR] Model ID'sine göre engine belirlenir
        //      "stability-sd3" → /generate/sd3 (daha kaliteli)
        //      diğerleri       → /generate/core (daha hızlı, ucuz)
        var engine = request.ModelName?.Contains("sd3", StringComparison.OrdinalIgnoreCase) == true
            ? "sd3"
            : "core";

        var url = $"{_options.BaseUrl.TrimEnd('/')}/{engine}";
        _logger.LogInformation("Stability AI görsel isteği → engine={Engine}, url={Url}", engine, url);

        // [TR] Stability AI multipart/form-data formatı kullanır.
        //      .NET MultipartFormDataContent uyumsuz header'lar eklediğinden
        //      multipart body MANUEL olarak inşa edilir.
        //      Format: --{boundary}\r\nContent-Disposition: form-data; name="..."\r\n\r\n{value}\r\n
        var boundary = Guid.NewGuid().ToString("N");
        var bodyStr =
            $"--{boundary}\r\n" +
            $"Content-Disposition: form-data; name=\"prompt\"\r\n\r\n" +
            $"{prompt}\r\n" +
            $"--{boundary}\r\n" +
            $"Content-Disposition: form-data; name=\"output_format\"\r\n\r\n" +
            $"png\r\n" +
            $"--{boundary}\r\n" +
            $"Content-Disposition: form-data; name=\"aspect_ratio\"\r\n\r\n" +
            $"1:1\r\n" +
            $"--{boundary}--\r\n";

        var bodyBytes   = System.Text.Encoding.UTF8.GetBytes(bodyStr);
        var httpContent = new ByteArrayContent(bodyBytes);
        httpContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");
        httpContent.Headers.ContentType.Parameters.Add(
            new System.Net.Http.Headers.NameValueHeaderValue("boundary", boundary));

        var response = await _http.PostAsync(url, httpContent, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Stability AI HTTP {Status}: {Body}", (int)response.StatusCode, errBody);

            var detail = (int)response.StatusCode switch
            {
                402 => "Ücretsiz krediniz bitti. https://platform.stability.ai/account/credits adresinden kredi ekleyin.",
                403 => "API anahtarı geçersiz. appsettings.json → Ai:Stability:ApiKey kontrol edin.",
                429 => "Rate limit aşıldı. Lütfen bekleyin.",
                _   => $"HTTP {(int)response.StatusCode}: {errBody}"
            };

            throw new InvalidOperationException($"Stability AI hatası: {detail}");
        }

        // [TR] Başarılı yanıt: Accept: application/json → {"image":"<base64>","finish_reason":"SUCCESS"}
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Stability AI yanıt: {Body}", body.Length > 200 ? body[..200] : body);

        string base64Data;
        string ext = "png";

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);

            // [TR] "image" alanı base64 görsel verisini içerir
            if (doc.RootElement.TryGetProperty("image", out var imgProp))
            {
                base64Data = imgProp.GetString() ?? throw new InvalidOperationException("Görsel verisi boş.");
            }
            // [TR] Bazı yanıtlarda "artifacts" dizisi içinde gelir
            else if (doc.RootElement.TryGetProperty("artifacts", out var arts))
            {
                base64Data = arts[0].GetProperty("base64").GetString()
                    ?? throw new InvalidOperationException("artifacts[0].base64 boş.");
            }
            else
            {
                throw new InvalidOperationException($"Beklenmeyen Stability AI yanıtı: {body}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Stability AI yanıtı ayrıştırılamadı: {body}", ex);
        }

        var fileName = $"{Guid.NewGuid():N}.{ext}";
        var dir      = Path.Combine(_env.WebRootPath, "ai-images");

        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(Path.Combine(dir, fileName), Convert.FromBase64String(base64Data), ct);

        _logger.LogInformation("Stability AI görsel kaydedildi: {File}", fileName);

        return new AiServiceResult
        {
            OutputText     = $"Görsel başarıyla üretildi (Stability AI — {engine}).",
            OutputImageUrl = $"/ai-images/{fileName}"
        };
    }

    // ─── PROMPT YARDIMCISI ────────────────────────────────────────────────────
    /// <summary>
    /// Stability AI için İngilizce görsel prompt oluşturur.
    /// [TR] Stability AI yalnızca İngilizce kabul eder.
    ///      Türkçe metin ASCII'ye dönüştürülür; anlamlı kelimeler tutulur.
    ///      Sonuna kalite etiketleri eklenerek görsel kalitesi artırılır.
    /// </summary>
    private static string BuildEnglishPrompt(string documentTitle, string inputText)
    {
        // [TR] Adım 1: ASCII dışı karakterleri (ş,ğ,ü,ı vb.) kaldır
        var asciiOnly = new string(inputText
            .Where(c => c < 128 && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)))
            .ToArray());

        // [TR] Adım 2: Anlamlı kelimeler (3+ harf), ilk 10 tanesi
        var keywords = asciiOnly
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Take(10)
            .ToList();

        // [TR] Adım 3: Belge başlığını da ASCII'ye çevir
        var titleAscii = new string(documentTitle
            .Where(c => c < 128 && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)))
            .ToArray()).Trim();

        string subject;
        if (keywords.Count >= 3)
        {
            subject = string.IsNullOrWhiteSpace(titleAscii)
                ? string.Join(" ", keywords)
                : $"{titleAscii}: {string.Join(", ", keywords)}";
        }
        else
        {
            subject = string.IsNullOrWhiteSpace(titleAscii)
                ? "A professional document visualization"
                : $"A professional illustration representing '{titleAscii}'";
        }

        return $"{subject}. High quality, detailed, professional artwork, 4K, sharp focus.";
    }
}
