namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Workspace AI panelinden gelen işlem isteği modeli.
 * [TR] Neden gerekli: Controller'a tek JSON gövdesi ile operasyon, model, stil, dil ve yönerge taşınır.
 * [TR] İlgili: AiController.Process, pdf-workspace.mjs
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte top-p/temperature gibi üretim parametreleri eklenebilir.
 * - Çoklu giriş metni (batch) desteği eklenebilir.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
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
}

