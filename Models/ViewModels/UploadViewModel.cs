using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Yükleme formu — isteğe bağlı başlık ve dosya; asıl PDF denetimi serviste.
 * [TR] İlgili: DocumentsController.Upload, Views/Documents/Upload.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu dosya: IFormFile[] ve döngü.
 * - Etiket (tag) veya proje klasörü seçimi.
 * - Zorluk: Kolay.
 */
public class UploadViewModel
{
    [Display(Name = "Title (optional)")]
    [StringLength(500)]
    public string? Title { get; set; }

    [Display(Name = "PDF file")]
    public IFormFile? File { get; set; }

    [Display(Name = "Copyright and responsibility confirmation")]
    public bool CopyrightResponsibilityAccepted { get; set; }
}
