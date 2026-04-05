using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace pdf_bitirme.Services.Email;

/*
 * [TR] Bu dosya ne işe yarar: Gmail SMTP üzerinden doğrulama e-postası gönderir.
 * [TR] Neden gerekli: Demo link gösterimi yerine gerçek mail doğrulama akışını aktif etmek için.
 * [TR] İlgili: IEmailSender, AccountController, appsettings.json (Email:Smtp)
 *
 * MODIFICATION NOTES (TR)
 * - Gmail dışında kurumsal SMTP için Host/Port değiştirerek kullanılabilir.
 * - HTML şablonlama motoru (Razor templating) eklenebilir.
 * - Bu yapı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Zorluk: Kolay-orta.
 */
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(bool Ok, string? ErrorMessage)> SendEmailVerificationAsync(
        string toEmail,
        string verifyLink,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return (false, "SMTP gönderimi kapalı. Email:Smtp:Enabled=true yapın.");

        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Password) ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            return (false, "SMTP ayarlari eksik.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = "CreativeDoc - E-posta Dogrulama",
                Body =
                    "Merhaba,\n\n" +
                    "Hesabinizi dogrulamak icin asagidaki baglantiyi acin:\n" +
                    $"{verifyLink}\n\n" +
                    "Bu e-postayi siz istemediyseniz dikkate almayin.",
                IsBodyHtml = false,
            };
            message.To.Add(toEmail);

            using var smtp = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                Credentials = new NetworkCredential(_options.Username, _options.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            // [TR] SmtpClient'da CancellationToken desteği sınırlı, basit MVP için SendMailAsync kullanılıyor.
            await smtp.SendMailAsync(message);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP email gonderimi basarisiz oldu.");
            return (false, "Dogrulama e-postasi gonderilemedi.");
        }
    }
}

