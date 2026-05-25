namespace pdf_bitirme.Models.Entities;

/*
 * [TR] Bu dosya ne işe yarar: Workspace AI sohbetindeki kullanıcı/AI mesajlarını sunucuda saklar.
 * [TR] Neden gerekli: Admin tüm kullanıcıların belge sohbetlerini inceleyip gerektiğinde mesajı banlayabilsin.
 * [TR] İlgili: ChatController, AdminService, Views/Admin/DocumentDetails
 *
 * MODIFICATION NOTES (TR)
 * - Mesajlar belge ve kullanıcı e-postası ile ilişkilidir; localStorage tek başına admin görünürlüğü sağlamaz.
 * - Ban soft-state olarak tutulur; audit gerekirse ayrı moderation_actions tablosu eklenebilir.
 * - Zorluk: Orta.
 */
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string MessageType { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string ResultUrl { get; set; } = string.Empty;
    public bool IsBanned { get; set; }
    public string BanReason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? BannedAtUtc { get; set; }
}
