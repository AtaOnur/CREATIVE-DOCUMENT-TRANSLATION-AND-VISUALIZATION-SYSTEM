namespace pdf_bitirme.Services.Email;

/*
 * [TR] Bu dosya ne işe yarar: SMTP ayarlarını (host, port, kullanıcı, app password) yapılandırmadan taşır.
 * [TR] Neden gerekli: E-posta gönderimini koddan ayırıp appsettings/user-secrets üzerinden yönetmek için.
 * [TR] İlgili: Program.cs, SmtpEmailSender
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte farklı provider (SendGrid/Brevo) için ayrı options sınıfları eklenebilir.
 * - Şablon/çoklu dil için konu ve gövde ayarları genişletilebilir.
 * - Bu yapı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Zorluk: Kolay.
 */
public class SmtpEmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "CreativeDoc";
}

