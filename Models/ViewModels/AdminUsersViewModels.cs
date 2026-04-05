namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Admin kullanıcı listesi ve kullanıcı detay ekranı modellerini içerir.
 * [TR] Neden gerekli: Kullanıcı yönetimi (liste, arama, pasife alma, detay istatistikleri) ekranlarını sade tutar.
 * [TR] İlgili: AdminController.Users/UserDetails, Views/Admin/Users.cshtml, Views/Admin/UserDetails.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte rol değiştirme (User->Admin) aksiyonu eklenebilir.
 * - Soft delete alanı (DeletedAtUtc) ile daha güvenli kullanıcı yönetimi yapılabilir.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Kolay.
 */
public class AdminUsersViewModel
{
    public string SearchQuery { get; set; } = string.Empty;
    public string StatusFilter { get; set; } = string.Empty;
    public bool HasAny { get; set; }
    public List<AdminUserRowViewModel> Users { get; set; } = new();
}

public class AdminUserRowViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminUserDetailsViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public int UploadCount { get; set; }
    public int OcrResultCount { get; set; }
    public int AiResultCount { get; set; }
    public int NotebookEntryCount { get; set; }
}

