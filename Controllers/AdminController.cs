using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Basit admin panel yönlendirme ve çekirdek yönetim aksiyonları.
 * [TR] Neden gerekli: Kullanıcı, belge, log, istatistik yönetimi için tek ve anlaşılır MVC giriş noktası sunar.
 * [TR] İlgili: IAdminService/AdminService, Views/Admin/*
 *
 * MODIFICATION NOTES (TR)
 * - Moderator/SuperAdmin rolleri için action bazlı ek [Authorize] kuralları eklenebilir.
 * - Gelişmiş sayfalama/sıralama ile büyük veri performansı artırılabilir.
 * - Admin belge detayında sahiplik kısıtına takılmadan PDF önizleme için Pdf action eklendi.
 * - Bu admin yapısı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Genel image-to-text bu sürümde yoktur, future work olarak planlanmıştır.
 * - Zorluk: Orta.
 */
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var vm = await _adminService.GetDashboardAsync(cancellationToken);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? q, string? status, CancellationToken cancellationToken)
    {
        var vm = await _adminService.GetUsersAsync(q, status, cancellationToken);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> UserDetails(string id, CancellationToken cancellationToken)
    {
        var vm = await _adminService.GetUserDetailsAsync(id, cancellationToken);
        if (vm == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserStatus(string email, bool makeActive, CancellationToken cancellationToken)
    {
        // [TR] Adminin kendini pasife almasını engellemek için basit güvenlik kontrolü.
        if (string.Equals(User.Identity?.Name, email, StringComparison.OrdinalIgnoreCase) && !makeActive)
        {
            TempData["ErrorMessage"] = "Kendi admin hesabinizi pasife alamazsiniz.";
            return RedirectToAction(nameof(UserDetails), new { id = email });
        }

        var (ok, err) = await _adminService.SetUserActiveAsync(email, makeActive, cancellationToken);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? (makeActive ? "User has been reactivated." : "User has been deactivated.") : (err ?? "Operation failed.");
        return RedirectToAction(nameof(UserDetails), new { id = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string email, CancellationToken cancellationToken)
    {
        var actorEmail = User.Identity?.Name ?? string.Empty;
        var (ok, err) = await _adminService.DeleteUserAsync(email, actorEmail, cancellationToken);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? "User and related data were deleted." : (err ?? "User deletion failed.");
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Documents(string? q, string? status, CancellationToken cancellationToken)
    {
        var vm = await _adminService.GetDocumentsAsync(q, status, cancellationToken);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DocumentDetails(Guid id, CancellationToken cancellationToken)
    {
        var vm = await _adminService.GetDocumentDetailsAsync(id, cancellationToken);
        if (vm == null)
        {
            TempData["ErrorMessage"] = "Document not found.";
            return RedirectToAction(nameof(Documents));
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var (stream, contentType, fileName) = await _adminService.OpenDocumentPdfAsync(id, cancellationToken);
        if (stream == null || contentType == null || fileName == null)
            return NotFound();

        // [TR] HTTP header değerleri ASCII olmalı; Türkçe karakterli dosya adları InvalidOperationException üretir.
        // Admin önizlemede inline kalması yeterli olduğundan güvenli ASCII fallback kullanıyoruz.
        Response.Headers["Content-Disposition"] = $"inline; filename=\"admin-document-{id:N}.pdf\"";
        return File(stream, contentType, enableRangeProcessing: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        var (ok, err) = await _adminService.DeleteDocumentAsync(id, cancellationToken);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? "Document was deleted from the admin panel." : (err ?? "Delete operation failed.");
        return RedirectToAction(nameof(Documents));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDocumentBan(Guid id, bool isBanned, string? reason, CancellationToken cancellationToken)
    {
        var (ok, err) = await _adminService.SetDocumentBanAsync(id, isBanned, reason, cancellationToken);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? (isBanned ? "Document has been banned." : "Document ban has been removed.") : (err ?? "Document ban operation failed.");
        return RedirectToAction(nameof(DocumentDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetChatMessageBan(Guid id, Guid documentId, bool isBanned, string? reason, CancellationToken cancellationToken)
    {
        var (ok, err) = await _adminService.SetChatMessageBanAsync(id, isBanned, reason, cancellationToken);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? (isBanned ? "Chat message has been banned." : "Chat message ban has been removed.") : (err ?? "Chat ban operation failed.");
        return RedirectToAction(nameof(DocumentDetails), new { id = documentId });
    }

    [HttpGet]
    public async Task<IActionResult> Logs(string? actionType, CancellationToken cancellationToken)
    {
        var vm = await _adminService.GetLogsAsync(actionType, cancellationToken);
        return View(vm);
    }
}

