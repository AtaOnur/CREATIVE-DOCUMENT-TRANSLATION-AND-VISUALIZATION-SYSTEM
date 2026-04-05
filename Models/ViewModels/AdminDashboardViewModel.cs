namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Admin ana panel için toplam istatistikler ve hızlı bağlantı sayılarını taşır.
 * [TR] Neden gerekli: Jüri sunumunda sistemin genel sağlığını tek ekranda göstermek için.
 * [TR] İlgili: AdminController.Index, Views/Admin/Index.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Grafik (haftalık kullanım trendi) verileri eklenebilir.
 * - Rol bazlı farklı kartlar (Moderator/SuperAdmin) eklenebilir.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Kolay.
 */
public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalOcrResults { get; set; }
    public int TotalAiResults { get; set; }
    public List<AdminLogRowViewModel> RecentLogs { get; set; } = new();
}

