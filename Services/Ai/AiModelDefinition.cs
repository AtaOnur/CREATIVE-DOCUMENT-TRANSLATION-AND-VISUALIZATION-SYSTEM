namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: Her AI modelini tanımlayan yapılandırma sınıfı.
 * [TR] Neden gerekli: Hangi modelin hangi task'leri desteklediğini ve hangi sağlayıcıyı
 *      kullandığını merkezi olarak tanımlamak için. UI buna göre model listesini filtreler.
 * [TR] İlgili: AiOptions, MultiProviderAiService, AiController.ModelsForTask
 *
 * MODIFICATION NOTES (TR)
 * - Yeni model eklemek: appsettings.json → Ai:Models dizisine yeni bir nesne eklenir.
 * - Yeni sağlayıcı eklemek: Provider alanına yeni bir değer (örn. "OpenAI") girilir,
 *   MultiProviderAiService'de ilgili dal eklenir.
 * - Yeni task tipi eklemek: Tasks dizisine yeni değer eklenir, AiController ve UI güncellenir.
 * - Zorluk: Kolay.
 *
 * JÜRI MODİFİKASYON NOTLARI (TR)
 * - "Neden bazı modeller bazı task'lerde yok?" → Tasks dizisi bu dosyada yönetilir.
 * - "OpenAI eklenebilir mi?" → Provider="OpenAI" eklenir, MultiProviderAiService genişletilir.
 */
public class AiModelDefinition
{
    /// <summary>
    /// Modelin API kimliği. Gemini için "gemini-2.5-flash",
    /// HuggingFace için "facebook/bart-large-cnn" gibi.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>UI'da gösterilecek kullanıcı dostu isim.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// API sağlayıcısı. "Gemini", "HuggingFace" veya "Groq".
    /// MultiProviderAiService bu değere göre doğru servise yönlendirir.
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>
    /// Bu modelin desteklediği task tipleri.
    /// Olası değerler: "Translate", "Summarize", "Rewrite", "CreativeWrite", "Visualize"
    /// UI, seçilen işleme göre sadece bu task'i destekleyen modelleri gösterir.
    /// </summary>
    public List<string> Tasks { get; set; } = [];
}

/// <summary>
/// Hugging Face API bağlantı ayarları.
/// appsettings.json → Ai:HuggingFace bölümünden okunur.
/// </summary>
public class HuggingFaceApiOptions
{
    /// <summary>
    /// HuggingFace API anahtarı.
    /// https://huggingface.co/settings/tokens adresinden ücretsiz alınır.
    /// Ücretsiz hesap ile çoğu model kullanılabilir; rate limit uygulanır.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>HuggingFace Inference API temel URL'si.</summary>
    public string BaseUrl { get; set; } = "https://api-inference.huggingface.co/models/";

    /// <summary>
    /// API istek zaman aşımı (saniye).
    /// HuggingFace ücretsiz modellerinde "cold start" 20-30 saniye sürebilir.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Groq API bağlantı ayarları.
/// appsettings.json → Ai:Groq bölümünden okunur.
/// Ücretsiz API anahtarı: https://console.groq.com/keys
/// </summary>
public class GroqApiOptions
{
    /// <summary>
    /// Groq API anahtarı. console.groq.com adresinden ücretsiz alınır.
    /// Kredi kartı gerekmez; çok yüksek ücretsiz rate limit sunar.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Groq chat completions endpoint.
    /// OpenAI uyumlu formattır; Authorization: Bearer {ApiKey} kullanılır.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/chat/completions";

    /// <summary>İstek zaman aşımı (saniye). Groq genellikle 1-3 saniyede yanıt verir.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Stability AI (api.stability.ai) bağlantı ayarları.
/// Ücretsiz API anahtarı: https://platform.stability.ai/account/credits
/// 25 ücretsiz kredi/ay → yaklaşık 25 Stable Image Core görseli.
/// </summary>
public class StabilityApiOptions
{
    /// <summary>
    /// Stability AI API anahtarı.
    /// https://platform.stability.ai → Sign In → API Keys → Create Key
    /// Kredi kartı gerektirmez; 25 ücretsiz kredi/ay verilir.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Stability AI v2beta temel URL'si.</summary>
    public string BaseUrl { get; set; } = "https://api.stability.ai/v2beta/stable-image/generate";

    /// <summary>İstek zaman aşımı (saniye). Stable Diffusion 30-60 saniye sürebilir.</summary>
    public int TimeoutSeconds { get; set; } = 90;
}

/// <summary>
/// Ai bölümünün tüm yapılandırması.
/// appsettings.json → Ai bölümünden okunur.
/// </summary>
public class AiOptions
{
    /// <summary>
    /// Tüm AI modelleri ve destekledikleri task'ler.
    /// UI bu listeyi task'e göre filtreler.
    /// </summary>
    public List<AiModelDefinition> Models { get; set; } = [];

    /// <summary>HuggingFace API bağlantı ayarları.</summary>
    public HuggingFaceApiOptions HuggingFace { get; set; } = new();

    /// <summary>Groq API bağlantı ayarları.</summary>
    public GroqApiOptions Groq { get; set; } = new();

    /// <summary>Stability AI bağlantı ayarları.</summary>
    public StabilityApiOptions Stability { get; set; } = new();
}
