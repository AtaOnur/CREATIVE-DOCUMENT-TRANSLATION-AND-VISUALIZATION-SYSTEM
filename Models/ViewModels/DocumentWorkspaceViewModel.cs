using pdf_bitirme.Models;

namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Belge çalışma alanı ekranı için tek ViewModel; viewer ayarları + seçim + OCR placeholder metni.
 * [TR] Neden gerekli: Details ekranında toolbar, sağ panel ve seçim akışını anlaşılır tek modelde toplar.
 * [TR] İlgili: DocumentsController.Details, Views/Documents/Details.cshtml, pdf-workspace.mjs
 *
 * MODIFICATION NOTES (TR)
 * - Gelişmiş viewer için rotate, fit-height ve thumbnail alanları eklenebilir.
 * - Text overlay ve annotation verileri bu modele taşınabilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * - Zorluk: Orta.
 */
public class DocumentWorkspaceViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public long SizeBytes { get; set; }

    public string PdfEndpointUrl { get; set; } = string.Empty;
    public string PagePreviewEndpointUrl { get; set; } = string.Empty;
    public string ExtractTextEndpointUrl { get; set; } = string.Empty;
    public string SaveOcrEndpointUrl { get; set; } = string.Empty;

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int ZoomPercent { get; set; } = 100;
    public bool SelectionMode { get; set; }

    public RegionSelectionViewModel SelectedRegion { get; set; } = new();
    public string OcrTextPlaceholder { get; set; } = string.Empty;
    public Guid? LastOcrResultId { get; set; }

    public string DefaultAiModel { get; set; } = "mock-gpt";
    public string DefaultTranslationStyle { get; set; } = "Formal";
    public string ThemePreference { get; set; } = "System";
    public List<string> AvailableAiModels { get; set; } = new();
}

