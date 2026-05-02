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
    /// pdftoppm (Poppler) ile sayfa raster edilir, ardından bölge kırpılır.
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

    /// <summary>
    /// [TR] Tarayıcıdan gelen, önceden kırpılmış PNG/JPEG görseli üzerinde OCR çalıştırır.
    /// Bu yol pdftoppm/Poppler bağımlılığını ortadan kaldırır — sunucuda yalnız Tesseract
    /// veya Python+PaddleOCR kurulu olması yeterlidir.
    /// </summary>
    /// <param name="imageBytes">PNG veya JPEG görüntü baytları.</param>
    /// <param name="documentTitle">Loglama amaçlı belge başlığı (opsiyonel).</param>
    /// <param name="language">
    /// OCR dil kodu. null ise appsettings varsayılanı kullanılır.
    /// </param>
    Task<string> ExtractFromImageBytesAsync(
        byte[] imageBytes,
        string documentTitle,
        string? language = null,
        CancellationToken cancellationToken = default);
}

