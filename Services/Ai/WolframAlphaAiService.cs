using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Wolfram Alpha Query API ile matematiksel sorgu, grafik çizimi ve sembolik sonuç alır.
 *      Kullanıcı "Math" işlemini seçip bu modeli seçtiğinde OCR/yönergedeki metin Wolfram'a
 *      gönderilir; API pods içinde plaintext ve görsel (grafik) döndürebilir.
 *
 * [TR] App ID: https://developer.wolframalpha.com/access/
 * [TR] Ücretsiz kota sınırlıdır; üretim için plan gerekebilir.
 */
public class WolframAlphaAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly WolframAlphaApiOptions _opts;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WolframAlphaAiService> _logger;

    public WolframAlphaAiService(
        HttpClient http,
        IOptions<AiOptions> aiOptions,
        IWebHostEnvironment env,
        ILogger<WolframAlphaAiService> logger)
    {
        _http = http;
        _opts = aiOptions.Value.WolframAlpha;
        _env = env;
        _logger = logger;

        var seconds = Math.Clamp(_opts.TimeoutSeconds, 10, 120);
        _http.Timeout = TimeSpan.FromSeconds(seconds);
    }

    public async Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.OperationType, "Math", StringComparison.OrdinalIgnoreCase))
        {
            return new AiServiceResult
            {
                OutputText =
                    "[Wolfram Alpha yalnızca Math işlemi ile kullanılır. İşlem listesinden Math seçin.]"
            };
        }

        if (string.IsNullOrWhiteSpace(_opts.AppId))
        {
            throw new InvalidOperationException(
                "Wolfram Alpha App ID yapılandırılmadı. appsettings.json → Ai:WolframAlpha:AppId " +
                "alanına anahtarınızı yazın (https://developer.wolframalpha.com/access/).");
        }

        var query = BuildWolframQuery(request);
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException(
                "Wolfram için boş sorgu. Denklem veya grafik isteğini OCR metnine yazın veya " +
                "\"Özel Yönerge\" alanına örn. plot sin(x) from -10 to 10 girin.");
        }

        var url =
            $"{_opts.BaseUrl.TrimEnd('/')}?input={Uri.EscapeDataString(query)}" +
            $"&appid={Uri.EscapeDataString(_opts.AppId.Trim())}&format=plaintext,image";

        _logger.LogInformation(
            "Wolfram Alpha sorgusu (Belge: {Doc}), uzunluk={Len}",
            documentTitle,
            query.Length);

        using var resp = await _http.GetAsync(url, cancellationToken);
        var xml = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Wolfram Alpha HTTP {Status}: {Snippet}",
                (int)resp.StatusCode,
                xml.Length > 400 ? xml[..400] + "…" : xml);
            throw new InvalidOperationException($"Wolfram Alpha HTTP hatası ({(int)resp.StatusCode}).");
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wolfram XML ayrıştırılamadı.");
            throw new InvalidOperationException("Wolfram Alpha yanıtı okunamadı.", ex);
        }

        var root = doc.Root;
        if (root == null)
            throw new InvalidOperationException("Wolfram Alpha boş yanıt döndürdü.");

        var ok = string.Equals(root.Attribute("success")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        if (!ok)
        {
            var hints = root.Descendants("didyoumean")
                .Select(e => e.Attribute("val")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Take(3)
                .ToList();

            var msg = hints.Count > 0
                ? $"Öneriler: {string.Join("; ", hints)}"
                : root.Attribute("error")?.Value ?? root.Attribute("errmsg")?.Value ?? "Sorgu başarısız.";
            throw new InvalidOperationException($"Wolfram Alpha: {msg}");
        }

        var textPieces = new List<string>();
        string? outputImageUrl = null;

        foreach (var pod in root.Elements("pod"))
        {
            var podTitle = pod.Attribute("title")?.Value ?? "";

            foreach (var sub in pod.Elements("subpod"))
            {
                var plain = sub.Element("plaintext")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    var line = string.IsNullOrWhiteSpace(podTitle)
                        ? plain
                        : $"[{podTitle}] {plain}";
                    textPieces.Add(line);
                }

                var imgSrc = sub.Element("img")?.Attribute("src")?.Value;
                if (!string.IsNullOrWhiteSpace(imgSrc) && outputImageUrl == null)
                {
                    try
                    {
                        outputImageUrl = await DownloadPlotImageAsync(imgSrc, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Wolfram grafik görseli indirilemedi.");
                    }
                }
            }
        }

        var distinctTexts = textPieces.Distinct(StringComparer.Ordinal).ToList();
        var outputText = distinctTexts.Count > 0
            ? string.Join("\n\n", distinctTexts)
            : outputImageUrl != null
                ? "Wolfram Alpha grafik görseli üretildi."
                : "Wolfram Alpha yanıt verdi ancak metin çıktısı yok.";

        return new AiServiceResult
        {
            OutputText = outputText,
            OutputImageUrl = outputImageUrl ?? string.Empty
        };
    }

    private static string BuildWolframQuery(AiProcessRequestViewModel request)
    {
        var instr = request.CustomInstruction?.Trim() ?? "";
        var text = request.InputText?.Trim() ?? "";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(instr))
            parts.Add(instr);
        if (!string.IsNullOrWhiteSpace(text))
            parts.Add(text);
        return string.Join(" ", parts).Trim();
    }

    private async Task<string?> DownloadPlotImageAsync(string src, CancellationToken ct)
    {
        using var imgResp = await _http.GetAsync(src, ct).ConfigureAwait(false);
        imgResp.EnsureSuccessStatusCode();
        var bytes = await imgResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

        var ext = "gif";
        var media = imgResp.Content.Headers.ContentType?.MediaType ?? "";
        if (media.Contains("png", StringComparison.OrdinalIgnoreCase))
            ext = "png";
        else if (media.Contains("jpeg", StringComparison.OrdinalIgnoreCase))
            ext = "jpg";

        var fileName = $"{Guid.NewGuid():N}.{ext}";
        var dir = Path.Combine(_env.WebRootPath, "ai-images");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);

        _logger.LogInformation("Wolfram görseli kaydedildi: {Path}", path);
        return $"/ai-images/{fileName}";
    }
}
