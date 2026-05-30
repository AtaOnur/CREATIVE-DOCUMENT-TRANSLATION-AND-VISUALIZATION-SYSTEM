namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Belge sahibi erişim durumu (izinli / banlı / bulunamadı) ve ban uyarı ekranı modelleri.
 * [TR] Neden gerekli: DocumentsController.Details banlı belgede 404 yerine anlamlı mesaj gösterebilsin.
 * [TR] İlgili: DocumentService.GetOwnerDocumentAccessAsync, Views/Documents/Banned.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - OwnerDocumentAccessStatus: servis katmanında erişim kararını tek enum ile taşır.
 * - DocumentBannedViewModel: admin ban gerekçesi (BanReason) varsa kullanıcıya gösterilir.
 * - Zorluk: Kolay.
 */
public enum OwnerDocumentAccessStatus
{
    NotFound,
    Banned,
    Allowed,
}

public class OwnerDocumentAccessResult
{
    public OwnerDocumentAccessStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BanReason { get; set; } = string.Empty;
}

public class DocumentBannedViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BanReason { get; set; } = string.Empty;
}
