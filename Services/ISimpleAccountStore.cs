namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Basit hesap deposu sözleşmesi — kayıt, giriş, şifre sıfırlama jetonu.
 * [TR] Neden gerekli: AccountController iş mantığını denetleyicide şişirmeden ayırmak; savunmada katman netliği.
 * [TR] İlgili: SimpleAccountStore, AccountController
 *
 * MODIFICATION NOTES (TR)
 * - Entity Framework + Identity: IUserStore / UserManager ile değiştirilir; parolalar hash’lenir.
 * - E-posta gönderimi: Forgot için SMTP veya SendGrid entegrasyonu.
 * - Genel görüntü OCR (future work) bu modülle ilgili değildir.
 * - Zorluk: Orta (Identity geçişi).
 */
public interface ISimpleAccountStore
{
    /// <summary>Yeni kullanıcı ekler; e-posta benzersiz olmalı.</summary>
    bool TryRegister(string email, string password, out string? errorMessage);

    /// <summary>E-posta ve parola eşleşiyorsa true.</summary>
    bool ValidateCredentials(string email, string password);

    /// <summary>Kullanıcı varsa sıfırlama jetonu üretir; yoksa null (e-posta sızıntısı önleme için dış mesaj aynı kalır).</summary>
    string? CreatePasswordResetToken(string email);

    /// <summary>Geçerli jetonla parolayı günceller.</summary>
    bool TryResetPassword(string token, string newPassword, out string? errorMessage);

    /// <summary>Kullanıcı için e-posta doğrulama jetonu üretir.</summary>
    string? CreateEmailVerificationToken(string email);

    /// <summary>E-posta doğrulama jetonunu işler.</summary>
    bool TryVerifyEmail(string token, out string? email, out string? errorMessage);

    /// <summary>Kullanıcının rolünü döner (User/Admin).</summary>
    string GetRole(string email);

    /// <summary>Kullanıcı aktif mi?</summary>
    bool IsActive(string email);

    /// <summary>Kullanıcının e-postası doğrulanmış mı?</summary>
    bool IsEmailVerified(string email);

    /// <summary>Admin panel için tüm kullanıcılar.</summary>
    IReadOnlyList<SimpleAccountUserInfo> GetUsers();

    /// <summary>Tek kullanıcı bilgisi.</summary>
    SimpleAccountUserInfo? GetUser(string email);

    /// <summary>Kullanıcıyı pasif/aktif yapar.</summary>
    bool TrySetActive(string email, bool isActive, out string? errorMessage);

    /// <summary>Kullanıcıyı depodan siler.</summary>
    bool TryDeleteUser(string email, out string? errorMessage);
}
