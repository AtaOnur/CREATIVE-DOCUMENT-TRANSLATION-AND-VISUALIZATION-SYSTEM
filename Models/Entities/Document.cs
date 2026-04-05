using pdf_bitirme.Models;

namespace pdf_bitirme.Models.Entities;

/*
 * [TR] Bu dosya ne işe yarar: Veritabanındaki PDF belge kaydı — sahip, dosya meta verisi, depo yolu, durum.
 * [TR] Neden gerekli: EF Core ile kalıcı liste; yükleme sonrası iş akışı bu satır üzerinden izlenir.
 * [TR] İlgili: AppDbContext, DocumentService
 *
 * MODIFICATION NOTES (TR)
 * - Sayfa sayısı, özet metin sütunları sonradan eklenebilir.
 * - Bulut depo URL’si veya hash ile bütünlük kontrolü.
 * - Önizleme küçük resmi (thumbnail) future work.
 * - Zorluk: Kolay–orta.
 */
public class Document
{
    public Guid Id { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    public DocumentStatus Status { get; set; }
    /// <summary>ContentRoot içinde Data/uploads altındaki göreli yol (ör. AB12/..../id.pdf).</summary>
    public string StorageRelativePath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
