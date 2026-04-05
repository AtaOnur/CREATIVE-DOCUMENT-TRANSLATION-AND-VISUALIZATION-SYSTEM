namespace pdf_bitirme.Models.Entities;

/*
 * [TR] Bu dosya ne işe yarar: AI işlem çıktısını (metin/görsel URL) ve kullanılan parametreleri saklar.
 * [TR] Neden gerekli: Sonuç sayfasında operasyon bilgisi, model/stil ve üretim çıktısı tekrar gösterilebilsin.
 * [TR] İlgili: AppDbContext, AiController, DocumentService
 *
 * MODIFICATION NOTES (TR)
 * - Yan yana model karşılaştırması için parent/variant alanları eklenebilir.
 * - Çeviri kalite puanı ve akademik atıf modu alanları eklenebilir.
 * - Overlay export metadata'sı ileride tutulabilir.
 * - Notebook başlığı ve kullanıcı notu alanları eklendi.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Orta.
 */
public class AiResult
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string UserEmail { get; set; } = string.Empty;

    public string OperationType { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string CustomInstruction { get; set; } = string.Empty;
    public int SourcePageNumber { get; set; }
    public string NoteTitle { get; set; } = string.Empty;
    public string UserNote { get; set; } = string.Empty;

    public string InputText { get; set; } = string.Empty;
    public string OutputText { get; set; } = string.Empty;
    public string OutputImageUrl { get; set; } = string.Empty;
    public bool IsSaved { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

