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
    [Display(Name = "Başlık (isteğe bağlı)")]
    [StringLength(500)]
    public string? Title { get; set; }

    [Display(Name = "PDF dosyası")]
    public IFormFile? File { get; set; }
}
