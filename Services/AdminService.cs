using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using pdf_bitirme.Data;
using pdf_bitirme.Models;
using pdf_bitirme.Models.Entities;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Admin panel için kullanıcı/belge/log verilerini derler ve temel moderasyon aksiyonlarını yürütür.
 * [TR] Neden gerekli: Admin akışını sade bir servis üzerinden yönetip controller tarafını anlaşılır bırakmak.
 * [TR] İlgili: IAdminService, AdminController, AppDbContext, ISimpleAccountStore
 *
 * MODIFICATION NOTES (TR)
 * - Soft delete yaklaşımına geçmek istenirse kullanıcı/doküman aksiyonları güncellenir.
 * - Gelişmiş filtreleme, sayfalama ve raporlama export eklenebilir.
 * - Moderator rolü için belge/log sınırlı yetkileri bu servis metodlarında ayrıştırılabilir.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Orta.
 */
public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly ISimpleAccountStore _accounts;
    private readonly IDocumentService _documentService;
    private readonly IWebHostEnvironment _env;

    public AdminService(AppDbContext db, ISimpleAccountStore accounts, IDocumentService documentService, IWebHostEnvironment env)
    {
        _db = db;
        _accounts = accounts;
        _documentService = documentService;
        _env = env;
    }

    public async Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = _accounts.GetUsers().Count;
        var totalDocuments = await _db.Documents.CountAsync(cancellationToken);
        var totalOcr = await _db.OcrResults.CountAsync(cancellationToken);
        var totalAi = await _db.AiResults.CountAsync(cancellationToken);
        var recentLogs = await _db.ActivityEntries.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(8)
            .Select(x => new AdminLogRowViewModel
            {
                DateUtc = x.CreatedAtUtc,
                UserEmail = x.UserEmail,
                Description = x.Message,
                ActionType = ToActionType(x.Message),
            })
            .ToListAsync(cancellationToken);

        return new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            TotalDocuments = totalDocuments,
            TotalOcrResults = totalOcr,
            TotalAiResults = totalAi,
            RecentLogs = recentLogs,
        };
    }

    public Task<AdminUsersViewModel> GetUsersAsync(string? searchQuery, string? statusFilter, CancellationToken cancellationToken = default)
    {
        var users = _accounts.GetUsers();
        var q = searchQuery?.Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.ToLowerInvariant();
            users = users
                .Where(x => x.Email.ToLowerInvariant().Contains(lower))
                .ToList();
        }

        var normalizedStatus = (statusFilter ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedStatus == "active")
            users = users.Where(x => x.IsActive).ToList();
        else if (normalizedStatus == "passive")
            users = users.Where(x => !x.IsActive).ToList();

        var rows = users
            .OrderBy(x => x.Email)
            .Select(x => new AdminUserRowViewModel
            {
                Email = x.Email,
                Username = x.Email,
                Role = x.Role,
                IsActive = x.IsActive,
                IsEmailVerified = x.IsEmailVerified,
                CreatedAtUtc = x.CreatedAtUtc,
            })
            .ToList();

        return Task.FromResult(new AdminUsersViewModel
        {
            SearchQuery = searchQuery ?? string.Empty,
            StatusFilter = statusFilter ?? string.Empty,
            HasAny = rows.Count > 0,
            Users = rows,
        });
    }

    public async Task<AdminUserDetailsViewModel?> GetUserDetailsAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = _accounts.GetUser(email);
        if (user == null) return null;

        var uploadCount = await _db.Documents.CountAsync(x => x.OwnerEmail == email, cancellationToken);
        var ocrCount = await _db.OcrResults.CountAsync(x => x.UserEmail == email, cancellationToken);
        var aiCount = await _db.AiResults.CountAsync(x => x.UserEmail == email, cancellationToken);
        var notebookCount = await _db.AiResults.CountAsync(x => x.UserEmail == email && x.IsSaved, cancellationToken);

        return new AdminUserDetailsViewModel
        {
            Email = user.Email,
            Username = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAtUtc = user.CreatedAtUtc,
            UploadCount = uploadCount,
            OcrResultCount = ocrCount,
            AiResultCount = aiCount,
            NotebookEntryCount = notebookCount,
        };
    }

    public Task<(bool Ok, string? ErrorMessage)> SetUserActiveAsync(string email, bool isActive, CancellationToken cancellationToken = default)
    {
        // [TR] Admin kendi hesabını pasife alamaz koruması burada yapılabilir; MVP için controller seviyesinde bırakıldı.
        var ok = _accounts.TrySetActive(email, isActive, out var err);
        return Task.FromResult((ok, err));
    }

    public async Task<(bool Ok, string? ErrorMessage)> DeleteUserAsync(string targetEmail, string actorEmail, CancellationToken cancellationToken = default)
    {
        targetEmail = targetEmail.Trim();
        actorEmail = actorEmail.Trim();
        if (string.IsNullOrWhiteSpace(targetEmail))
            return (false, "User email cannot be empty.");

        if (string.Equals(targetEmail, actorEmail, StringComparison.OrdinalIgnoreCase))
            return (false, "Admins cannot delete their own account.");

        var account = _accounts.GetUser(targetEmail);
        if (account == null)
            return (false, "User not found.");

        // [TR] Önce kullanıcının dosyalarını servis üzerinden silip disk+DB tutarlılığını koruyoruz.
        var userDocs = await _db.Documents.AsNoTracking()
            .Where(x => x.OwnerEmail == targetEmail)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var docId in userDocs)
        {
            await _documentService.DeleteAsync(targetEmail, docId, cancellationToken);
        }

        var userChats = await _db.ChatMessages.Where(x => x.UserEmail == targetEmail).ToListAsync(cancellationToken);
        if (userChats.Count > 0) _db.ChatMessages.RemoveRange(userChats);

        var userActivities = await _db.ActivityEntries.Where(x => x.UserEmail == targetEmail).ToListAsync(cancellationToken);
        if (userActivities.Count > 0) _db.ActivityEntries.RemoveRange(userActivities);

        var userSettings = await _db.UserSettings.Where(x => x.UserEmail == targetEmail).ToListAsync(cancellationToken);
        if (userSettings.Count > 0) _db.UserSettings.RemoveRange(userSettings);

        if (!_accounts.TryDeleteUser(targetEmail, out var deleteErr))
            return (false, deleteErr ?? "User could not be deleted.");

        _db.ActivityEntries.Add(new ActivityEntry
        {
            Id = Guid.NewGuid(),
            UserEmail = actorEmail,
            Message = $"Admin deleted user \"{targetEmail}\".",
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<AdminDocumentsViewModel> GetDocumentsAsync(string? searchQuery, string? statusFilter, CancellationToken cancellationToken = default)
    {
        var q = _db.Documents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var s = searchQuery.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.Title.ToLower().Contains(s) ||
                x.FileName.ToLower().Contains(s) ||
                x.OwnerEmail.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<DocumentStatus>(statusFilter, true, out var st))
        {
            q = q.Where(x => x.Status == st);
        }

        var rows = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => new AdminDocumentRowViewModel
            {
                Id = x.Id,
                DocumentName = x.Title,
                OwnerEmail = x.OwnerEmail,
                UploadDateUtc = x.CreatedAtUtc,
                Status = x.Status,
                IsBanned = x.IsBanned,
            })
            .ToListAsync(cancellationToken);

        return new AdminDocumentsViewModel
        {
            SearchQuery = searchQuery ?? string.Empty,
            StatusFilter = statusFilter ?? string.Empty,
            HasAny = rows.Count > 0,
            Documents = rows,
        };
    }

    public async Task<AdminDocumentDetailsViewModel?> GetDocumentDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var d = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null) return null;

        var ocr = await _db.OcrResults.CountAsync(x => x.DocumentId == id, cancellationToken);
        var ai = await _db.AiResults.CountAsync(x => x.DocumentId == id, cancellationToken);
        var chats = await _db.ChatMessages.AsNoTracking()
            .Where(x => x.DocumentId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => new AdminChatMessageRowViewModel
            {
                Id = x.Id,
                UserEmail = x.UserEmail,
                Role = x.Role,
                MessageType = x.MessageType,
                Text = x.Text,
                ImageUrl = x.ImageUrl,
                AudioUrl = x.AudioUrl,
                ResultUrl = x.ResultUrl,
                IsBanned = x.IsBanned,
                BanReason = x.BanReason,
                CreatedAtUtc = x.CreatedAtUtc,
                BannedAtUtc = x.BannedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return new AdminDocumentDetailsViewModel
        {
            Id = d.Id,
            DocumentName = d.Title,
            OwnerEmail = d.OwnerEmail,
            Status = d.Status,
            SizeBytes = d.SizeBytes,
            CreatedAtUtc = d.CreatedAtUtc,
            UpdatedAtUtc = d.UpdatedAtUtc,
            OcrCount = ocr,
            AiCount = ai,
            IsBanned = d.IsBanned,
            BanReason = d.BanReason,
            BannedAtUtc = d.BannedAtUtc,
            ChatMessages = chats,
        };
    }

    public async Task<(bool Ok, string? ErrorMessage)> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var d = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null)
            return (false, "Document not found.");

        var result = await _documentService.DeleteAsync(d.OwnerEmail, id, cancellationToken);
        return result;
    }

    public async Task<(Stream? Stream, string? ContentType, string? DownloadName)> OpenDocumentPdfAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var d = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null)
            return (null, null, null);

        var abs = Path.Combine(_env.ContentRootPath, d.StorageRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(abs))
            return (null, null, null);

        var stream = new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, d.ContentType, d.FileName);
    }

    public async Task<(bool Ok, string? ErrorMessage)> SetDocumentBanAsync(
        Guid id,
        bool isBanned,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d == null)
            return (false, "Document not found.");

        d.IsBanned = isBanned;
        d.BanReason = isBanned ? (reason?.Trim() ?? string.Empty) : string.Empty;
        d.BannedAtUtc = isBanned ? DateTime.UtcNow : null;
        d.UpdatedAtUtc = DateTime.UtcNow;
        _db.ActivityEntries.Add(new ActivityEntry
        {
            Id = Guid.NewGuid(),
            UserEmail = "admin",
            Message = isBanned ? $"Admin banned document \"{d.Title}\"." : $"Admin removed the ban from document \"{d.Title}\".",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? ErrorMessage)> SetChatMessageBanAsync(
        Guid id,
        bool isBanned,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var msg = await _db.ChatMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (msg == null)
            return (false, "Chat message not found.");

        msg.IsBanned = isBanned;
        msg.BanReason = isBanned ? (reason?.Trim() ?? string.Empty) : string.Empty;
        msg.BannedAtUtc = isBanned ? DateTime.UtcNow : null;
        _db.ActivityEntries.Add(new ActivityEntry
        {
            Id = Guid.NewGuid(),
            UserEmail = "admin",
            Message = isBanned ? $"Admin chat mesajini banladi ({msg.UserEmail})." : $"Admin chat mesaji banini kaldirdi ({msg.UserEmail}).",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<AdminLogsViewModel> GetLogsAsync(string? actionFilter, CancellationToken cancellationToken = default)
    {
        var logs = await _db.ActivityEntries.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => new AdminLogRowViewModel
            {
                DateUtc = x.CreatedAtUtc,
                UserEmail = x.UserEmail,
                Description = x.Message,
                ActionType = ToActionType(x.Message),
            })
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(actionFilter))
            logs = logs.Where(x => x.ActionType.Equals(actionFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        return new AdminLogsViewModel
        {
            ActionFilter = actionFilter ?? string.Empty,
            HasAny = logs.Count > 0,
            Logs = logs,
        };
    }

    /// <summary>
    /// [TR] Basit metin eşleştirme ile log satırını eylem tipine ayırır.
    /// </summary>
    private static string ToActionType(string message)
    {
        var m = (message ?? string.Empty).ToLowerInvariant();
        if (m.Contains("giriş") || m.Contains("giris")) return "LOGIN";
        if (m.Contains("çıkış") || m.Contains("cikis")) return "LOGOUT";
        if (m.Contains("yüklendi") || m.Contains("yuklendi") || m.Contains("upload")) return "UPLOAD";
        if (m.Contains("ocr")) return "OCR";
        if (m.Contains("ai")) return "AI";
        if (m.Contains("silindi") || m.Contains("delete")) return "DELETE";
        return "OTHER";
    }
}

