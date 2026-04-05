namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Belge listesi sayfası — arama, durum filtresi, sonuç satırları.
 * [TR] İlgili: DocumentsController.Index
 *
 * MODIFICATION NOTES (TR)
 * - Sayfalama (page, pageSize) sonraki adım.
 * - Sıralama sütunu (başlık, tarih).
 * - Zorluk: Kolay.
 */
public class DocumentsIndexViewModel
{
    public string? SearchQuery { get; set; }
    public string? StatusFilter { get; set; }
    public IReadOnlyList<DocumentRowViewModel> Documents { get; set; } = Array.Empty<DocumentRowViewModel>();
    public bool HasAny { get; set; }
}
