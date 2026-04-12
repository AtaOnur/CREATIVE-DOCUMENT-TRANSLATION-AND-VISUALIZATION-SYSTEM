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
        if (request.OperationType == "Visualize")
        {
            var imgModel = string.IsNullOrWhiteSpace(request.ModelName)
                ? _options.ImageModel
                : request.ModelName;

            // [TR] Imagen modelleri Google Cloud billing gerektirir.
            //      Ücretsiz Gemini API key'iyle çalışmaz → anlamlı hata göster.
            if (!string.IsNullOrWhiteSpace(imgModel) &&
                imgModel.Contains("imagen", StringComparison.OrdinalIgnoreCase))
            {
                return await GenerateImageWithImagenAsync(documentTitle, request, imgModel, cancellationToken);
            }

            // [TR] Gemini görsel üretimi (responseModalities: IMAGE) ücretsiz tier'da sınırlıdır.
            //      Çalışmazsa kullanıcıya HuggingFace FLUX modelini öneririz.
            if (!string.IsNullOrWhiteSpace(imgModel))
                return await GenerateImageAsync(documentTitle, request, imgModel, cancellationToken);
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

        var body = new GeminiTextRequest
        {
            Contents = [new GeminiContent { Role = "user", Parts = [new GeminiPart { Text = prompt }] }],
            GenerationConfig = new GeminiTextConfig
            {
                MaxOutputTokens = _options.MaxOutputTokens,
                Temperature = _options.Temperature,
            }
        };

        _logger.LogInformation("Gemini metin isteği. Model: {Model}, Op: {Op}", model, request.OperationType);

        var response = await SendAsync(url, body, cancellationToken);
        var geminiResp = await ReadResponseAsync(response, cancellationToken);

        var text = geminiResp?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini geçerli bir metin yanıtı döndürmedi.");

        return new AiServiceResult { OutputText = text.Trim() };
    }

    // ── Görsel üretimi ────────────────────────────────────────────────────────

    private async Task<AiServiceResult> GenerateImageAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        string imageModel,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(imageModel);

        var prompt = BuildImagePrompt(documentTitle, request);

        var body = new GeminiImageRequest
        {
            Contents = [new GeminiContent { Role = "user", Parts = [new GeminiPart { Text = prompt }] }],
            GenerationConfig = new GeminiImageConfig { ResponseModalities = ["IMAGE"] }
        };

        _logger.LogInformation("Gemini görsel isteği. Model: {Model}", imageModel);

        var response = await SendAsync(url, body, cancellationToken);
        var geminiResp = await ReadResponseAsync(response, cancellationToken);

        // [TR] Yanıt içindeki tüm part'ları tara; inlineData (görsel) ve text parçalarını ayır.
        var parts = geminiResp?.Candidates?.FirstOrDefault()?.Content?.Parts;
        if (parts == null || parts.Count == 0)
            throw new InvalidOperationException("Gemini görsel yanıtı boş geldi.");

        // [TR] Görsel parça bul (inlineData dolu olan).
        var imagePart = parts.FirstOrDefault(p => p.InlineData != null);
        if (imagePart?.InlineData == null)
        {
            // [TR] Görsel gelmedi; metin kısmı varsa onu döndür (prompt açıklaması).
            var fallbackText = parts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?.Text ?? "";
            return new AiServiceResult
            {
                OutputText = $"[Görsel üretilemedi — Model metin yanıtı döndürdü]\n\n{fallbackText}".Trim()
            };
        }

        // [TR] Base64 görsel verisini wwwroot/ai-images/ klasörüne PNG olarak kaydet.
        var imageUrl = await SaveBase64ImageAsync(imagePart.InlineData, cancellationToken);

        // [TR] Metin parçası varsa (açıklama) onu da döndür.
        var caption = parts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?.Text ?? string.Empty;

        return new AiServiceResult
        {
            OutputText = string.IsNullOrWhiteSpace(caption) ? "Görsel başarıyla üretildi." : caption.Trim(),
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
            _logger.LogError(ex, "Gemini API bağlantı hatası.");
            throw new InvalidOperationException(
                "Gemini API'ye ulaşılamadı. İnternet bağlantısını ve API anahtarını kontrol edin.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini API hata yanıtı: {Status} | {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Gemini API hatası ({(int)response.StatusCode}): {TryParseGeminiError(errorBody)}");
        }

        return response;
    }

    private static async Task<GeminiResponse?> ReadResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var geminiResp = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);

        if (geminiResp?.PromptFeedback?.BlockReason != null)
            throw new InvalidOperationException(
                $"Gemini içerik politikası engeli: {geminiResp.PromptFeedback.BlockReason}");

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

        _logger.LogInformation("AI görseli kaydedildi: {File}", filePath);
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
        CancellationToken ct)
    {
        // [TR] Imagen URL formatı: BaseUrl/{model}:predict
        //      Örnek: .../imagen-3.0-generate-002:predict
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{modelId}:predict?key={_options.ApiKey}";

        var prompt = BuildImagePrompt(documentTitle, request);

        var body = new ImagenRequest
        {
            Instances  = [new ImagenInstance { Prompt = prompt }],
            Parameters = new ImagenParameters { SampleCount = 1, AspectRatio = "1:1" }
        };

        _logger.LogInformation("Imagen görsel isteği. Model: {Model}", modelId);

        var response = await _http.PostAsJsonAsync(url, body, ct);
        var raw      = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Imagen API hatası {Status}: {Body}", (int)response.StatusCode, raw);

            // [TR] Imagen modelleri ücretsiz Gemini API key'iyle çalışmaz (Vertex AI billing gerekir).
            //      Kullanıcıya anlamlı bir yönlendirme mesajı döndürülür; uygulama çökmez.
            if ((int)response.StatusCode == 404)
                throw new InvalidOperationException(
                    "Imagen modelleri ücretsiz Google AI Studio API anahtarıyla kullanılamaz. " +
                    "Google Cloud + Vertex AI faturalandırması gerektirir. " +
                    "Lütfen 'FLUX.1 Schnell' veya 'Stable Diffusion XL' gibi HuggingFace modellerini seçin.");

            throw new InvalidOperationException(
                $"Gemini Imagen API hatası ({(int)response.StatusCode}): {raw}");
        }

        // [TR] predictions[0].bytesBase64Encoded → base64 PNG/JPEG
        var imagenResp = JsonSerializer.Deserialize<ImagenResponse>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var prediction = imagenResp?.Predictions?.FirstOrDefault();
        if (prediction == null || string.IsNullOrWhiteSpace(prediction.BytesBase64Encoded))
            throw new InvalidOperationException("Imagen görsel yanıtı boş geldi.");

        var imageUrl = await SaveBase64ImageAsync(
            new GeminiInlineData { MimeType = prediction.MimeType, Data = prediction.BytesBase64Encoded }, ct);

        return new AiServiceResult
        {
            OutputText     = $"Görsel başarıyla üretildi (Google Imagen — {modelId}).",
            OutputImageUrl = imageUrl
        };
    }

    // ── Prompt oluşturucular ──────────────────────────────────────────────────

    /// <summary>[TR] Metin işleme operasyonları için prompt oluşturur.</summary>
    private static string BuildTextPrompt(string documentTitle, AiProcessRequestViewModel req)
    {
        var text  = req.InputText?.Trim() ?? string.Empty;
        var style = req.Style?.Trim() ?? "Formal";
        var instr = req.CustomInstruction?.Trim() ?? string.Empty;
        var tgt   = req.TargetLanguage ?? "Turkish";

        return req.OperationType switch
        {
            "Translate" =>
                $"""
                Sen profesyonel bir çeviri asistanısın. Aşağıdaki metni {tgt} diline çevir.
                Çeviri stili: {style} (Formal = resmi, Academic = akademik, Simplified = sade).
                Sadece çevrilmiş metni yaz, açıklama ekleme.

                Kaynak metin (Belge: {documentTitle}):
                {text}
                """,

            "Summarize" =>
                $"""
                Aşağıdaki metni ana noktaları koruyarak özetle. Özet kısa, net ve anlaşılır olmalıdır.
                Sadece özeti yaz, ek açıklama ekleme.

                Kaynak metin (Belge: {documentTitle}):
                {text}
                """,

            "Rewrite" =>
                $"""
                Aşağıdaki metni şu talimata göre yeniden yaz: "{instr}"
                Talimat yoksa metni daha akıcı ve anlaşılır hale getir.
                Sadece yeniden yazılmış metni yaz.

                Orijinal metin (Belge: {documentTitle}):
                {text}
                """,

            "CreativeWrite" =>
                $"""
                Aşağıdaki metinden ilham alarak {style} tarzında yaratıcı bir metin yaz.
                Özgün ve akıcı bir dille yaz. Sadece yaratıcı metni yaz.

                İlham kaynağı (Belge: {documentTitle}):
                {text}
                """,

            _ => $"Aşağıdaki metni işle:\n\n{text}"
        };
    }

    /// <summary>
    /// [TR] Görsel üretim için kısa ve etkili İngilizce prompt oluşturur.
    /// Gemini görsel modelleri kısa, İngilizce prompt'larla daha iyi sonuç verir.
    /// </summary>
    private static string BuildImagePrompt(string documentTitle, AiProcessRequestViewModel req)
    {
        var text = req.InputText?.Trim() ?? string.Empty;
        // [TR] Maksimum 500 karakter — görsel model uzun metin işlemez.
        var excerpt = text.Length > 500 ? text[..500] + "..." : text;

        return $"""
            Create a vivid, detailed illustration inspired by the following text excerpt.
            Style: photorealistic or detailed digital art. Include relevant objects, scenery, and atmosphere.
            Source document: {documentTitle}

            Text:
            {excerpt}
            """;
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
