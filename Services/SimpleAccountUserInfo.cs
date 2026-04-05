namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Basit hesap deposundaki kullanıcı satırını (rol/durum/tarih) taşır.
 * [TR] Neden gerekli: Admin panelde kullanıcı listesi ve detayını anlaşılır veri modeliyle göstermek için.
 * [TR] İlgili: ISimpleAccountStore, SimpleAccountStore, AdminController
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte telefon, son giriş tarihi gibi alanlar eklenebilir.
 * - Soft-delete/lockout durum kodları genişletilebilir.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Kolay.
 */
public class SimpleAccountUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

