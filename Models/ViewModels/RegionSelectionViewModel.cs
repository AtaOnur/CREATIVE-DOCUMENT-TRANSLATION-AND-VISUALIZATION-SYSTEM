namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: PDF sayfası üzerindeki kullanıcı seçimini temsil eden sade koordinat modeli.
 * [TR] Neden gerekli: OCR yalnızca PDF içindeki seçili bölgeden çalışacağı için x-y-genişlik-yükseklik açık tutulur.
 * [TR] İlgili: DocumentWorkspaceViewModel, Documents/Details workspace paneli
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu bölge seçimi için liste modeli (List<RegionSelectionViewModel>) eklenebilir.
 * - Tam sayfa OCR için tek tıkla sayfa boyutunda seçim üretilebilir.
 * - Annotation araçları (highlight/not) için color/label alanları eklenebilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * - Zorluk: Kolay.
 */
public class RegionSelectionViewModel
{
    public int PageNumber { get; set; } = 1;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

