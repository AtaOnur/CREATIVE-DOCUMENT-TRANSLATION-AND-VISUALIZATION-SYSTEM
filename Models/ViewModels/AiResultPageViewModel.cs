namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: AI sonuç sayfası için okunabilir gösterim modeli.
 * [TR] Neden gerekli: İşlem türü, model/stil, yönerge ve çıktı tek sayfada savunmaya uygun gösterilir.
 * [TR] İlgili: AiController.Result, Views/Ai/Result.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Yan yana sonuç karşılaştırma alanı eklenebilir.
 * - Akademik citation mode çıktısı için ek alanlar eklenebilir.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Kolay.
 */
public class AiResultPageViewModel
{
    public Guid AiResultId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;

    public string OperationType { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public int SourcePageNumber { get; set; }
    public string Style { get; set; } = string.Empty;
    public string CustomInstruction { get; set; } = string.Empty;

    public string InputText { get; set; } = string.Empty;
    public string OutputText { get; set; } = string.Empty;
    public string OutputImageUrl { get; set; } = string.Empty;
    public bool IsSaved { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

