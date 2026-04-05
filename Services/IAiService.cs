using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: AI işlem servisi arayüzü (çeviri, özet, rewrite, creative write, visualize).
 * [TR] Neden gerekli: Mock ve gerçek sağlayıcıyı aynı imza ile değiştirebilmek için.
 * [TR] İlgili: MockAiService, AiController
 *
 * MODIFICATION NOTES (TR)
 * - İleride model bazlı routing (OpenAI, local model vb.) eklenebilir.
 * - Operation bazlı prompt şablon sistemi eklenebilir.
 * - Bu sürümde OCR yalnızca PDF içindeki seçilen bölgeye uygulanmaktadır.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Kolay.
 */
public interface IAiService
{
    Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default);
}

