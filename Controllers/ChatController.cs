using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pdf_bitirme.Data;
using pdf_bitirme.Models.Entities;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne ise yarar: Workspace AI chat mesajlarini sunucuda kalici hale getirir.
 * [TR] Neden gerekli: Admin panelin kullanici sohbetlerini gorebilmesi icin localStorage yeterli degildir.
 * [TR] Ilgili: pdf-workspace.mjs, ChatMessage, AdminController.DocumentDetails
 *
 * MODIFICATION NOTES (TR)
 * - Mesaj silme yerine admin ban flag'i kullanilir; audit icin veri korunur.
 * - Ileride chat session/thread ayrimi eklenebilir.
 * - Zorluk: Orta.
 */
[Authorize]
public class ChatController : Controller
{
    private readonly AppDbContext _db;

    public ChatController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] ChatMessageSaveRequestViewModel request, CancellationToken cancellationToken)
    {
        if (request.DocumentId == Guid.Empty)
            return BadRequest(new { ok = false, message = "Document ID is required." });

        var email = User.Identity?.Name ?? string.Empty;
        var document = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.DocumentId && x.OwnerEmail == email && !x.IsBanned, cancellationToken);
        if (document == null)
            return NotFound(new { ok = false, message = "Document was not found or is banned." });

        var role = NormalizeRole(request.Role);
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            DocumentId = request.DocumentId,
            UserEmail = email,
            Role = role,
            MessageType = NormalizeMessageType(request.MessageType),
            Text = TrimTo(request.Text, 12000),
            ImageUrl = TrimTo(request.ImageUrl, 1000),
            AudioUrl = TrimTo(request.AudioUrl, 1000),
            ResultUrl = TrimTo(request.ResultUrl, 1000),
            IsBanned = false,
            BanReason = string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
        };

        if (string.IsNullOrWhiteSpace(message.Text) &&
            string.IsNullOrWhiteSpace(message.ImageUrl) &&
            string.IsNullOrWhiteSpace(message.AudioUrl) &&
            string.IsNullOrWhiteSpace(message.ResultUrl))
        {
            return BadRequest(new { ok = false, message = "Empty chat messages are not saved." });
        }

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, id = message.Id });
    }

    private static string NormalizeRole(string? role)
    {
        role = (role ?? string.Empty).Trim().ToLowerInvariant();
        return role is "ai" or "assistant" ? "ai" : "user";
    }

    private static string NormalizeMessageType(string? type)
    {
        type = (type ?? string.Empty).Trim().ToLowerInvariant();
        return type is "image" or "audio" or "error" ? type : "text";
    }

    private static string TrimTo(string? value, int max)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }
}
