namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Notebook/History liste ekranı modeli.
 * [TR] Neden gerekli: Arama, işlem filtresi ve sonuç kartlarını tek modelde taşır.
 * [TR] İlgili: NotebookController.Index, Views/Notebook/Index.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Klasör, etiket ve favori filtreleri eklenebilir.
 * - Dışa aktarma (PDF/Markdown) eklenebilir.
 * - Genel resim OCR desteği bu sürümde yer almamaktadır.
 * - Zorluk: Kolay.
 */
public class NotebookIndexViewModel
{
    public string SearchQuery { get; set; } = string.Empty;
    public string OperationFilter { get; set; } = string.Empty;
    public bool HasAny { get; set; }
    public List<NotebookRowViewModel> Items { get; set; } = new();
}

public class NotebookRowViewModel
{
    public Guid AiResultId { get; set; }
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string PreviewContent { get; set; } = string.Empty;
}

