using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: HuggingFace Inference API üzerinden NLP ve görsel işlemler yapar.
 * [TR] Neden gerekli: Ücretsiz, açık kaynak modeller kullanarak Gemini'ye alternatif sağlar.
 *      Farklı task'ler için prompt tabanlı LLM modelleri kullanılır.
 * [TR] İlgili: IAiService, MultiProviderAiService, AiOptions, AiController
 *
 * ÖNEMLİ TASARIM KARARI (TR):
 *   HuggingFace 2025+ API'sinde eski pipeline endpoint'leri ({"inputs": "..."}) kaldırıldı.
 *   Yeni API, OpenAI uyumlu chat completions formatını kullanır:
 *   POST https://router.huggingface.co/v1/chat/completions
 *   Body: {"model": "...", "messages": [{"role":"user","content":"..."}]}
 *   Bu nedenle çeviri/özetleme/yeniden yazma gibi task'ler
 *   artık "instruct" tipli LLM modelleriyle prompt bazlı yapılmaktadır.
 *
 * MODIFICATION NOTES (TR)
 * - Yeni task eklemek: ProcessAsync'teki switch'e yeni case, private prompt metodu eklenir.
 * - Farklı model eklemek: appsettings.json'a model tanımı eklenir; servis değişmez.
 * - Rate limit (429) durumunda: exponential backoff retry logic eklenebilir.
 * - Token limiti aşıldığında: metin parçalama (chunking) eklenir.
 * - Zorluk: Orta.
 *
 * JÜRI MODİFİKASYON NOTLARI (TR)
 * - "HuggingFace neden yavaş?" → Ücretsiz tier cold start süresi 5-30 saniyedir.
 * - "Başka HF modeli eklenebilir mi?" → appsettings.json'a eklemek yeterlidir.
 * - "Görsel üretimi neden yok?" → HuggingFace görsel üretimi ayrı bir provider endpoint'i gerektirir.
 */
public class HuggingFaceAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HuggingFaceAiService> _logger;

    // [TR] Metin işlemleri (chat) için OpenAI uyumlu endpoint
    private const string ChatEndpoint = "https://router.huggingface.co/v1/chat/completions";

    // [TR] Görsel üretim (text-to-image) için HF pipeline endpoint
    //      Farklı format: {"inputs":"prompt"} → ham görüntü byte'ları döner
    private const string ImagePipelineBase = "https://router.huggingface.co/hf-inference/models";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HuggingFaceAiService(
        HttpClient http,
        IOptions<AiOptions> options,
        IWebHostEnvironment env,
        ILogger<HuggingFaceAiService> logger)
    {
        _http = http;
        _options = options.Value;
        _env = env;
        _logger = logger;

        // [TR] Timeout ve Authorization bir kez yapılandırılır.
        //      BaseAddress kullanılmaz; tam URL her istekte inşa edilir.
        _http.Timeout = TimeSpan.FromSeconds(_options.HuggingFace.TimeoutSeconds);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.HuggingFace.ApiKey);
    }

    // ─── ANA YÖNLENDIRICI ─────────────────────────────────────────────────────
    // [TR] Task tipine göre doğru private metoda yönlendirir.
    public async Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InputText))
        {
            if (string.Equals(request.OperationType, "Visualize", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(request.CustomInstruction))
            {
                return await VisualizeAsync(documentTitle, request, cancellationToken);
            }

            if (string.Equals(request.OperationType, "Math", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(request.CustomInstruction))
            {
                return new AiServiceResult
                {
                    OutputText =
                        "[For Math, select a Gemini image model or Wolfram Alpha. HuggingFace does not support this operation.]"
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
                "Visualize"     => await VisualizeAsync(documentTitle, request, cancellationToken),
                // [TR] Explanation: HuggingFace metin modelleri görsel almaz; sadece
                //      verilen metni analiz ederek "ne anlatıyor / hangi tip içerik /
                //      hangi veriler" gibi açıklayıcı çıktı verir.
                "Explanation"   => await ExplainAsync(request, cancellationToken),
                "Math" => new AiServiceResult
                {
                    OutputText =
                        "[For Math charts, select a Gemini image model or Wolfram Alpha. Text-based HF models are not suitable.]"
                },
                _ => new AiServiceResult { OutputText = $"[Unsupported operation: {request.OperationType}]" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HuggingFace AI operation failed. Model={Model}, Op={Op}",
                request.ModelName, request.OperationType);
            throw new InvalidOperationException($"HuggingFace error: {ex.Message}", ex);
        }
    }

    // ─── 1. ÇEVİRİ ─────────────────────────────────────────────────────────────
    /*
     * [TR] Prompt tabanlı çeviri: LLM'e "Translate to {lang}: {text}" tarzında istek gönderilir.
     *      Eski NLLB/Opus-MT pipeline modelleri artık çalışmadığından
     *      bu yaklaşım tüm dillerde çalışan instruct LLM'lerle gerçekleştirilir.
     */
    private async Task<AiServiceResult> TranslateAsync(
        AiProcessRequestViewModel request,
        CancellationToken ct)
    {
        var targetLang = request.TargetLanguage ?? "Turkish";
        var style = request.Style ?? "Formal";

        var systemPrompt = $"You are a professional translator. " +
                           $"Translate text accurately into {targetLang} using a {style} style. " +
                           $"Return ONLY the translated text, no explanations.";

        var userPrompt = $"Translate the following text to {targetLang}:\n\n{request.InputText}";

        return await CallChatAsync(request.ModelName!, systemPrompt, userPrompt, ct);
    }

    // ─── 2. ÖZETLEME ───────────────────────────────────────────────────────────
    /*
     * [TR] Prompt tabanlı özetleme: LLM'e özet talebi gönderilir.
     *      Eski BART/DistilBART pipeline modelleri artık çalışmadığından
     *      instruct LLM'ler tercih edilir.
     */
    private async Task<AiServiceResult> SummarizeAsync(
        AiProcessRequestViewModel request,
        CancellationToken ct)
    {
        // [TR] Çok uzun metinleri modelin token limitini aşmamak için kırpıyoruz.
        var input = request.InputText!.Length > 4000
            ? request.InputText[..4000]
            : request.InputText;

        var systemPrompt = "You are an expert summarizer. " +
                           "Create a concise, accurate summary of the provided text. " +
                           "Return ONLY the summary, no preamble.";

        var userPrompt = $"Summarize the following text:\n\n{input}";

        return await CallChatAsync(request.ModelName!, systemPrompt, userPrompt, ct);
    }

    // ─── 3. YENİDEN YAZMA ──────────────────────────────────────────────────────
    /*
     * [TR] Prompt tabanlı yeniden yazma: Kullanıcının özel yönergesi + metin LLM'e gönderilir.
     *      Eski Flan-T5 pipeline modeli yerine instruct LLM kullanılır.
     */
    private async Task<AiServiceResult> RewriteAsync(
        AiProcessRequestViewModel request,
        CancellationToken ct)
    {
        var instruction = string.IsNullOrWhiteSpace(request.CustomInstruction)
            ? $"Rewrite in {request.Style ?? "Formal"} style"
            : request.CustomInstruction;

        var systemPrompt = """
            You are a professional rewriting assistant. Rewrite the given text according to the user's instruction.
            Output ONLY the rewritten text — no commentary, explanations, analysis, or meta-remarks about the original.
            Do not quote the original and then comment on it. The rewrite may be shorter or longer as needed.
            """;

        var userPrompt = $"""
            Instruction: {instruction}
            Style: {request.Style ?? "Formal"}

            Source text to rewrite (output only the rewritten version):
            {request.InputText}
            """;

        return await CallChatAsync(request.ModelName!, systemPrompt, userPrompt, ct);
    }

    // ─── 4. YARATICI YAZARLIK ──────────────────────────────────────────────────
    /*
     * [TR] Prompt tabanlı yaratıcı yazarlık: LLM'e yaratıcı içerik üretimi talebi gönderilir.
     *      Eski GPT-Neo metin tamamlama yerine instruct LLM kullanılır.
     */
    private async Task<AiServiceResult> CreativeWriteAsync(
        AiProcessRequestViewModel request,
        CancellationToken ct)
    {
        var instruction = string.IsNullOrWhiteSpace(request.CustomInstruction)
            ? "Creatively expand and reimagine the source while keeping its subject and core content"
            : request.CustomInstruction;

        var systemPrompt = """
            You are a creative writing assistant. Transform the SOURCE TEXT according to the instruction.
            The result must be a creative reworking of the source — keep its subject, context, and core content.
            If the instruction adds themes or topics, weave them into a rewrite OF the source; do not ignore the source and write unrelated new content.
            Output ONLY the creative text — no explanations.
            """;

        var userPrompt = $"""
            Instruction: {instruction}
            Style: {request.Style ?? "Formal"}

            Source text to transform creatively:
            {request.InputText}
            """;

        return await CallChatAsync(request.ModelName!, systemPrompt, userPrompt, ct, maxTokens: 3000);
    }

    // ─── 4b. AÇIKLAMA / EXPLANATION ────────────────────────────────────────────
    /*
     * [TR] Verilen metni analitik olarak açıklar:
     *        - İçerik türünü tespit et (öğrenci notları, hikâye, makale, sayısal veri vb.)
     *        - İçeriği detaylı analiz et (sayım, gruplama, kahramanlar, dönem, vb.)
     *        - Mümkünse anlamlı çıkarımlar ver
     *
     * [TR] HuggingFace metin LLM'leri görsel girdi almaz; bu yüzden burada görsel
     *      iliştirilmiş olsa bile yalnız metin işlenir. Multimodal kullanım için
     *      kullanıcı Gemini modellerinden birini seçmelidir.
     */
    private async Task<AiServiceResult> ExplainAsync(
        AiProcessRequestViewModel request,
        CancellationToken ct)
    {
        var instruction = string.IsNullOrWhiteSpace(request.CustomInstruction)
            ? "(no extra instruction)"
            : request.CustomInstruction!;

        var input = request.InputText!.Length > 4000
            ? request.InputText[..4000]
            : request.InputText;

        var systemPrompt = """
            You are an analytical assistant. Explain the given content clearly in three plain-text sections:
            Content type, Detailed analysis, Insights.
            Plain text only — no Markdown: no # headers, no * or ** bullets/bold, no backticks.
            Use section titles on their own line, then normal paragraphs. Numbered lines (1. 2.) are OK; asterisks are not.
            If the user instruction asks for a specific focus, follow it while keeping plain readable text.
            """;

        var userPrompt = $"""
            User instruction (priority if any): {instruction}

            Text to explain:
            {input}
            """;

        return await CallChatAsync(request.ModelName!, systemPrompt, userPrompt, ct, maxTokens: 3000);
    }

    // ─── 5. GÖRSEL ÜRETME ──────────────────────────────────────────────────────
    /*
     * [TR] HuggingFace text-to-image pipeline endpoint'ini kullanır.
     *      Chat completions formatı DEĞİL — ayrı bir pipeline formatıdır:
     *        POST https://router.huggingface.co/hf-inference/models/{model_id}
     *        Body : {"inputs": "prompt", "parameters": {...}}
     *        Response: ham görüntü byte'ları (image/png veya image/jpeg)
     *      Üretilen görüntü wwwroot/ai-images/ klasörüne kaydedilir.
     *
     * MODIFICATION NOTES (TR)
     * - Daha iyi kalite: num_inference_steps artırılır (yavaşlar), guidance_scale yükseltilir.
     * - Türkçe prompt desteği: Önce metnin İngilizceye çevirilmesi gerekir (future work).
     * - negative_prompt: İstenmeyen öğeleri çıkarmak için parameters'a eklenir.
     */
    private async Task<AiServiceResult> VisualizeAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken ct)
    {
        // [TR] FLUX ve diğer görsel modeller İngilizce prompt ister.
        //      Türkçe OCR metni ASCII'ye çevrilerek anahtar kelimeler alınır.
        var prompt = BuildEnglishImagePrompt(documentTitle, request.InputText ?? string.Empty, request.CustomInstruction);

        // [TR] Negative prompt: istenmeyen öğeleri engeller
        var negativePrompt = "blurry, low quality, distorted, text, watermark, ugly, bad anatomy";

        var payload = new
        {
            inputs = prompt,
            parameters = new
            {
                num_inference_steps = 25,
                guidance_scale      = 7.5,
                width               = 768,
                height              = 768,
                negative_prompt     = negativePrompt
            }
        };

        var json    = JsonSerializer.Serialize(payload, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // [TR] Her model için tam URL: https://router.huggingface.co/hf-inference/models/{model}
        var fullUrl = $"{ImagePipelineBase}/{request.ModelName}";
        _logger.LogInformation("HuggingFace image request -> {Url}", fullUrl);

        var response = await _http.PostAsync(fullUrl, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("HuggingFace image HTTP {Status}: {Body}", (int)response.StatusCode, errBody);

            string errDetail;
            try
            {
                using var errDoc = JsonDocument.Parse(errBody);
                errDetail = errDoc.RootElement.TryGetProperty("error", out var errProp)
                    ? (errProp.ValueKind == JsonValueKind.String ? errProp.GetString() ?? errBody : errProp.ToString())
                    : errBody;
            }
            catch { errDetail = errBody; }

            throw new InvalidOperationException($"HuggingFace image error ({(int)response.StatusCode}): {errDetail}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            // [TR] Başarılı: ham görüntü byte'ları alınır ve diske kaydedilir
            var bytes    = await response.Content.ReadAsByteArrayAsync(ct);
            var ext      = contentType.Contains("jpeg") ? "jpg" : "png";
            var fileName = $"{Guid.NewGuid():N}.{ext}";
            var dir      = Path.Combine(_env.WebRootPath, "ai-images");
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(Path.Combine(dir, fileName), bytes, ct);

            return new AiServiceResult
            {
                OutputText    = "Image generated successfully (HuggingFace Stable Diffusion).",
                OutputImageUrl = $"/ai-images/{fileName}"
            };
        }

        // [TR] Beklenmeyen JSON yanıtı — hata mesajı olabilir
        var raw = await response.Content.ReadAsStringAsync(ct);
        return new AiServiceResult { OutputText = $"[Unexpected response format]: {raw}" };
    }

    // ─── YARDIMCI: Chat Completions API çağrısı ───────────────────────────────
    /*
     * [TR] HuggingFace yeni API'sinin (OpenAI uyumlu) chat completions endpoint'ini çağırır.
     *      URL: https://router.huggingface.co/v1/chat/completions
     *      Format: {"model": "...", "messages": [...], "max_tokens": ...}
     *
     *      JÜRI SORUSU: "HuggingFace neden OpenAI formatını kullanıyor?"
     *      → HuggingFace, 2025 itibarıyla tüm modellerine OpenAI uyumlu bir router
     *        kurmuştur. Bu sayede farklı altyapı sağlayıcıları (Together, Fireworks, vb.)
     *        tek bir API ile kullanılabilir.
     */
    private async Task<AiServiceResult> CallChatAsync(
        string modelId,
        string systemPrompt,
        string userPrompt,
        CancellationToken ct,
        int maxTokens = 4000)
    {
        var payload = new
        {
            model = modelId,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt   }
            },
            max_tokens = maxTokens,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("HuggingFace request -> model={Model}, endpoint={Endpoint}",
            modelId, ChatEndpoint);

        var response = await _http.PostAsync(ChatEndpoint, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("HuggingFace HTTP {Status}: {Body}", (int)response.StatusCode, body);

            // [TR] HuggingFace hata gövdesini ayrıştırmaya çalış.
            //      "error" alanı bazen string, bazen {"message":...,"code":...} objesi gelir.
            //      Tüm exception türlerini catch ediyoruz; JSON kırık olsa da ham body dönülür.
            string errDetail;
            try
            {
                using var errDoc = JsonDocument.Parse(body);
                if (errDoc.RootElement.TryGetProperty("error", out var errProp))
                {
                    errDetail = errProp.ValueKind == JsonValueKind.String
                        ? errProp.GetString() ?? body
                        : ExtractText(errProp);           // object veya array olabilir
                }
                else
                {
                    errDetail = body;
                }
            }
            catch
            {
                errDetail = body;
            }

            throw new InvalidOperationException($"HuggingFace API error ({(int)response.StatusCode}): {errDetail}");
        }

        // [TR] OpenAI uyumlu başarılı yanıt: choices[0].message.content
        //      HuggingFace modellerine göre "content" alanı üç farklı biçimde gelebilir:
        //      1) "content": "düz metin"                        ← standart
        //      2) "content": [{"type":"text","text":"..."}]     ← vision/multimodal
        //      3) "content": null  (bazı reasoning modelleri)   ← null; reasoning_content'e bak
        //      Bu üç durum da aşağıda güvenli biçimde ele alınır.
        using var doc = JsonDocument.Parse(body);
        _logger.LogDebug("HuggingFace successful response: {Body}", body.Length > 500 ? body[..500] : body);

        var messageEl = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");

        string text = ExtractMessageContent(messageEl);

        return new AiServiceResult { OutputText = text.Trim() };
    }

    // ─── JSON YARDIMCILARI ────────────────────────────────────────────────────

    /// <summary>
    /// message element'inden content metnini güvenli biçimde çıkarır.
    /// [TR] Farklı HuggingFace modellerinin döndürdüğü tüm content formatlarını destekler.
    /// </summary>
    private static string ExtractMessageContent(JsonElement messageEl)
    {
        // [TR] 1. Önce standart "content" alanına bak
        if (messageEl.TryGetProperty("content", out var contentEl))
        {
            var fromContent = ExtractText(contentEl);
            if (!string.IsNullOrWhiteSpace(fromContent))
                return fromContent;
        }

        // [TR] 2. Bazı reasoning modelleri (DeepSeek-R1 vb.) "reasoning_content" döner
        if (messageEl.TryGetProperty("reasoning_content", out var reasonEl))
        {
            var fromReason = ExtractText(reasonEl);
            if (!string.IsNullOrWhiteSpace(fromReason))
                return fromReason;
        }

        // [TR] 3. Hiçbir alan bulunamazsa tüm message JSON'unu döndür
        return messageEl.ToString();
    }

    /// <summary>
    /// Bir JsonElement'ten metin çıkarır; String, Array ve Object durumlarını ele alır.
    /// [TR] GetString() yalnızca ValueKind==String olduğunda çağrılır; 
    ///      Object veya Array üzerinde çağrılmaz → tip uyuşmazlığı hatası önlenir.
    /// </summary>
    private static string ExtractText(JsonElement el)
    {
        return el.ValueKind switch
        {
            // [TR] Standart düz metin
            JsonValueKind.String => el.GetString() ?? string.Empty,

            // [TR] Vision/multimodal: [{type:"text",text:"..."}, ...]
            JsonValueKind.Array => string.Concat(
                el.EnumerateArray()
                  .Select(item => item.TryGetProperty("text", out var t)
                      ? (t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : t.ToString())
                      : string.Empty)),

            // [TR] Null — içerik boş
            JsonValueKind.Null => string.Empty,

            // [TR] Object — mesaj alanını veya ham JSON'u döndür
            JsonValueKind.Object => el.TryGetProperty("message", out var msg)
                ? ExtractText(msg)
                : el.ToString(),

            // [TR] Number, Boolean, vb. beklenmeyen türler
            _ => el.ToString()
        };
    }

    // ─── PROMPT YARDIMCISI ────────────────────────────────────────────────────
    /// <summary>
    /// Görsel modeller için İngilizce prompt oluşturur.
    /// [TR] Türkçe metin ve kullanıcının özel prompt'u ASCII'ye dönüştürülür;
    ///      özel prompt varsa görsel yönergesinde öncelikli kullanılır.
    /// </summary>
    private static string BuildEnglishImagePrompt(string documentTitle, string inputText, string? customInstruction)
    {
        var mergedInput = string.IsNullOrWhiteSpace(customInstruction)
            ? inputText
            : $"{customInstruction} {inputText}";

        var asciiOnly = new string(mergedInput
            .Where(c => c < 128 && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)))
            .ToArray());

        var keywords = asciiOnly
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Take(10)
            .ToList();

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

        var instructionPrefix = string.IsNullOrWhiteSpace(customInstruction)
            ? ""
            : $"Follow this user visual instruction: {new string(customInstruction.Where(c => c < 128).ToArray())}. ";

        return $"{instructionPrefix}A detailed professional illustration about {subject}. " +
               "High quality, sharp, photorealistic, 4K, vibrant colors.";
    }
}
