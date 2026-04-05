using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Admin panel iş mantığı sözleşmesi (dashboard, kullanıcı, belge, log).
 * [TR] Neden gerekli: AdminController'ı sade tutup okunabilir servis katmanına taşımak için.
 * [TR] İlgili: AdminService, AdminController
 *
 * MODIFICATION NOTES (TR)
 * - İleride Moderator/SuperAdmin ayrımı için metodlar genişletilebilir.
 * - Grafiksel dashboard verileri ve rapor export fonksiyonları eklenebilir.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Orta.
 */
public interface IAdminService
{
    Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<AdminUsersViewModel> GetUsersAsync(string? searchQuery, string? statusFilter, CancellationToken cancellationToken = default);

    Task<AdminUserDetailsViewModel?> GetUserDetailsAsync(string email, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> SetUserActiveAsync(string email, bool isActive, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> DeleteUserAsync(string targetEmail, string actorEmail, CancellationToken cancellationToken = default);

    Task<AdminDocumentsViewModel> GetDocumentsAsync(string? searchQuery, string? statusFilter, CancellationToken cancellationToken = default);

    Task<AdminDocumentDetailsViewModel?> GetDocumentDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdminLogsViewModel> GetLogsAsync(string? actionFilter, CancellationToken cancellationToken = default);
}

