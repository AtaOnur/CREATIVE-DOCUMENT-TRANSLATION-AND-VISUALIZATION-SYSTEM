namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Yükleme kuralları sabitleri — boyut ve MIME; tek yerde tutulur.
 * [TR] İlgili: DocumentService, Upload.cshtml doğrulama mesajları
 *
 * MODIFICATION NOTES (TR)
 * - appsettings.json üzerinden yapılandırılabilir sınır (IOptions).
 * - DOCX için ayrı boyut ve izin verilen uzantılar.
 * - Zorluk: Kolay.
 */
public static class DocumentUploadConstants
{
    public const long MaxBytes = 20 * 1024 * 1024;
    public const string AllowedContentType = "application/pdf";
    public static readonly string[] AllowedExtensions = [".pdf"];
    public static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // [TR] ASCII: %PDF
}
