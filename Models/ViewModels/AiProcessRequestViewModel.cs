namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Workspace AI panelinden gelen işlem isteği modeli.
 * [TR] Neden gerekli: Controller'a tek JSON gövdesi ile operasyon, model, stil, dil,
 *      yönerge ve (opsiyonel) multimodal görsel girdisi taşınır.
 * [TR] İlgili: AiController.Process, pdf-workspace.mjs, GeminiAiService
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte top-p/temperature gibi üretim parametreleri eklenebilir.
 * - Çoklu giriş metni (batch) desteği eklenebilir.
 * - InputImageBase64: "Görsel Seç" ile yakalanmış PDF bölgesinin PNG'si (data: prefix'siz).
 *   Gemini multimodal modellerine inlineData olarak iletilir.
 * - Zorluk: Kolay.
 */
public class AiProcessRequestViewModel
{
    public Guid DocumentId { get; set; }
    public string OperationType { get; set; } = "Translate";
    public string ModelName { get; set; } = "mock-gpt";

    public string SourceLanguage { get; set; } = "Turkish";
    public string TargetLanguage { get; set; } = "English";
    public string Style { get; set; } = "Formal"; // Formal / Academic / Simplified

    public string CustomInstruction { get; set; } = string.Empty;
    public string InputText { get; set; } = string.Empty;
    public int SourcePageNumber { get; set; } = 1;

    // ─── Multimodal görsel girdi (opsiyonel) ─────────────────────────────────
    // [TR] "Görsel Seç" butonu ile PDF bölgesinden yakalanan PNG.
    //      Sadece Gemini multimodal modellerinde kullanılır; null ise yalnız metin akışı çalışır.
    public string? InputImageBase64 { get; set; }
    public string? InputImageMimeType { get; set; } = "image/png";
}

