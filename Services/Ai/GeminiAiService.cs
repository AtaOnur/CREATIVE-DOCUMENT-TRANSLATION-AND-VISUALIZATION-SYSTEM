using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar:
 *      Google Gemini REST API'sini kullanarak metin işleme (çeviri, özetleme vb.) ve
 *      görsel üretimi (gemini-...-image-generation modeli) yapar.
 *
 * [TR] Neden gerekli:
 *      IAiService arayüzünü gerçek Gemini API ile doldurur; mock servisi ile bire bir
 *      değiştirilebilir (bkz. Program.cs, Ai:Provider ayarı).
 *
 * [TR] Görsel üretim akışı (Visualize operasyonu):
 *      1. appsettings Ai:Gemini:ImageModel kontrol edilir.
 *      2. responseModalities:["IMAGE"] ile Gemini'ye istek gönderilir.
 *      3. Gelen base64 PNG, wwwroot/ai-images/ klasörüne kaydedilir.
 *      4. /ai-images/<guid>.png URL'i AiServiceResult.OutputImageUrl olarak döner.
 *      Fallback: ImageModel boşsa eski davranış (metin prompt) devreye girer.
 *
 * MODIFICATION NOTES (TR)
 * - Safety settings (HarmBlockThreshold) içerik politikası için eklenebilir.
 * - Streaming (Server-Sent Events) daha hızlı UX için ileride eklenebilir.
 * - Imagen API (ayrı endpoint) daha yüksek kalite görsel üretimi için eklenebilir.
 * - Genel image-to-text bu modülün kapsamında değildir.
 * - Zorluk: Orta.
 */
public class GeminiAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly GeminiAiOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<GeminiAiService> _logger;

    // ── İstek JSON tipleri ────────────────────────────────────────────────────
    // [TR] Gemini API camelCase bekler; [JsonPropertyName] zorunlu.

    private sealed class GeminiTextRequest
    {
        [JsonPropertyName("contents")] public List<GeminiContent> Contents { get; init; } = [];
        [JsonPropertyName("generationConfig")] public GeminiTextConfig GenerationConfig { get; init; } = new();
    }

    private sealed class GeminiImageRequest
    {
        [JsonPropertyName("contents")] public List<GeminiContent> Contents { get; init; } = [];
        [JsonPropertyName("generationConfig")] public GeminiImageConfig GenerationConfig { get; init; } = new();
    }

    private sealed class GeminiTextConfig
    {
        [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; init; }
        [JsonPropertyName("temperature")] public float Temperature { get; init; }
    }

    private sealed class GeminiImageConfig
    {
        // [TR] responseModalities: IMAGE → Gemini görsel üretir (base64 PNG döner).
        [JsonPropertyName("responseModalities")]
        public List<string> ResponseModalities { get; init; } = ["IMAGE"];
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")] public string Role { get; init; } = "user";
        [JsonPropertyName("parts")] public List<GeminiPart> Parts { get; init; } = [];
    }

    private sealed class GeminiPart
    {
        // [TR] Metin parçaları için text, görsel parçalar için inlineData dolu olur.
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; init; }

        [JsonPropertyName("inlineData")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiInlineData? InlineData { get; init; }
    }

    private sealed class GeminiInlineData
    {
        [JsonPropertyName("mimeType")] public string MimeType { get; init; } = "image/png";
        // [TR] base64 kodlanmış görsel verisi.
        [JsonPropertyName("data")] public string Data { get; init; } = string.Empty;
    }

    // ── Imagen istek/yanıt tipleri ────────────────────────────────────────────
    // [TR] Imagen, Gemini'den farklı bir API formatı kullanır:
    //      - Endpoint: .../{model}:predict  (generateContent değil)
    //      - Yanıt: predictions[].bytesBase64Encoded  (candidates değil)
    //      Kaynak: https://ai.google.dev/api/generate-content#v1beta.models.predict

    private sealed class ImagenRequest
    {
        [JsonPropertyName("instances")]  public List<ImagenInstance>  Instances  { get; init; } = [];
        [JsonPropertyName("parameters")] public ImagenParameters Parameters { get; init; } = new();
    }

    private sealed class ImagenInstance
    {
        [JsonPropertyName("prompt")] public string Prompt { get; init; } = string.Empty;
    }

    private sealed class ImagenParameters
    {
        [JsonPropertyName("sampleCount")]  public int    SampleCount  { get; init; } = 1;
        [JsonPropertyName("aspectRatio")]  public string AspectRatio  { get; init; } = "1:1";
        // [TR] Güvenli çıktı için mevcut (varsayılan: "block_some")
        [JsonPropertyName("safetySetting")] public string SafetySetting { get; init; } = "block_some";
    }

    private sealed class ImagenResponse
    {
        [JsonPropertyName("predictions")] public List<ImagenPrediction>? Predictions { get; init; }
    }

    private sealed class ImagenPrediction
    {
        [JsonPropertyName("bytesBase64Encoded")] public string BytesBase64Encoded { get; init; } = string.Empty;
        [JsonPropertyName("mimeType")]           public string MimeType            { get; init; } = "image/png";
    }

    // ── Yanıt JSON tipleri ────────────────────────────────────────────────────

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; init; }
        [JsonPropertyName("promptFeedback")] public GeminiPromptFeedback? PromptFeedback { get; init; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")] public GeminiContent? Content { get; init; }
        [JsonPropertyName("finishReason")] public string? FinishReason { get; init; }
    }

    private sealed class GeminiPromptFeedback
    {
        [JsonPropertyName("blockReason")] public string? BlockReason { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public GeminiAiService(
        HttpClient http,
        IOptions<GeminiAiOptions> options,
        IWebHostEnvironment env,
        ILogger<GeminiAiService> logger)
    {
        _http = http;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationType == "Visualize" || request.OperationType == "Math")
        {
            var mathChartMode = request.OperationType == "Math";

            var imgModel = string.IsNullOrWhiteSpace(request.ModelName)
                ? _options.ImageModel
                : request.ModelName;

            // [TR] Imagen modelleri Google Cloud billing gerektirir.
            //      Ücretsiz Gemini API key'iyle çalışmaz → anlamlı hata göster.
            if (!string.IsNullOrWhiteSpace(imgModel) &&
                imgModel.Contains("imagen", StringComparison.OrdinalIgnoreCase))
            {
                return await GenerateImageWithImagenAsync(documentTitle, request, imgModel, cancellationToken, mathChartMode);
            }

            // [TR] Gemini görsel üretimi (responseModalities: IMAGE) ücretsiz tier'da sınırlıdır.
            //      Çalışmazsa kullanıcıya HuggingFace FLUX modelini öneririz.
            if (!string.IsNullOrWhiteSpace(imgModel))
                return await GenerateImageAsync(documentTitle, request, imgModel, cancellationToken, mathChartMode);
        }

        return await GenerateTextAsync(documentTitle, request, cancellationToken);
    }

    // ── Metin üretimi ─────────────────────────────────────────────────────────

    private async Task<AiServiceResult> GenerateTextAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(request.ModelName)
            ? _options.DefaultModel
            : request.ModelName;

        var prompt = BuildTextPrompt(documentTitle, request);
        var url = BuildUrl(model);

        // [TR] Multimodal: "Görsel Seç" ile yakalanmış görsel varsa parts'a inlineData
        //      olarak eklenir. Gemini text modelleri (gemini-2.5-flash vb.) görsel + metin
        //      girdisini destekler ve "describe / explain / compare" gibi soruları cevaplar.
        var parts = BuildMultimodalParts(prompt, request);

        var body = new GeminiTextRequest
        {
            Contents = [new GeminiContent { Role = "user", Parts = parts }],
            GenerationConfig = new GeminiTextConfig
            {
                MaxOutputTokens = _options.MaxOutputTokens,
                Temperature = _options.Temperature,
            }
        };

        _logger.LogInformation(
            "Gemini text request. Model: {Model}, Op: {Op}, ImageAttached: {HasImage}",
            model, request.OperationType, !string.IsNullOrWhiteSpace(request.InputImageBase64));

        var response = await SendAsync(url, body, cancellationToken);
        var geminiResp = await ReadResponseAsync(response, cancellationToken);

        // [TR] Uzun cevaplarda kesilme (truncation) sorununu çözmek için:
        //  1) Parts listesinin tamamından metin birleştir (Gemini bazen metni
        //     birden fazla Part'a böler — sadece ilki alınırsa yanıt yarım kalır).
        //  2) finishReason == "MAX_TOKENS" ise kullanıcıyı bilgilendir; cevap
        //     modelin ürettiği kısma kadar döner ama kesildiği açıkça belirtilir.
        //  3) Cevap tamamen boşsa (Gemini başka nedenle kesmişse) anlamlı hata fırlat.
        var candidate = geminiResp?.Candidates?.FirstOrDefault();
        var allTextParts = candidate?.Content?.Parts?
            .Where(p => !string.IsNullOrEmpty(p.Text))
            .Select(p => p.Text!) ?? Enumerable.Empty<string>();
        var text = string.Join("\n", allTextParts).Trim();

        var finishReason = candidate?.FinishReason;
        var truncated = string.Equals(finishReason, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
        {
            if (truncated)
                throw new InvalidOperationException(
                    "Gemini reached the token limit before generating a response. " +
                    "Increase Ai:Gemini:MaxOutputTokens (it is currently too small).");
            throw new InvalidOperationException(
                $"Gemini did not return a valid text response. (finishReason: {finishReason ?? "?"})");
        }

        if (truncated)
        {
            _logger.LogWarning("Gemini response was truncated because of MAX_TOKENS. Model: {Model}", model);
            text += "\n\n[Note: The response may have been truncated because it reached the maximum token limit. " +
                    "For longer output, increase Ai:Gemini:MaxOutputTokens in appsettings.]";
        }

        return new AiServiceResult { OutputText = text };
    }

    // ── Görsel üretimi ────────────────────────────────────────────────────────

    private async Task<AiServiceResult> GenerateImageAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        string imageModel,
        CancellationToken cancellationToken,
        bool mathChartMode)
    {
        var url = BuildUrl(imageModel);

        var prompt = mathChartMode
            ? BuildMathImagePrompt(documentTitle, request)
            : BuildImagePrompt(documentTitle, request);

        // [TR] "Benzer görsel üret" akışı: kullanıcı "Görsel Seç" ile bir bölge yakaladıysa
        //      onu da Gemini'ye referans görsel olarak gönderiyoruz (multimodal).
        var requestParts = BuildMultimodalParts(prompt, request);

        var body = new GeminiImageRequest
        {
            Contents = [new GeminiContent { Role = "user", Parts = requestParts }],
            GenerationConfig = new GeminiImageConfig { ResponseModalities = ["IMAGE"] }
        };

        _logger.LogInformation(
            "Gemini image request. Model: {Model}, ReferenceImage: {HasImage}",
            imageModel, !string.IsNullOrWhiteSpace(request.InputImageBase64));

        var response = await SendAsync(url, body, cancellationToken);
        var geminiResp = await ReadResponseAsync(response, cancellationToken);

        // [TR] Yanıt içindeki tüm part'ları tara; inlineData (görsel) ve text parçalarını ayır.
        var parts = geminiResp?.Candidates?.FirstOrDefault()?.Content?.Parts;
        if (parts == null || parts.Count == 0)
            throw new InvalidOperationException("Gemini image response was empty.");

        // [TR] Görsel parça bul (inlineData dolu olan).
        var imagePart = parts.FirstOrDefault(p => p.InlineData != null);
        if (imagePart?.InlineData == null)
        {
            // [TR] Görsel gelmedi; metin kısmı varsa onu döndür (prompt açıklaması).
            var fallbackText = parts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?.Text ?? "";
            return new AiServiceResult
            {
                OutputText = $"[Image could not be generated - the model returned a text response]\n\n{fallbackText}".Trim()
            };
        }

        // [TR] Base64 görsel verisini wwwroot/ai-images/ klasörüne PNG olarak kaydet.
        var imageUrl = await SaveBase64ImageAsync(imagePart.InlineData, cancellationToken);

        // [TR] Metin parçası varsa (açıklama) onu da döndür.
        var caption = parts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?.Text ?? string.Empty;

        return new AiServiceResult
        {
            OutputText = string.IsNullOrWhiteSpace(caption) ? "Image generated successfully." : caption.Trim(),
            OutputImageUrl = imageUrl
        };
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    private string BuildUrl(string model) =>
        $"{_options.BaseUrl.TrimEnd('/')}/{model}:generateContent?key={_options.ApiKey}";

    private async Task<HttpResponseMessage> SendAsync<T>(string url, T body, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(url, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API connection error.");
            throw new InvalidOperationException(
                "Could not reach the Gemini API. Check the internet connection and API key.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini API error response: {Status} | {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Gemini API error ({(int)response.StatusCode}): {TryParseGeminiError(errorBody)}");
        }

        return response;
    }

    private static async Task<GeminiResponse?> ReadResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var geminiResp = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);

        if (geminiResp?.PromptFeedback?.BlockReason != null)
            throw new InvalidOperationException(
                $"Gemini content policy block: {geminiResp.PromptFeedback.BlockReason}");

        return geminiResp;
    }

    /// <summary>
    /// [TR] Base64 görsel verisini disk'e kaydeder ve göreli URL döner.
    /// wwwroot/ai-images/{guid}.{ext} klasörüne yazılır.
    /// </summary>
    private async Task<string> SaveBase64ImageAsync(GeminiInlineData inlineData, CancellationToken ct)
    {
        var ext = inlineData.MimeType.Contains("jpeg") ? "jpg" : "png";
        var fileName = $"{Guid.NewGuid():N}.{ext}";

        var saveDir = Path.Combine(_env.WebRootPath, "ai-images");
        Directory.CreateDirectory(saveDir);

        var filePath = Path.Combine(saveDir, fileName);
        var bytes = Convert.FromBase64String(inlineData.Data);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        _logger.LogInformation("AI image saved: {File}", filePath);
        return $"/ai-images/{fileName}";
    }

    // ── Imagen görsel üretimi ────────────────────────────────────────────────
    /*
     * [TR] Google Imagen API'sini kullanarak yüksek kaliteli görsel üretir.
     *      Gemini'nin generateContent endpoint'inden FARKLIDIR:
     *        • URL     : {BaseUrl}{model}:predict  ← "predict" kullanılır
     *        • İstek   : {"instances":[{"prompt":"..."}], "parameters":{...}}
     *        • Yanıt   : {"predictions":[{"bytesBase64Encoded":"...","mimeType":"..."}]}
     *
     *      Desteklenen modeller (Gemini API keyiyle çalışır):
     *        • imagen-3.0-generate-002      → en yüksek kalite
     *        • imagen-3.0-fast-generate-001 → hızlı, daha düşük maliyet
     *
     *      JÜRI SORUSU: "Imagen ile Gemini image generation farkı?"
     *      → Imagen: özelleşmiş text-to-image modeli, daha gerçekçi ve detaylı görseller
     *         Gemini image: genel amaçlı model, çok modlu (metin+görsel) kullanım
     */
    private async Task<AiServiceResult> GenerateImageWithImagenAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        string modelId,
        CancellationToken ct,
        bool mathChartMode)
    {
        // [TR] Imagen URL formatı: BaseUrl/{model}:predict
        //      Örnek: .../imagen-3.0-generate-002:predict
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{modelId}:predict?key={_options.ApiKey}";

        var prompt = mathChartMode
            ? BuildMathImagePrompt(documentTitle, request)
            : BuildImagePrompt(documentTitle, request);

        var body = new ImagenRequest
        {
            Instances  = [new ImagenInstance { Prompt = prompt }],
            Parameters = new ImagenParameters { SampleCount = 1, AspectRatio = "1:1" }
        };

        _logger.LogInformation("Imagen image request. Model: {Model}", modelId);

        var response = await _http.PostAsJsonAsync(url, body, ct);
        var raw      = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Imagen API error {Status}: {Body}", (int)response.StatusCode, raw);

            // [TR] Imagen modelleri ücretsiz Gemini API key'iyle çalışmaz (Vertex AI billing gerekir).
            //      Kullanıcıya anlamlı bir yönlendirme mesajı döndürülür; uygulama çökmez.
            if ((int)response.StatusCode == 404)
                throw new InvalidOperationException(
                    "Imagen models cannot be used with the free Google AI Studio API key. " +
                    "Google Cloud + Vertex AI billing is required. " +
                    "Please select a HuggingFace model such as 'FLUX.1 Schnell' or 'Stable Diffusion XL'.");

            throw new InvalidOperationException(
                $"Gemini Imagen API error ({(int)response.StatusCode}): {raw}");
        }

        // [TR] predictions[0].bytesBase64Encoded → base64 PNG/JPEG
        var imagenResp = JsonSerializer.Deserialize<ImagenResponse>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var prediction = imagenResp?.Predictions?.FirstOrDefault();
        if (prediction == null || string.IsNullOrWhiteSpace(prediction.BytesBase64Encoded))
            throw new InvalidOperationException("Imagen image response was empty.");

        var imageUrl = await SaveBase64ImageAsync(
            new GeminiInlineData { MimeType = prediction.MimeType, Data = prediction.BytesBase64Encoded }, ct);

        return new AiServiceResult
        {
            OutputText     = $"Image generated successfully (Google Imagen - {modelId}).",
            OutputImageUrl = imageUrl
        };
    }

    // ── Multimodal parts oluşturucu ──────────────────────────────────────────
    /// <summary>
    /// [TR] Gemini "contents.parts" listesini hazırlar. Eğer istek içinde
    ///      "Görsel Seç" ile yakalanmış base64 PNG varsa parts'a inlineData
    ///      bir parça eklenir; aksi halde sadece metin parçası döner.
    ///
    ///      JÜRI MODİFİKASYON NOTU (TR):
    ///      - Birden fazla görselin desteklenmesi için req'e List eklenir ve
    ///        burada her biri için ayrı GeminiPart eklenir.
    ///      - Görsel boyut limitleri için sunucu tarafında kontrol eklenebilir
    ///        (Gemini ~ 20MB toplam istek limitine sahiptir).
    /// </summary>
    private static List<GeminiPart> BuildMultimodalParts(string prompt, AiProcessRequestViewModel req)
    {
        var parts = new List<GeminiPart> { new() { Text = prompt } };

        if (!string.IsNullOrWhiteSpace(req.InputImageBase64))
        {
            // [TR] Bazı istemciler base64'ü "data:image/png;base64,..." şeklinde gönderebilir;
            //      Gemini sadece ham base64 kabul eder, prefix'i temizleyelim.
            var raw = req.InputImageBase64.Trim();
            var commaIdx = raw.IndexOf(',');
            if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx > 0)
                raw = raw[(commaIdx + 1)..];

            parts.Add(new GeminiPart
            {
                InlineData = new GeminiInlineData
                {
                    MimeType = string.IsNullOrWhiteSpace(req.InputImageMimeType)
                        ? "image/png"
                        : req.InputImageMimeType!,
                    Data = raw,
                }
            });
        }

        return parts;
    }

    // ── Prompt oluşturucular ──────────────────────────────────────────────────

    /// <summary>[TR] Metin işleme operasyonları için prompt oluşturur.</summary>
    private static string BuildTextPrompt(string documentTitle, AiProcessRequestViewModel req)
    {
        var text  = req.InputText?.Trim() ?? string.Empty;
        var style = req.Style?.Trim() ?? "Formal";
        var instr = req.CustomInstruction?.Trim() ?? string.Empty;
        var tgt   = req.TargetLanguage ?? "Turkish";
        var hasImage = !string.IsNullOrWhiteSpace(req.InputImageBase64);

        // [TR] "Görsel Seç" ile sadece görsel gönderildiğinde (metin yok) prompt'u
        //      görseli açıklamaya / kullanıcı yönergesini takip etmeye yönlendir.
        //      Operation tipine göre varsayılan davranış belirlenir; kullanıcının
        //      yazdığı CustomInstruction her zaman ek talimat olarak eklenir.
        if (hasImage && string.IsNullOrWhiteSpace(text))
        {
            var imageInstruction = !string.IsNullOrWhiteSpace(instr)
                ? instr
                : req.OperationType switch
                {
                    "Translate"     => $"Detect the text in the image and translate it into {tgt}. Write only the translations.",
                    "Summarize"     => "Write a short summary of what you see in the image.",
                    "Rewrite"       => string.IsNullOrWhiteSpace(instr)
                        ? "Extract the text from the image and rewrite it clearly. Output ONLY the rewritten text — no commentary."
                        : $"Extract the text from the image and rewrite it as follows: {instr}. Output ONLY the rewritten text — no commentary or explanation.",
                    "CreativeWrite" => string.IsNullOrWhiteSpace(instr)
                        ? $"Extract the text or subject from the image and write a creative {style} reworking of it. Output ONLY the creative text."
                        : $"Extract the text or subject from the image and creatively transform it as follows: {instr}. Keep the image content as the base; weave the instruction into a rewrite of that content. Output ONLY the creative text.",
                    "Explanation"   => BuildExplanationInstruction(),
                    "Math" =>
                        """
                        Analyze the attached image for mathematical content (graphs, formulas, tables).
                        Briefly explain axes, curves, or numeric patterns in Turkish.
                        Note: For generating a new chart image the user should pick Gemini image model or Wolfram Alpha.
                        """,
                    _               => "Explain this image in detail."
                };

            var imagePrompt = $"""
                Analyze the following image and follow this instruction:
                {imageInstruction}

                (Context - Document: {documentTitle})
                """;
            return AppendLanguageDirective(imagePrompt, req.OperationType, tgt);
        }

        var textPrompt = req.OperationType switch
        {
            "Translate" =>
                $"""
                You are a professional translation assistant. Translate the following text into {tgt}.
                Translation style: {style} (Formal = official, Academic = academic, Simplified = plain).
                Write only the translated text; do not add explanations.

                Source text (Document: {documentTitle}):
                {text}
                """,

            "Summarize" =>
                $"""
                Summarize the following text while preserving the main points. The summary should be short, clear, and understandable.
                Write only the summary; do not add extra explanations.

                Source text (Document: {documentTitle}):
                {text}
                """,

            "Rewrite" =>
                $"""
                You are a professional rewriting assistant. REWRITE the source text below — do not comment on it, explain it, analyze it, or discuss what it says.

                Rules:
                - Output ONLY the rewritten text. No introductions, no commentary, no notes about changes, no "here is the rewrite" phrases.
                - Do not quote the original and then add remarks. Do not summarize or critique the source.
                - Follow the user instruction. If none is given, rewrite in {style} style to be clearer and more fluent.
                - Preserve the core meaning and facts unless the instruction explicitly asks to change them.
                - The rewrite may be shorter or longer than the original as needed.

                User instruction: {(string.IsNullOrWhiteSpace(instr) ? $"(none — rewrite in {style} style)" : instr)}

                Source text (Document: {documentTitle}):
                {text}
                """,

            "CreativeWrite" =>
                $"""
                You are a creative writing assistant. Creatively TRANSFORM the source text below — do not ignore it and write unrelated new content.

                Rules:
                - The output must be a creative reworking of the source: keep its subject, context, and core ideas, then reshape them as instructed.
                - If the user asks to add themes or topics (e.g. "include cats and dogs"), weave them into a creative rewrite OF the source — do not replace the source with a brand-new piece on only that theme.
                - Style: {style}. Output ONLY the creative text — no explanations or meta-commentary.
                - Apply the user instruction to HOW you transform the source, not as a standalone writing topic that ignores the source.

                User instruction: {(string.IsNullOrWhiteSpace(instr) ? "(none — creatively expand and reimagine the source while keeping its subject)" : instr)}

                Source text (Document: {documentTitle}):
                {text}
                """,

            // [TR] "Explanation": metni (ve isteğe bağlı görseli) analiz eder.
            //      İçerik tipi tespiti + detaylı açıklama + anlamlı çıkarımlar.
            //      Multimodal akışta görsel parts'a inlineData olarak ek olarak eklenir;
            //      bu prompt hem yalnız metin hem metin+görsel için çalışır.
            "Explanation" =>
                $"""
                {BuildExplanationInstruction()}

                If the user wrote a custom instruction, prioritize it in your answer:
                "{(string.IsNullOrWhiteSpace(instr) ? "(no instruction)" : instr)}"

                Content to analyze (Document: {documentTitle}):
                {(string.IsNullOrWhiteSpace(text) ? "(image only - no text provided)" : text)}
                """,

            // [TR] Metin-tabanlı Math fallback — doğru model seçilmezse kullanıcıya yönlendirme + özet.
            "Math" =>
                $"""
                This operation normally generates a chart or mathematical visual.
                For visual output, select "Gemini 3.1 Flash Image Preview" as the model or use Wolfram Alpha.

                For now, summarize the following content mathematically and state which chart type would be suitable.

                Custom instruction: {(string.IsNullOrWhiteSpace(instr) ? "(none)" : instr)}

                Content (Document: {documentTitle}):
                {(string.IsNullOrWhiteSpace(text) ? "(no text - only the image was sent multimodally)" : text)}
                """,

            _ => $"Process the following text:\n\n{text}"
        };

        return AppendLanguageDirective(textPrompt, req.OperationType, tgt);
    }

    /// <summary>
    /// [TR] Çeviri dışındaki TÜM işlemlerde, kullanıcının seçtiği hedef dilde cevap
    /// üretilmesini zorunlu kılar. Kullanıcı prompt'u kendi dilinde yazsa bile cevap
    /// seçilen dilde döner. "Auto" / boş seçildiğinde dil dayatılmaz (kaynak diliyle yanıt).
    /// Translate işlemi zaten {tgt} diline çevirdiği için yönerge eklenmez.
    /// </summary>
    private static string AppendLanguageDirective(string prompt, string? operationType, string? targetLanguage)
    {
        if (string.Equals(operationType, "Translate", StringComparison.OrdinalIgnoreCase))
            return prompt;
        if (string.IsNullOrWhiteSpace(targetLanguage) ||
            string.Equals(targetLanguage, "Auto", StringComparison.OrdinalIgnoreCase))
            return prompt;

        return prompt +
            $"\n\nIMPORTANT: Write your entire answer in {targetLanguage}, regardless of the language of the source text or the instruction.";
    }

    /// <summary>
    /// [TR] "Explanation" işlemi için ortak yönerge bloğu. Hem yalnız-görsel hem
    /// metin+görsel akışlarında aynı analiz yapısını verir:
    ///   1) içerik tipi tespiti, 2) detaylı analiz, 3) anlamlı çıkarımlar.
    /// </summary>
    private static string BuildExplanationInstruction() =>
        """
        You are an analytical assistant. Review the provided content (text and/or image) and clearly explain what it is.

        Structure your answer in three plain-text sections. Put each section title on its own line, then write normal sentences underneath (no Markdown):

        Content type:
        Identify what kind of content this is (chart, photograph, article, story, table, code, list, formula, etc.).

        Detailed analysis:
        Describe concretely what appears in the content. For visuals: subjects, labels, axes, values, colors. For text: topic, key facts, people, dates, numbers, structure. If there is tabular or numeric data, summarize groups and counts in normal sentences.

        Insights:
        Add useful context: purpose, related topics, patterns, trends, or notable points. Mark speculation clearly.

        Formatting rules (IMPORTANT):
        - Plain text only. Do NOT use Markdown symbols: no # headers, no * or ** for bullets or bold, no backticks, no --- dividers.
        - Write section titles as plain words on their own line, then normal paragraphs below.
        - Prefer flowing sentences; if you must list items, use numbered lines (1. 2. 3.) without asterisks or hash marks.
        - If the user gave a custom instruction, follow it while keeping the answer in this plain, readable format.
        """;

    /// <summary>
    /// [TR] Görsel üretim için kısa ve etkili İngilizce prompt oluşturur.
    /// Gemini görsel modelleri kısa, İngilizce prompt'larla daha iyi sonuç verir.
    ///
    /// [TR] Eğer kullanıcı "Görsel Seç" ile referans bir görsel iliştirdiyse,
    ///      prompt "create a similar/derived image" tarzında düzenlenir; metin
    ///      ek bağlam olarak verilir. Aksi halde mevcut metin tabanlı akış çalışır.
    /// </summary>
    private static string BuildImagePrompt(string documentTitle, AiProcessRequestViewModel req)
    {
        var text  = req.InputText?.Trim() ?? string.Empty;
        var instr = req.CustomInstruction?.Trim() ?? string.Empty;
        var hasImage = !string.IsNullOrWhiteSpace(req.InputImageBase64);
        var excerpt = text.Length > 500 ? text[..500] + "..." : text;

        if (hasImage)
        {
            var userInstruction = string.IsNullOrWhiteSpace(instr)
                ? "Generate a new image inspired by the attached reference image. Keep the overall composition and subject, but improve quality and style."
                : instr;

            return $"""
                You are given a reference image (attached) along with the following instructions.
                Use the reference image as inspiration and produce a new high-quality image.

                Instructions: {userInstruction}

                Optional context (text excerpt from document "{documentTitle}"):
                {excerpt}
                """;
        }

        return $"""
            Create a vivid, detailed illustration inspired by the following text excerpt.
            Style: photorealistic or detailed digital art. Include relevant objects, scenery, and atmosphere.
            Source document: {documentTitle}

            User's extra visual instruction (follow this with priority if present):
            {(string.IsNullOrWhiteSpace(instr) ? "(none)" : instr)}

            Text:
            {excerpt}
            """;
    }

    /// <summary>
    /// [TR] Math işlemi: bar grafik, fonksiyon grafiği, eksen etiketli bilimsel şema üretimi için İngilizce görsel prompt.
    /// </summary>
    private static string BuildMathImagePrompt(string documentTitle, AiProcessRequestViewModel req)
    {
        var text = req.InputText?.Trim() ?? string.Empty;
        var instr = req.CustomInstruction?.Trim() ?? string.Empty;
        var hasImage = !string.IsNullOrWhiteSpace(req.InputImageBase64);
        var excerpt = text.Length > 1200 ? text[..1200] + "..." : text;

        const string rules =
            """
            Produce ONE clean STEM-quality figure:
            - Bar/column/line/scatter/pie: labeled axes (x,y when relevant), sensible numeric ticks, short title,
              legend if multiple series; optional light grid.
            - Function plots y=f(x): Cartesian axes, smooth curve over a reasonable domain/range implied by the problem,
              equation label when helpful.
            - Honor numeric data / formulas accurately when provided.
            - White background, high legibility.
            """;

        if (hasImage)
        {
            var userInstruction = string.IsNullOrWhiteSpace(instr)
                ? "Infer quantitative relationships from the attached reference image and render an accurate chart or plot."
                : instr;

            return $"""
                {rules}

                Priority instructions:
                {userInstruction}

                A reference image is attached — extract visible numbers/categories or curve shapes when possible.

                Document title (context): {documentTitle}

                Supplementary text / OCR excerpt:
                {excerpt}
                """;
        }

        return $"""
            {rules}

            Priority instructions:
            {(string.IsNullOrWhiteSpace(instr) ? "Infer the requested visualization from the excerpt below." : instr)}

            Document title (context): {documentTitle}

            Data, equations, table excerpt or word problem:
            {excerpt}
            """;
    }

    /// <summary>
    /// Tek kullanıcı mesajıyla kısa metin üretir (örn. Wolfram sorgu planlama).
    /// </summary>
    public async Task<string> GenerateSimpleTextAsync(
        string? modelOverride,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Gemini API key is not configured.");

        var model = string.IsNullOrWhiteSpace(modelOverride) ? _options.DefaultModel : modelOverride.Trim();
        var url = BuildUrl(model);

        var body = new GeminiTextRequest
        {
            Contents =
            [
                new GeminiContent { Role = "user", Parts = [new GeminiPart { Text = userPrompt }] }
            ],
            GenerationConfig = new GeminiTextConfig
            {
                MaxOutputTokens = Math.Min(2048, Math.Max(256, _options.MaxOutputTokens)),
                Temperature = 0.12f
            }
        };

        var response = await SendAsync(url, body, cancellationToken);
        var geminiResp = await ReadResponseAsync(response, cancellationToken);

        var candidate = geminiResp?.Candidates?.FirstOrDefault();
        var allTextParts = candidate?.Content?.Parts?
            .Where(p => !string.IsNullOrEmpty(p.Text))
            .Select(p => p.Text!) ?? Enumerable.Empty<string>();
        var text = string.Join("\n", allTextParts).Trim();

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini returned empty text (planner).");

        return text;
    }

    private static string TryParseGeminiError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? body;
        }
        catch { /* ignore */ }
        return body.Length > 300 ? body[..300] + "..." : body;
    }
}
