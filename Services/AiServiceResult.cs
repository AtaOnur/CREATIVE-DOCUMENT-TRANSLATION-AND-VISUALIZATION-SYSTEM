namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: AI servis çıktısını sade bir DTO ile taşır (metin ve/veya görsel URL).
 * [TR] Neden gerekli: Mock servis ve ileride gerçek sağlayıcı arasında tek tip çıktı sağlar.
 * [TR] İlgili: IAiService, MockAiService, AiController
 *
 * MODIFICATION NOTES (TR)
 * - Token kullanımı / maliyet bilgisi alanı eklenebilir.
 * - Confidence veya quality score alanı eklenebilir.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Kolay.
 */
public class AiServiceResult
{
    public string OutputText { get; set; } = string.Empty;
    public string OutputImageUrl { get; set; } = string.Empty;
}

