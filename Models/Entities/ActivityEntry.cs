namespace pdf_bitirme.Models.Entities;

/*
 * [TR] Bu dosya ne işe yarar: Kullanıcıya özgü kısa aktivite satırı — yükleme/silme mesajı ve zaman.
 * [TR] Neden gerekli: Panoda “son hareketler” kartı; basit denetim izi.
 * [TR] İlgili: DocumentService (kayıt ekleme), Dashboard
 *
 * MODIFICATION NOTES (TR)
 * - Denetim günlüğünde IP veya istek kimliği eklenebilir.
 * - Çok büyük tablolar için temizleme (retention) job’ı.
 * - Zorluk: Kolay.
 */
public class ActivityEntry
{
    public Guid Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
