using pdf_bitirme.Models;

namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: “Aç” eyleminin gösterdiği belge meta özeti (PDF önizleme yok).
 * [TR] İlgili: DocumentsController.Details
 *
 * MODIFICATION NOTES (TR)
 * - PDF.js gömülü önizleme ve bölge seçimi sonraki sprint.
 * - OCR yalnızca seçili PDF bölgesi; genel görüntü OCR future work.
 * - Zorluk: Orta (önizleyici ile).
 */
public class DocumentDetailsViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Not:
 * - Bu model eski metadata görünümü için tutuldu; workspace ekranı DocumentWorkspaceViewModel ile çalışır.
 *
 * MODIFICATION NOTES (TR)
 * - Eski detay kartı geri dönüş ekranı olarak kullanılabilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * -----------------------------------------------------------------------------
 */
