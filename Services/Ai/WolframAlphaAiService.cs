using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
 *
 * [TR] OCR tablosu (öğrenci + notlar vb.) doğrudan NLP ile sıklıkla başarısız olur; satırlardan sayı
 *      matrisi çıkarılıp özel yönergeye göre Mean[] / Total[] ile Wolfram uyumlu ifade üretilir.
 * [TR] İsteğe bağlı: Gemini ile OCR+yönerge tek satır Wolfram diline çevrilir (UseGeminiQueryPlanner).
 * [TR] Wolfram Alpha Query API çoğu zaman Mathematica Apply (@), Flatten vb. kabul etmez (success=false,
 *      HTTP yine 200 döner); çözülen sayısal not tablolarında ortalama/toplam yerelde hesaplanır.
 * [TR] Öğrenci ortalamalarının histogram / çan eğrisi istekleri yerelde SVG olarak çizilir (Wolfram grafik sıklıkla başarısız).
 */
public class WolframAlphaAiService : IAiService
{
    private sealed record GradeTableRow(string Label, List<double> Values);

    private readonly HttpClient _http;
    private readonly WolframAlphaApiOptions _opts;
    private readonly GeminiAiService _gemini;
    private readonly GeminiAiOptions _geminiOpts;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WolframAlphaAiService> _logger;

    public WolframAlphaAiService(
        HttpClient http,
        IOptions<AiOptions> aiOptions,
        IOptions<GeminiAiOptions> geminiOptions,
        GeminiAiService gemini,
        IWebHostEnvironment env,
        ILogger<WolframAlphaAiService> logger)
    {
        _http = http;
        _opts = aiOptions.Value.WolframAlpha;
        _geminiOpts = geminiOptions.Value;
        _gemini = gemini;
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

        var instr = request.CustomInstruction?.Trim() ?? "";
        var text = request.InputText?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(text) && ContainsLineBreak(text) &&
            TryParseNumericGradeTable(text, out var parsedTable))
        {
            // Histogram / bell curve — Wolfram ile güvenilir değil; tablodan satır ortalamaları çıkarılıp SVG üretilir.
            if (InstructionRequestsStudentAverageHistogram(instr))
            {
                var svgUrl = await SaveStudentAverageHistogramSvgAsync(documentTitle, parsedTable, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation(
                    "Öğrenci ortalama histogramı yerelde SVG olarak kaydedildi (öğrenci sayısı={N}).",
                    parsedTable.Count);
                return new AiServiceResult
                {
                    OutputText = FormatStudentAverageHistogramCaption(parsedTable),
                    OutputImageUrl = svgUrl
                };
            }

            // Wolfram Alpha genelde Mean /@ ve Mean[Flatten[...]] gibi WL kısayollarında success=false döner (HTTP 200 olsa bile).
            // Tabloyu zaten çıkardıysak istatistikleri yerelde hesaplamak güvenilir.
            if (!InstructionRequestsWolframPlot(instr))
            {
                var intent = ClassifyGridIntent(instr);
                _logger.LogInformation(
                    "Not tablosu yerelde hesaplandı (satır={Rows}, sütun={Cols}, niyet={Intent}).",
                    parsedTable.Count,
                    parsedTable[0].Values.Count,
                    intent);
                return new AiServiceResult
                {
                    OutputText = FormatGridStatisticsLocally(parsedTable, intent),
                    OutputImageUrl = string.Empty
                };
            }
        }

        var query = await ResolveWolframQueryAsync(request, cancellationToken).ConfigureAwait(false);
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

            var rawErr = root.Attribute("error")?.Value ?? root.Attribute("errmsg")?.Value;
            var trivialErr = string.IsNullOrWhiteSpace(rawErr) ||
                             string.Equals(rawErr, "false", StringComparison.OrdinalIgnoreCase);

            string msg;
            if (hints.Count > 0)
                msg = $"Öneriler: {string.Join("; ", hints)}";
            else if (!trivialErr)
                msg = rawErr!;
            else
                msg = "Sorgu başarısız (Wolfram sonuç üretemedi). Karmaşık tablo kullanıyorsanız Ai:WolframAlpha:UseGeminiQueryPlanner " +
                      "açık olsun ve Gemini API anahtarının dolu olduğundan emin olun.";

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

    private async Task<string> ResolveWolframQueryAsync(
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken)
    {
        var instr = request.CustomInstruction?.Trim() ?? "";
        var text = request.InputText?.Trim() ?? "";

        if (_opts.UseGeminiQueryPlanner &&
            !string.IsNullOrWhiteSpace(_geminiOpts.ApiKey) &&
            (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(instr)))
        {
            try
            {
                var planned = await TryPlanQueryWithGeminiAsync(instr, text, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(planned))
                {
                    _logger.LogInformation(
                        "Wolfram sorgusu Gemini planlayıcı ile üretildi (uzunluk={Len}).",
                        planned.Length);
                    return planned;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini Wolfram planlayıcı başarısız; yerel düzenleyici kullanılacak.");
            }
        }

        return BuildWolframQuery(request);
    }

    private async Task<string?> TryPlanQueryWithGeminiAsync(
        string instruction,
        string rawOcrText,
        CancellationToken cancellationToken)
    {
        const int maxChars = 14000;
        var excerpt = rawOcrText;
        if (excerpt.Length > maxChars)
            excerpt = excerpt[..maxChars] + "\n... [truncated]";

        var prompt = BuildGeminiWolframPlannerPrompt(instruction, excerpt);

        var modelOverride = string.IsNullOrWhiteSpace(_opts.GeminiPlannerModel)
            ? null
            : _opts.GeminiPlannerModel.Trim();

        var raw = await _gemini.GenerateSimpleTextAsync(modelOverride, prompt, cancellationToken)
            .ConfigureAwait(false);

        var cleaned = SanitizeGeminiPlannerOutput(raw);
        if (cleaned.Length < 2 || cleaned.Length > 6000)
            return null;

        if (LooksLikePlannerRefusal(cleaned))
            return null;

        return cleaned;
    }

    private static bool LooksLikePlannerRefusal(string s)
    {
        var lower = s.ToLowerInvariant();
        return lower.StartsWith("i cannot", StringComparison.Ordinal) ||
               lower.StartsWith("i can't", StringComparison.Ordinal) ||
               lower.StartsWith("i'm sorry", StringComparison.Ordinal) ||
               lower.StartsWith("sorry,", StringComparison.Ordinal) ||
               lower.Contains("cannot fulfill", StringComparison.Ordinal);
    }

    private static string BuildGeminiWolframPlannerPrompt(string instruction, string rawText)
    {
        return $"""
You translate messy OCR / pasted tables into ONE Wolfram Alpha input line.

USER GOAL (any language):
{instruction}

RAW TEXT (may include headers, student IDs, names, letter grades, uneven spacing):
{rawText}

RULES:
1. Output ONLY the Wolfram Alpha query string. No markdown fences. No quotes. No commentary before or after.
2. Prefer Wolfram Language that Wolfram Alpha understands: Mean[list], Total[list], Mean /@ matrix for per-row averages, Mean[matrix] for per-column averages of a numeric matrix.
3. MULTIPLE STUDENTS / MULTIPLE ROWS OF NUMBERS: build only the numeric matrix (same column count each row). For ONE overall average across ALL numeric grades use Mean[Flatten[matrix]] NOT a huge comma-separated list. For per-student averages use Mean /@ matrix.
4. CLASS-WIDE AVERAGE OF ALL NUMERIC GRADES: same as flattening every numeric cell — Mean[Flatten[matrix]] when you have a rectangular grade matrix.
5. ONE STUDENT ROW: Mean[list] over that row's numeric scores only.
6. Skip IDs (e.g. 21COMP1016), skip letter grades (AA) unless requested; decimals use '.' only.

OUTPUT (single line):
""";
    }

    private static string SanitizeGeminiPlannerOutput(string raw)
    {
        var s = raw.Trim();

        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var openEnd = s.IndexOf('\n');
            if (openEnd >= 0)
                s = s[(openEnd + 1)..].TrimStart();
            var fence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
                s = s[..fence].TrimEnd();
        }

        var nl = s.AsSpan().IndexOfAny('\r', '\n');
        if (nl >= 0)
            s = s[..nl].Trim();

        return s.Trim().Trim('"').Trim('\'');
    }

    private static bool InstructionRequestsWolframPlot(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return false;

        return Regex.IsMatch(
            instruction,
            @"\b(plot|graph|chart|histogram|diagram|scatter|grafik|çiz(?:im)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Öğrenci başına ortalamaların histogram / çan eğrisi / dağılım grafikleri — yerel SVG ile karşılanır.
    /// </summary>
    private static bool InstructionRequestsStudentAverageHistogram(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return false;

        var s = instruction.ToLowerInvariant();

        var distCue = Regex.IsMatch(
            s,
            @"\b(histogram|histograms|bell\s*-?\s*curve|bellcurve|bell\s+curve|normal\s+distribution|gaussian|gauss|çan\s*eğri|çan\s+eğrisi|frequency\s+distribution|density\s+plot|dağılım\s+grafik|dağılım)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!distCue)
            return false;

        var studentAvgCue = Regex.IsMatch(
            s,
            @"\b(student\s+averages?|each\s+student|per\s+student|student'?s\s+average|students'?|row\s+averages?|öğrenci\s+ortalaması|öğrenci\s+ortalamaları|satır\s+ortalaması|satır\s+ortalamaları)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                         || Regex.IsMatch(s, @"öğrenci\s+ortalama", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var gradeCue = Regex.IsMatch(
            s,
            @"\b(average\s+grade|grades?\s+distribution|ortalama\s+dağılımı)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var plotCue = Regex.IsMatch(
            s,
            @"\b(plot|chart|graph|grafik|çiz)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return studentAvgCue || gradeCue || plotCue;
    }

    private static string FormatStudentAverageHistogramCaption(List<GradeTableRow> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        var avgs = rows.Select(r => r.Values.Average()).ToList();
        var listBlock = FormatGridStatisticsLocally(rows, WolframGridIntent.MeanEachRow);
        var mu = avgs.Average();
        var sd = SampleStdDev(avgs);

        return listBlock +
               "\n\n---\n" +
               $"Öğrenci ortalamaları özeti — n={avgs.Count}, min={avgs.Min().ToString("F2", inv)}, max={avgs.Max().ToString("F2", inv)}, " +
               $"ortalama={mu.ToString("F2", inv)}, örneklem s.sapması={sd.ToString("F2", inv)}\n" +
               "Grafik: mavi çubuklar = histogram; mor çizgi = aynı ortalama/sapma ile teorik normal yoğunluk (referans).";
    }

    private static double SampleStdDev(IReadOnlyList<double> xs)
    {
        if (xs.Count < 2)
            return 0;
        var m = xs.Average();
        var v = xs.Sum(x => (x - m) * (x - m)) / (xs.Count - 1);
        return Math.Sqrt(v);
    }

    private static double NormalPdf(double x, double mu, double sigma)
    {
        if (sigma <= 1e-12)
            return 0;
        var z = (x - mu) / sigma;
        return Math.Exp(-0.5 * z * z) / (sigma * Math.Sqrt(2 * Math.PI));
    }

    private async Task<string> SaveStudentAverageHistogramSvgAsync(
        string documentTitle,
        List<GradeTableRow> table,
        CancellationToken ct)
    {
        var avgs = table.Select(r => r.Values.Average()).ToList();
        var svg = BuildStudentAverageHistogramSvg(documentTitle, avgs);

        var fileName = $"{Guid.NewGuid():N}.svg";
        var dir = Path.Combine(_env.WebRootPath, "ai-images");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(path, svg, Encoding.UTF8, ct).ConfigureAwait(false);

        _logger.LogInformation("Histogram SVG yazıldı: {Path}", path);
        return $"/ai-images/{fileName}";
    }

    private static string BuildStudentAverageHistogramSvg(string documentTitle, List<double> rowAverages)
    {
        const int W = 780;
        const int H = 460;
        const int marginL = 76;
        const int marginR = 36;
        const int marginT = 56;
        const int marginB = 88;
        var plotW = W - marginL - marginR;
        var plotH = H - marginT - marginB;
        var inv = CultureInfo.InvariantCulture;

        var n = rowAverages.Count;
        var binCount = (int)Math.Clamp(Math.Round(1 + Math.Log2(Math.Max(n, 2))), 5, 14);

        var min = rowAverages.Min();
        var max = rowAverages.Max();
        if (Math.Abs(max - min) < 1e-9)
        {
            min -= 1;
            max += 1;
        }

        var span = max - min;
        var pad = span * 0.06;
        min -= pad;
        max += pad;
        span = max - min;

        var counts = new int[binCount];
        foreach (var a in rowAverages)
        {
            var idx = (int)Math.Floor((a - min) / (span / binCount));
            if (idx < 0)
                idx = 0;
            if (idx >= binCount)
                idx = binCount - 1;
            counts[idx]++;
        }

        var maxCount = counts.Max();
        maxCount = Math.Max(maxCount, 1);

        var mu = rowAverages.Average();
        var sigma = SampleStdDev(rowAverages);

        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ");
        sb.Append($"width=\"{W}\" height=\"{H}\" viewBox=\"0 0 {W} {H}\">\n");
        sb.Append("<defs><style>\n");
        sb.Append(".t{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;fill:#1e293b}\n");
        sb.Append("</style></defs>\n");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#f8fafc\"/>\n");

        var title = string.IsNullOrWhiteSpace(documentTitle)
            ? "Öğrenci ortalamaları dağılımı"
            : $"Öğrenci ortalamaları — {documentTitle}";
        sb.Append($"<text class=\"t\" x=\"{W / 2}\" y=\"34\" text-anchor=\"middle\" font-size=\"17\" font-weight=\"600\">{SvgEscape(title)}</text>\n");

        sb.Append($"<rect x=\"{marginL}\" y=\"{marginT}\" width=\"{plotW}\" height=\"{plotH}\" fill=\"#ffffff\" stroke=\"#cbd5e1\" stroke-width=\"1\"/>\n");

        var barGap = 2;
        var bw = (plotW - barGap * (binCount + 1)) / Math.Max(binCount, 1);
        bw = Math.Max(bw, 4);

        for (var i = 0; i < binCount; i++)
        {
            var hBar = counts[i] / (double)maxCount * (plotH - 12);
            var x = marginL + barGap + i * (bw + barGap);
            var y = marginT + plotH - hBar;
            sb.Append($"<rect x=\"{x.ToString(inv)}\" y=\"{y.ToString(inv)}\" width=\"{bw.ToString(inv)}\" height=\"{hBar.ToString(inv)}\" fill=\"#93c5fd\" stroke=\"#2563eb\" stroke-width=\"1\" rx=\"2\"/>\n");
        }

        // Referans normal eğrisi (aynı örneklem ortalaması ve s.sapması)
        if (sigma > 1e-9 && binCount > 0)
        {
            var curvePts = new List<string>(140);
            double maxPdf = 0;
            var pdfSamples = new List<(double x, double y)>();
            for (var k = 0; k <= 120; k++)
            {
                var gx = min + span * k / 120.0;
                var py = NormalPdf(gx, mu, sigma);
                pdfSamples.Add((gx, py));
                if (py > maxPdf)
                    maxPdf = py;
            }

            maxPdf = Math.Max(maxPdf, 1e-12);
            var normH = plotH * 0.92;
            foreach (var (gx, py) in pdfSamples)
            {
                var px = marginL + (gx - min) / span * plotW;
                var pySvg = marginT + plotH - py / maxPdf * normH;
                curvePts.Add($"{px.ToString(inv)},{pySvg.ToString(inv)}");
            }

            sb.Append($"<polyline fill=\"none\" stroke=\"#7c3aed\" stroke-width=\"2.25\" opacity=\"0.88\" points=\"{string.Join(" ", curvePts)}\"/>\n");
        }

        sb.Append($"<text class=\"t\" x=\"{marginL + plotW / 2}\" y=\"{H - 52}\" text-anchor=\"middle\" font-size=\"13\">Öğrenci ortalaması (not)</text>\n");
        sb.Append($"<text class=\"t\" x=\"22\" y=\"{marginT + plotH / 2}\" text-anchor=\"middle\" font-size=\"12\" transform=\"rotate(-90 22 {marginT + plotH / 2})\">Öğrenci sayısı (yükseklik)</text>\n");

        sb.Append($"<text class=\"t\" x=\"{marginL}\" y=\"{H - 22}\" font-size=\"11\" fill=\"#64748b\">Min {min.ToString("F2", inv)} → Max {max.ToString("F2", inv)} · n={n}</text>\n");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string SvgEscape(string s)
    {
        return s
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    /// <summary>
    /// Wolfram Query API Mathematica Apply/Flatten ile sıklıkla başarısız olduğu için tablo istatistikleri burada hesaplanır.
    /// </summary>
    private static string FormatGridStatisticsLocally(List<GradeTableRow> rows, WolframGridIntent intent)
    {
        var inv = CultureInfo.InvariantCulture;

        switch (intent)
        {
            case WolframGridIntent.MeanAllValues:
            {
                var flat = rows.SelectMany(r => r.Values).ToList();
                return
                    $"Tüm sayısal notların ortalaması: {flat.Average().ToString("F4", inv)}\n" +
                    $"(Not sayısı: {flat.Count})";
            }
            case WolframGridIntent.SumAllValues:
            {
                var flat = rows.SelectMany(r => r.Values).ToList();
                return
                    $"Tüm sayısal notların toplamı: {flat.Sum().ToString("F4", inv)}\n" +
                    $"(Not sayısı: {flat.Count})";
            }
            case WolframGridIntent.MeanEachRow:
                return "Öğrenci / satır ortalamaları:\n\n" + string.Join("\n",
                    rows.Select((r, i) =>
                        $"{FormatRowCaption(r, i)}: {r.Values.Average().ToString("F4", inv)} ({r.Values.Count} not)"));
            case WolframGridIntent.SumEachRow:
                return "Öğrenci / satır toplamları:\n\n" + string.Join("\n",
                    rows.Select((r, i) =>
                        $"{FormatRowCaption(r, i)}: {r.Values.Sum().ToString("F4", inv)} ({r.Values.Count} not)"));
            case WolframGridIntent.MeanEachColumn:
            {
                var n = rows[0].Values.Count;
                var lines = new List<string>(n);
                for (var c = 0; c < n; c++)
                {
                    var col = rows.Select(r => r.Values[c]).ToList();
                    lines.Add($"Sütun {c + 1}: {col.Average().ToString("F4", inv)} ({col.Count} satır)");
                }

                return "Sütun bazında ortalamalar:\n\n" + string.Join("\n", lines);
            }
            case WolframGridIntent.SumEachColumn:
            {
                var n = rows[0].Values.Count;
                var lines = new List<string>(n);
                for (var c = 0; c < n; c++)
                {
                    var col = rows.Select(r => r.Values[c]).ToList();
                    lines.Add($"Sütun {c + 1}: {col.Sum().ToString("F4", inv)}");
                }

                return "Sütun bazında toplamlar:\n\n" + string.Join("\n", lines);
            }
            default:
            {
                var flat = rows.SelectMany(r => r.Values).ToList();
                return
                    $"Tüm sayısal notların ortalaması: {flat.Average().ToString("F4", inv)}\n" +
                    $"(Not sayısı: {flat.Count})";
            }
        }
    }

    private static string FormatRowCaption(GradeTableRow row, int index)
    {
        if (string.IsNullOrWhiteSpace(row.Label))
            return $"Satır {index + 1}";

        var s = row.Label.Trim();
        const int maxLen = 140;
        return s.Length <= maxLen ? s : s[..maxLen] + "…";
    }

    private static List<List<double>> ToValuesMatrix(List<GradeTableRow> rows) =>
        rows.Select(r => r.Values).ToList();

    private static string BuildWolframQuery(AiProcessRequestViewModel request)
    {
        var instr = request.CustomInstruction?.Trim() ?? "";
        var text = request.InputText?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(text) && ContainsLineBreak(text))
        {
            if (TryParseNumericGradeTable(text, out var gradeRows))
            {
                var intent = ClassifyGridIntent(instr);
                return BuildStructuredWolframExpression(intent, ToValuesMatrix(gradeRows));
            }

            // Yapı çözülmezse tüm sayıları tek satır listeye düşür.
            if (TryFlattenMultiLineNumericGrid(text, out var flatNumbers))
                text = flatNumbers;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(instr))
            parts.Add(instr);
        if (!string.IsNullOrWhiteSpace(text))
            parts.Add(text);
        return string.Join(" ", parts).Trim();
    }

    private enum WolframGridIntent
    {
        MeanAllValues,
        MeanEachRow,
        MeanEachColumn,
        SumAllValues,
        SumEachRow,
        SumEachColumn
    }

    /// <summary>
    /// Özel yönergeden satır/sütun/genel istatistik seçimi (TR/EN anahtar kelimeler).
    /// </summary>
    private static WolframGridIntent ClassifyGridIntent(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return WolframGridIntent.MeanAllValues;

        var s = instruction.ToLowerInvariant();

        var overallCue = Regex.IsMatch(
            s,
            @"\b(class-wide|classwide|every\s+grade|all\s+grades|all\s+scores|overall\s+(average|mean)|genel\s+ortalama|tüm\s+notlar|butun\s+notlar)\b",
            RegexOptions.IgnoreCase);

        var rowCue = Regex.IsMatch(
            s,
            @"\b(rows?|each\s+row|per\s+row|by\s+row|student|students|satır|satir|öğrenci|ogrenci)\b",
            RegexOptions.IgnoreCase);
        var colCue = Regex.IsMatch(
            s,
            @"\b(columns?|each\s+column|per\s+column|by\s+column|quiz|quizzes|exam|exams|midterm|final|test|tests|sütun|sutun)\b",
            RegexOptions.IgnoreCase);
        var sumCue = Regex.IsMatch(
            s,
            @"\b(sum|sums|total|totals|add\s+(up\s+)?all|toplam)\b",
            RegexOptions.IgnoreCase);

        if (sumCue)
        {
            if (rowCue && !colCue && !overallCue)
                return WolframGridIntent.SumEachRow;
            if (colCue && !rowCue && !overallCue)
                return WolframGridIntent.SumEachColumn;
            return WolframGridIntent.SumAllValues;
        }

        if (!overallCue && rowCue && !colCue)
            return WolframGridIntent.MeanEachRow;
        if (!overallCue && colCue && !rowCue)
            return WolframGridIntent.MeanEachColumn;

        return WolframGridIntent.MeanAllValues;
    }

    /// <summary>
    /// Mathematica tarzı ifade — Wolfram Alpha Query API ile uyumludur.
    /// Satır ortalamaları: Mean /@ m ; sütun ortalamaları: Mean[m] ; satır toplamları: Total /@ m ; sütun toplamları: Total[m].
    /// </summary>
    private static string BuildStructuredWolframExpression(WolframGridIntent intent, List<List<double>> rows)
    {
        var matrix = FormatWolframMatrix(rows);

        return intent switch
        {
            WolframGridIntent.MeanEachRow => $"Mean /@ {matrix}",
            WolframGridIntent.MeanEachColumn => $"Mean[{matrix}]",
            WolframGridIntent.SumEachRow => $"Total /@ {matrix}",
            WolframGridIntent.SumEachColumn => $"Total[{matrix}]",
            // Çok öğrenci × çok sınav: düz liste URL/Wolfram limitini aşar; Flatten kısa ve güvenilir.
            WolframGridIntent.MeanAllValues => $"Mean[Flatten[{matrix}]]",
            WolframGridIntent.SumAllValues => $"Total[Flatten[{matrix}]]",
            _ => $"Mean[Flatten[{matrix}]]"
        };
    }

    private static string FormatWolframMatrix(List<List<double>> rows)
    {
        static string Row(List<double> r) =>
            "{" + string.Join(",", r.Select(n => n.ToString(CultureInfo.InvariantCulture))) + "}";

        return "{" + string.Join(",", rows.Select(Row)) + "}";
    }

    /// <summary>
    /// Her satırda sağdan başlayarak ardışık sayıları toplar; soldaki tokenlar etiket (öğrenci kimliği/adı) olarak saklanır.
    /// </summary>
    private static bool TryExtractTrailingNumericTokens(string line, out string labelPrefix, out List<double> numbers)
    {
        numbers = new List<double>();
        labelPrefix = "";
        var tokens = Regex.Split(line.Trim(), @"\s+")
            .Where(t => t.Length > 0)
            .ToArray();
        if (tokens.Length == 0)
            return false;

        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            var t = tokens[i].Replace(',', '.');
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                break;
            numbers.Insert(0, v);
        }

        if (numbers.Count == 0)
            return false;

        var prefixLen = tokens.Length - numbers.Count;
        labelPrefix = prefixLen > 0 ? string.Join(" ", tokens.Take(prefixLen)).Trim() : "";
        return true;
    }

    private static IEnumerable<string> SplitPhysicalLines(string raw)
    {
        return raw.Split(new[] { '\r', '\n', '\u0085', '\u2028', '\u2029' }, StringSplitOptions.None);
    }

    private static readonly Regex SHeaderLineHints =
        new(
            @"\b(first\s+name|last\s+name|id\s+number|student\s+name|userid|hw\s*avg|quiz:|midterm|final\s*\(|overall|cumulative|letter\s+grade|\bavg\b\s*$)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool LooksLikeTableHeaderLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return false;
        return SHeaderLineHints.IsMatch(trimmed.ToLowerInvariant());
    }

    /// <summary>
    /// LMS/OCR tablosu: başlık satırlarını atlar; satır başına sağdan notları ve soldan etiketi alır.
    /// Sütun sayısı OCR'da kayıyorsa mod genişliğe göre satırın son K sayısı kullanılır.
    /// </summary>
    private static bool TryParseNumericGradeTable(string raw, out List<GradeTableRow> table)
    {
        table = new List<GradeTableRow>();
        foreach (var line in SplitPhysicalLines(raw))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;
            if (LooksLikeTableHeaderLine(trimmed))
                continue;
            if (!TryExtractTrailingNumericTokens(trimmed, out var label, out var nums))
                continue;
            table.Add(new GradeTableRow(label, nums));
        }

        var solid = table.Where(r => r.Values.Count >= 2).ToList();
        if (solid.Count < 2)
            return false;

        var modeCols = solid.GroupBy(r => r.Values.Count)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
        if (modeCols < 2)
            return false;

        var aligned = new List<GradeTableRow>();
        foreach (var r in solid)
        {
            List<double> vals;
            if (r.Values.Count == modeCols)
                vals = r.Values;
            else if (r.Values.Count > modeCols)
                vals = r.Values.Skip(r.Values.Count - modeCols).ToList();
            else
                continue;

            aligned.Add(new GradeTableRow(r.Label, vals));
        }

        table = aligned;
        return table.Count >= 2;
    }

    private static readonly Regex SNumericLiteral =
        new(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Metinde satır sonu varsa ve birden fazla sayı bulunuyorsa, tek satır virgül listesine çevirir.
    /// </summary>
    private static bool TryFlattenMultiLineNumericGrid(string raw, out string flatNumbers)
    {
        flatNumbers = "";
        if (!ContainsLineBreak(raw))
            return false;

        var nums = ExtractOrderedNumericLiterals(raw);
        if (nums.Count < 2)
            return false;

        flatNumbers = string.Join(", ", nums.Select(n => n.ToString(CultureInfo.InvariantCulture)));
        return true;
    }

    private static bool ContainsLineBreak(string s)
    {
        foreach (var c in s)
        {
            if (c is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029')
                return true;
        }

        return false;
    }

    private static List<double> ExtractOrderedNumericLiterals(string text)
    {
        var list = new List<double>();
        foreach (Match m in SNumericLiteral.Matches(text))
        {
            var token = m.Value.Replace(',', '.');
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                list.Add(v);
        }

        return list;
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
