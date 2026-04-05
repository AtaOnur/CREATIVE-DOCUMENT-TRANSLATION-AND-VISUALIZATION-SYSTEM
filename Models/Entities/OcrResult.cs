namespace pdf_bitirme.Models.Entities;

/*
 * [TR] Bu dosya ne işe yarar: PDF içindeki seçili bölgeden üretilen OCR sonucunu kalıcı olarak saklar.
 * [TR] Neden gerekli: Kullanıcı metni düzenleyebilsin ve yeniden açtığında son OCR metni görülsün.
 * [TR] İlgili: AppDbContext, DocumentService, DocumentsController OCR action'ları
 *
 * MODIFICATION NOTES (TR)
 * - Güven skoru (confidence) alanı ileride eklenebilir.
 * - Çoklu bölge için regionGroupId veya sıra alanı eklenebilir.
 * - Tam sayfa OCR için isFullPage alanı eklenebilir.
 * - Bu sürümde OCR yalnızca PDF içindeki seçilen bölgeye uygulanmaktadır.
 * - Genel resimden metin çıkarma future work olarak bırakılmıştır.
 * - Zorluk: Orta.
 */
public class OcrResult
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string UserEmail { get; set; } = string.Empty;

    public int PageNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public string ExtractedText { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

