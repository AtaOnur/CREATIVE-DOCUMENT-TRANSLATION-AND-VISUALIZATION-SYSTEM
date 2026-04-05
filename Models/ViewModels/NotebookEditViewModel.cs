using System.ComponentModel.DataAnnotations;

namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Notebook kaydı düzenleme (başlık + kısa not) modeli.
 * [TR] Neden gerekli: Kullanıcı AI çıktısına kişisel başlık/not ekleyebilsin.
 * [TR] İlgili: NotebookController.Edit, Views/Notebook/Edit.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Etiket/favori alanları ileride buraya eklenebilir.
 * - İşbirliği notları (paylaşım) future work olarak bırakıldı.
 * - Genel resim OCR desteği bu sürümde yer almamaktadır.
 * - Zorluk: Kolay.
 */
public class NotebookEditViewModel
{
    public Guid AiResultId { get; set; }
    public Guid DocumentId { get; set; }

    [Required, StringLength(160)]
    public string NoteTitle { get; set; } = string.Empty;

    [StringLength(1000)]
    public string UserNote { get; set; } = string.Empty;

    public string SourceDocument { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
}

