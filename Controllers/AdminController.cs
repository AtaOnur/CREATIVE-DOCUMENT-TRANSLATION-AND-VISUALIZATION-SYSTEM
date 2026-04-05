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
            TempData["ErrorMessage"] = "Kullanici bulunamadi.";
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
            ok ? (makeActive ? "Kullanici tekrar aktif edildi." : "Kullanici pasif yapildi.") : (err ?? "Islem basarisiz.");
        return RedirectToAction(nameof(UserDetails), new { id = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string email, CancellationToken cancellationToken)
    {
        var actorEmail = User.Identity?.Name ?? string.Empty;
        var (ok, err) = await _adminService.DeleteUserAsync(email, actorEmail, cancellationToken);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? "Kullanici ve iliskili verileri silindi." : (err ?? "Kullanici silme islemi basarisiz.");
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
            TempData["ErrorMessage"] = "Belge bulunamadi.";
            return RedirectToAction(nameof(Documents));
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        var (ok, err) = await _adminService.DeleteDocumentAsync(id, cancellationToken);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] =
            ok ? "Belge admin panelinden silindi." : (err ?? "Silme islemi basarisiz.");
        return RedirectToAction(nameof(Documents));
    }

    [HttpGet]
    public async Task<IActionResult> Logs(string? actionType, CancellationToken cancellationToken)
    {
        var vm = await _adminService.GetLogsAsync(actionType, cancellationToken);
        return View(vm);
    }
}

