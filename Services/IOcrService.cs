using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: OCR servis sözleşmesi (arayüz).
 * [TR] Neden gerekli: Şimdilik mock kullanılır, ileride gerçek OCR motoru aynı imza ile değiştirilebilir.
 * [TR] İlgili: MockOcrService, DocumentsController
 *
 * MODIFICATION NOTES (TR)
 * - Gerçek OCR (Tesseract/API) bu arayüzü implement ederek kolayca takılabilir.
 * - Confidence ve dil tespiti çıktısı eklenebilir.
 * - Bu sürümde OCR yalnızca PDF içindeki seçilen bölgeye uygulanmaktadır.
 * - Genel resimden metin çıkarma future work olarak düşünülmüştür.
 * - Zorluk: Kolay.
 */
public interface IOcrService
{
    /// <summary>
    /// [TR] Verilen PDF dosyasının belirtilen sayfasındaki seçili bölgeden metin çıkarır.
    /// </summary>
    /// <param name="language">
    /// OCR dil kodu. PaddleOCR: "en", "latin", "french", "german", "ch" vb.
    /// Tesseract: "eng", "tur", "fra" vb. null ise appsettings varsayılanı kullanılır.
    /// </param>
    Task<string> ExtractFromPdfRegionAsync(
        string pdfFilePath,
        string documentTitle,
        RegionSelectionViewModel region,
        string? language = null,
        CancellationToken cancellationToken = default);
}

