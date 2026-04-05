namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Workspace'ten OCR tetikleme isteği için basit request modeli.
 * [TR] Neden gerekli: Seçim koordinatını controller action'a temiz şekilde taşır.
 * [TR] İlgili: DocumentsController.ExtractText
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu bölge için RegionSelectionViewModel[] kullanılabilir.
 * - Bu sürümde OCR yalnızca PDF içindeki seçilen bölgeye uygulanmaktadır.
 * - Genel resimden metin çıkarma future work olarak düşünülmüştür.
 * - Zorluk: Kolay.
 */
public class OcrExtractRequestViewModel
{
    public Guid DocumentId { get; set; }
    public RegionSelectionViewModel Region { get; set; } = new();

    /// <summary>
    /// [TR] Kullanıcının UI'dan seçtiği OCR dil kodu.
    /// PaddleOCR için: "en", "latin", "french", "german", "ch" vb.
    /// Tesseract için: "eng", "tur+eng", "fra" vb.
    /// null/boş ise appsettings varsayılanı kullanılır.
    /// </summary>
    public string? Language { get; set; }
}

