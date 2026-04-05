using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Gerçek OCR yerine demo metin dönen basit mock servis.
 * [TR] Neden gerekli: Bitirme sunumunda uçtan uca akış gösterilirken altyapı sade tutulur.
 * [TR] İlgili: IOcrService, DocumentsController
 *
 * MODIFICATION NOTES (TR)
 * - Gerçek OCR motoruna geçişte bu sınıf yerine yeni implementasyon kayıt edilir.
 * - Bölge boyutuna göre confidence benzeri örnek değer döndürülebilir.
 * - Bu sürümde OCR yalnızca PDF içindeki seçilen bölgeye uygulanmaktadır.
 * - Genel resimden metin çıkarma future work olarak düşünülmüştür.
 * - Zorluk: Kolay.
 */
public class MockOcrService : IOcrService
{
    public async Task<string> ExtractFromPdfRegionAsync(
        string pdfFilePath,
        string documentTitle,
        RegionSelectionViewModel region,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        // [TR] Kısa gecikme: kullanıcı loading durumunu görsün.
        await Task.Delay(650, cancellationToken);

        return
            $"[Mock OCR] {documentTitle}{Environment.NewLine}" +
            $"Page={region.PageNumber}, x={region.X:0.##}, y={region.Y:0.##}, w={region.Width:0.##}, h={region.Height:0.##}{Environment.NewLine}{Environment.NewLine}" +
            "This is a placeholder extracted text from the selected PDF region. " +
            "User can edit this text before AI processing.";
    }
}

