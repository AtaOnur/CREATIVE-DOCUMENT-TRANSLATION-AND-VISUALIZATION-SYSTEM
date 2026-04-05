namespace pdf_bitirme.Services.Email;

/*
 * [TR] Bu dosya ne işe yarar: Hesap e-postalarını (özellikle doğrulama) gönderen sade servis sözleşmesi.
 * [TR] Neden gerekli: AccountController'ı SMTP detaylarından bağımsız tutmak için.
 * [TR] İlgili: AccountController, SmtpEmailSender
 *
 * MODIFICATION NOTES (TR)
 * - İleride şifre sıfırlama maili de bu arayüz üzerinden gönderilebilir.
 * - Kuyruklu gönderim (background job) gerekiyorsa bu arayüz korunarak eklenebilir.
 * - Bu yapı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Zorluk: Kolay.
 */
public interface IEmailSender
{
    Task<(bool Ok, string? ErrorMessage)> SendEmailVerificationAsync(
        string toEmail,
        string verifyLink,
        CancellationToken cancellationToken = default);
}

