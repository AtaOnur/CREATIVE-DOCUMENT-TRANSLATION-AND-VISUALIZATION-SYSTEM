namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Admin log/usage ekranı için aktivite satırlarını taşır.
 * [TR] Neden gerekli: OCR/AI/upload/delete/login benzeri eylemleri takip edilebilir sunmak için.
 * [TR] İlgili: AdminController.Logs, Views/Admin/Logs.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Eylem tipine göre detay filtreleme eklenebilir.
 * - Gelişmiş denetim için request-id/IP alanları eklenebilir.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Kolay.
 */
public class AdminLogsViewModel
{
    public string ActionFilter { get; set; } = string.Empty;
    public bool HasAny { get; set; }
    public List<AdminLogRowViewModel> Logs { get; set; } = new();
}

public class AdminLogRowViewModel
{
    public DateTime DateUtc { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

