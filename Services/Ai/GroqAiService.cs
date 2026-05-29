using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: Groq Inference API üzerinden NLP işlemleri yapar.
 * [TR] Neden gerekli: Groq, Llama / Gemma / Mistral modellerini ücretsiz ve çok hızlı çalıştırır.
 *      Kredi kartı gerektirmez; https://console.groq.com/keys adresinden API anahtarı alınır.
 *      HuggingFace'in bazı modelleri ücretsiz hesaplarda çalışmazken Groq her modeli sunar.
 * [TR] İlgili: IAiService, MultiProviderAiService, GroqApiOptions, AiController
 *
 * GROQ API HAKKINDA (TR)
 *   - Base URL : https://api.groq.com/openai/v1/chat/completions
 *   - Format   : OpenAI uyumlu (model, messages, max_tokens)
 *   - Auth     : Authorization: Bearer {API_KEY}
 *   - Ücretsiz modeller: llama-3.3-70b-versatile, llama-3.1-8b-instant,
 *                         gemma2-9b-it, deepseek-r1-distill-llama-70b
 *   - Rate limit: Ücretsiz hesap dakikada 30 istek, günde 14400 token
 *
 * MODIFICATION NOTES (TR)
 * - Yeni Groq modeli eklemek: appsettings.json'a Provider="Groq" ile yeni satır eklenir.
 * - Streaming eklemek: PostAsync yerine HttpCompletionOption.ResponseHeadersRead + StreamReader kullanılır.
 * - Zorluk: Kolay.
 *
 * JÜRI MODİFİKASYON NOTLARI (TR)
 * - "Groq ile Gemini arasındaki fark?" → Groq açık kaynak modelleri çalıştırır (Llama, Gemma);
 *   Gemini ise Google'ın özel modelidir. Groq ücretsiz, Gemini ücretsiz quota sınırlıdır.
 * - "Başka model eklenebilir mi?" → appsettings.json'a eklemek yeterlidir; servis değişmez.
 */
public class GroqAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly GroqApiOptions _options;
    private readonly ILogger<GroqAiService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GroqAiService(
        HttpClient http,
        IOptions<AiOptions> options,
        ILogger<GroqAiService> logger)
    {
        _http = http;
        _options = options.Value.Groq;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    // ─── ANA YÖNLENDIRICI ─────────────────────────────────────────────────────
    // [TR] Task tipine göre uygun sistem promptunu belirler ve Groq'a gönderir.
    public async Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InputText))
        {
            // [TR] Math işlemi bazen yalnız özel yönerge ile gelir (örn. Wolfram sorgusu).
            //      Groq yine de Math'i desteklemez — net mesaj döndür.
            if (string.Equals(request.OperationType, "Math", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(request.CustomInstruction))
            {
                return new AiServiceResult
                {
                    OutputText =
                        "[Math / chart operations cannot be done with Groq. Select a Gemini image model or Wolfram Alpha.]"
                };
            }

            return new AiServiceResult { OutputText = "[Text to process is empty]" };
        }

        try
        {
            return request.OperationType switch
            {
                "Translate"     => await TranslateAsync(request, cancellationToken),
                "Summarize"     => await SummarizeAsync(request, cancellationToken),
                "Rewrite"       => await RewriteAsync(request, cancellationToken),
                "CreativeWrite" => await CreativeWriteAsync(request, cancellationToken),
                // [TR] Explanation: Groq metin LLM'leri görsel almaz; sadece metni
                //      analiz ederek açıklayıcı çıktı üretir. Görsel için Gemini önerilir.
                "Explanation"   => await ExplainAsync(request, cancellationToken),
                "Math" => new AiServiceResult
                {
                    OutputText =
                        "[Math image charts or Wolfram solutions are not supported with Groq. " +
                        "Select a Gemini 3.1 image model or Wolfram Alpha.]"
                },
                "Visualize"     => new AiServiceResult
                {
                    OutputText = "[Image generation is not supported by Groq models. " +
                                 "Please select a Gemini Image model.]"
                },
                _ => new AiServiceResult { OutputText = $"[Unsupported operation: {request.OperationType}]" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq AI operation failed. Model={Model}, Op={Op}",
                request.ModelName, request.OperationType);
            throw new InvalidOperationException($"Groq error: {ex.Message}", ex);
        }
    }

    // ─── 1. ÇEVİRİ ─────────────────────────────────────────────────────────────
    private async Task<AiServiceResult> TranslateAsync(AiProcessRequestViewModel req, CancellationToken ct)
    {
        var targetLang = req.TargetLanguage ?? "Turkish";
        var style      = req.Style ?? "Formal";
        return await CallAsync(req.ModelName!,
            $"You are a professional translator. Translate text into {targetLang} using {style} style. " +
            $"Return ONLY the translated text, no explanations.",
            $"Translate the following text to {targetLang}:\n\n{req.InputText}",
            ct);
    }

    // ─── 2. ÖZETLEME ───────────────────────────────────────────────────────────
    private async Task<AiServiceResult> SummarizeAsync(AiProcessRequestViewModel req, CancellationToken ct)
    {
        var input = req.InputText!.Length > 4000 ? req.InputText[..4000] : req.InputText;
        return await CallAsync(req.ModelName!,
            "You are an expert summarizer. Create a concise, accurate summary. Return ONLY the summary.",
            $"Summarize the following text:\n\n{input}{LanguageSuffix(req)}",
            ct);
    }

    // ─── 3. YENİDEN YAZMA ──────────────────────────────────────────────────────
    private async Task<AiServiceResult> RewriteAsync(AiProcessRequestViewModel req, CancellationToken ct)
    {
        var instruction = string.IsNullOrWhiteSpace(req.CustomInstruction)
            ? $"Rewrite in {req.Style ?? "Formal"} style"
            : req.CustomInstruction;
        return await CallAsync(req.ModelName!,
            """
            You are a professional rewriting assistant. Rewrite the given text according to the instruction.
            Output ONLY the rewritten text — no commentary, explanations, analysis, or meta-remarks about the original.
            Do not quote the original and then comment on it. The rewrite may be shorter or longer as needed.
            """,
            $"""
            Instruction: {instruction}
            Style: {req.Style ?? "Formal"}

            Source text to rewrite (output only the rewritten version):
            {req.InputText}{LanguageSuffix(req)}
            """,
            ct);
    }

    // ─── 4. YARATICI YAZARLIK ──────────────────────────────────────────────────
    private async Task<AiServiceResult> CreativeWriteAsync(AiProcessRequestViewModel req, CancellationToken ct)
    {
        var instruction = string.IsNullOrWhiteSpace(req.CustomInstruction)
            ? "Creatively expand and reimagine the source while keeping its subject and core content"
            : req.CustomInstruction;
        return await CallAsync(req.ModelName!,
            """
            You are a creative writing assistant. Transform the SOURCE TEXT according to the instruction.
            The result must be a creative reworking of the source — keep its subject, context, and core content.
            If the instruction adds themes or topics, weave them into a rewrite OF the source; do not ignore the source and write unrelated new content.
            Output ONLY the creative text — no explanations.
            """,
            $"""
            Instruction: {instruction}
            Style: {req.Style ?? "Formal"}

            Source text to transform creatively:
            {req.InputText}{LanguageSuffix(req)}
            """,
            ct, maxTokens: 3000);
    }

    /// <summary>
    /// [TR] Çeviri dışı işlemlerde cevabın seçilen dilde olmasını zorlayan yönerge.
    /// "Auto"/boş seçimde dil dayatılmaz; kullanıcı prompt'u kendi dilinde yazsa bile
    /// cevap seçilen dilde döner.
    /// </summary>
    private static string LanguageSuffix(AiProcessRequestViewModel req)
    {
        var tgt = req.TargetLanguage;
        if (string.IsNullOrWhiteSpace(tgt) ||
            string.Equals(tgt, "Auto", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return $"\n\nIMPORTANT: Write your entire answer in {tgt}, regardless of the source or instruction language.";
    }

    // ─── 4b. AÇIKLAMA / EXPLANATION ────────────────────────────────────────────
    /*
     * [TR] Verilen metni analitik olarak açıklar (Groq metin modelleri için).
     *      Görsel girdi alınmaz; multimodal kullanım için kullanıcı Gemini seçmelidir.
     *      Çıktıda: içerik tipi tespiti, detaylı analiz (sayım/gruplama/özet) ve
     *      anlamlı çıkarımlar.
     */
    private async Task<AiServiceResult> ExplainAsync(AiProcessRequestViewModel req, CancellationToken ct)
    {
        var instruction = string.IsNullOrWhiteSpace(req.CustomInstruction)
            ? "(no extra instruction)"
            : req.CustomInstruction;

        var input = req.InputText!.Length > 4000
            ? req.InputText[..4000]
            : req.InputText;

        return await CallAsync(req.ModelName!,
            """
            You are an analytical assistant. Explain the given content clearly in three plain-text sections:
            Content type, Detailed analysis, Insights.
            Plain text only — no Markdown: no # headers, no * or ** bullets/bold, no backticks.
            Use section titles on their own line, then normal paragraphs. Numbered lines (1. 2.) are OK; asterisks are not.
            If the user instruction asks for a specific focus, follow it while keeping plain readable text.
            """,
            $"""
            User instruction (priority if any): {instruction}

            Text to explain:
            {input}{LanguageSuffix(req)}
            """,
            ct, maxTokens: 3000);
    }

    // ─── GROQ API ÇAĞRISI ────────────────────────────────────────────────────
    /*
     * [TR] Groq'un OpenAI uyumlu chat completions endpoint'ine POST isteği gönderir.
     *      Yanıt formatı: {"choices":[{"message":{"content":"..."}}]}
     *      İçerik çıkarma, HuggingFaceAiService ile aynı güvenli yaklaşımı kullanır:
     *      ValueKind kontrolü yapılarak GetString() yalnızca String üzerinde çağrılır.
     */
    private async Task<AiServiceResult> CallAsync(
        string modelId,
        string systemPrompt,
        string userPrompt,
        CancellationToken ct,
        int maxTokens = 4000)
    {
        var payload = new
        {
            model       = modelId,
            messages    = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt   }
            },
            max_tokens  = maxTokens,
            temperature = 0.7
        };

        var json    = JsonSerializer.Serialize(payload, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Groq request -> model={Model}", modelId);

        var response = await _http.PostAsync(_options.BaseUrl, content, ct);
        var body     = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Groq HTTP {Status}: {Body}", (int)response.StatusCode, body);
            string errDetail;
            try
            {
                using var errDoc = JsonDocument.Parse(body);
                errDetail = errDoc.RootElement.TryGetProperty("error", out var errProp)
                    ? (errProp.ValueKind == JsonValueKind.String
                        ? errProp.GetString() ?? body
                        : errProp.ToString())
                    : body;
            }
            catch { errDetail = body; }

            throw new InvalidOperationException($"Groq API error ({(int)response.StatusCode}): {errDetail}");
        }

        // [TR] choices[0].message.content — String veya Array formatlarını destekler
        using var doc  = JsonDocument.Parse(body);
        var messageEl  = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        var text       = ExtractContent(messageEl);

        return new AiServiceResult { OutputText = text.Trim() };
    }

    // ─── JSON YARDIMCISI ─────────────────────────────────────────────────────
    private static string ExtractContent(JsonElement messageEl)
    {
        if (messageEl.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null)
        {
            if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? string.Empty;
            if (c.ValueKind == JsonValueKind.Array)
                return string.Concat(c.EnumerateArray()
                    .Select(i => i.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString() ?? "" : string.Empty));
        }
        return messageEl.ToString();
    }
}
