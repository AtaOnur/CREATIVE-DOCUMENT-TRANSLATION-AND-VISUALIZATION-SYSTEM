using pdf_bitirme.Models;

namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Tablo ve kartlarda kullanılan satır özeti (DTO).
 * [TR] İlgili: Documents/Index, Dashboard listeleri
 *
 * MODIFICATION NOTES (TR)
 * - Son işlem kullanıcı adı, ilerleme yüzdesi alanları.
 * - Zorluk: Kolay.
 */
public class DocumentRowViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public long SizeBytes { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
