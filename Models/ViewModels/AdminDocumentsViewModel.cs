using pdf_bitirme.Models;

namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Admin belge yönetimi listesi ve detay modeli.
 * [TR] Neden gerekli: Problemli içerik/dosya silme gibi temel moderasyon aksiyonlarını göstermek için.
 * [TR] İlgili: AdminController.Documents/DocumentDetails, Views/Admin/Documents.cshtml, Views/Admin/DocumentDetails.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Gelişmiş filtreleme (durum, tarih aralığı, kullanıcı) eklenebilir.
 * - İçerik raporlama (şikayet nedeni) alanı eklenebilir.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Kolay.
 */
public class AdminDocumentsViewModel
{
    public string SearchQuery { get; set; } = string.Empty;
    public string StatusFilter { get; set; } = string.Empty;
    public bool HasAny { get; set; }
    public List<AdminDocumentRowViewModel> Documents { get; set; } = new();
}

public class AdminDocumentRowViewModel
{
    public Guid Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public DateTime UploadDateUtc { get; set; }
    public DocumentStatus Status { get; set; }
}

public class AdminDocumentDetailsViewModel
{
    public Guid Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int OcrCount { get; set; }
    public int AiCount { get; set; }
}

