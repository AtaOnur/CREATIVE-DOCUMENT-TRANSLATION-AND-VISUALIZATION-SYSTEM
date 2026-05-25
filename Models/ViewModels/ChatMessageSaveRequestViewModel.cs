namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne ise yarar: Workspace chat mesajinin sunucuya kaydedilmesi icin JSON istek modeli.
 * [TR] Neden gerekli: Admin panelde chat gorunurlugu icin localStorage disinda kalici kayit gerekir.
 * [TR] Ilgili: ChatController.Save, pdf-workspace.mjs
 *
 * MODIFICATION NOTES (TR)
 * - Ileride thread/session id eklenerek sohbet oturumlari ayrilabilir.
 * - Zorluk: Kolay.
 */
public class ChatMessageSaveRequestViewModel
{
    public Guid DocumentId { get; set; }
    public string Role { get; set; } = "user";
    public string MessageType { get; set; } = "text";
    public string Text { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string ResultUrl { get; set; } = string.Empty;
}
