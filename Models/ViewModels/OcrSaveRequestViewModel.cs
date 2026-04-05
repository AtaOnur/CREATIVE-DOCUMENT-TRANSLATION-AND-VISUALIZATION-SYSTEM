namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Kullanıcının düzenlediği OCR metnini kaydetmek için request modeli.
 * [TR] Neden gerekli: OCR sonucu editable metin alanından veritabanına yazılabilsin.
 * [TR] İlgili: DocumentsController.SaveOcrText
 *
 * MODIFICATION NOTES (TR)
 * - Versiyonlama için revision no alanı eklenebilir.
 * - Manuel düzeltme notu (editedByUser) alanı eklenebilir.
 * - Bu sürümde OCR yalnızca PDF içindeki seçilen bölgeye uygulanmaktadır.
 * - Genel resimden metin çıkarma future work olarak düşünülmüştür.
 * - Zorluk: Kolay.
 */
public class OcrSaveRequestViewModel
{
    public Guid OcrResultId { get; set; }
    public string Text { get; set; } = string.Empty;
}

