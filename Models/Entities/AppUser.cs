namespace pdf_bitirme.Models.Entities;

/*
 * [TR] Bu dosya ne işe yarar: Kullanıcı hesabını SQLite'ta kalıcı olarak saklar.
 * [TR] Neden gerekli: In-memory mağaza uygulama her restart'ta sıfırlanıyordu; DB ile veriler korunur.
 * [TR] İlgili: AppDbContext, DbSimpleAccountStore
 *
 * MODIFICATION NOTES (TR)
 * - Parola düz metin saklanıyor; yalnızca bitirme/demo için. Üretimde BCrypt/PBKDF2 kullanılmalı.
 * - Gelecekte PasswordHash sütununa geçiş için bu entity düzenlenir.
 * - ASP.NET Core Identity'e geçişte bu tablo kaldırılır, IdentityUser kullanılır.
 * - Zorluk: Kolay (demo) → Orta (üretim hash).
 */
public class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
