using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using pdf_bitirme.Data;
using pdf_bitirme.Models.Entities;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Kullanıcı hesaplarını SQLite üzerinden kalıcı olarak yönetir.
 * [TR] Neden gerekli: Eski in-memory mağaza uygulama her restart'ta sıfırlanıyordu; bu sınıf DB'den okur/yazar.
 * [TR] İlgili: AppDbContext, AppUser, AccountController, AdminController
 *
 * MODIFICATION NOTES (TR)
 * - Tokenlar (şifre sıfırlama, e-posta doğrulama) hâlâ in-memory; kısa ömürlü olduklarından DB'ye taşımak
 *   zorunlu değil. Kalıcılık isteniyor ise ayrı bir token tablosu eklenebilir.
 * - Parola düz metin saklanıyor (yalnızca demo/bitirme). Üretimde BCrypt eklenmelidir.
 * - ASP.NET Core Identity'e geçişte bu dosya kaldırılır.
 * - Zorluk: Kolay.
 */
public class DbSimpleAccountStore : ISimpleAccountStore
{
    private readonly AppDbContext _db;

    // [TR] Tokenlar kısa ömürlüdür; static dict yeterli, DB'ye taşımaya gerek yok.
    private static readonly ConcurrentDictionary<string, (string Email, DateTime ExpiresUtc)>
        ResetTokens = new();
    private static readonly ConcurrentDictionary<string, (string Email, DateTime ExpiresUtc)>
        VerifyTokens = new();

    public DbSimpleAccountStore(AppDbContext db)
    {
        _db = db;
    }

    public bool TryRegister(string email, string password, out string? errorMessage)
    {
        errorMessage = null;
        email = email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            errorMessage = "E-posta adresi gerekli.";
            return false;
        }

        if (_db.AppUsers.Any(u => u.Email.ToLower() == email.ToLower()))
        {
            errorMessage = "Bu e-posta ile zaten kayıt var.";
            return false;
        }

        _db.AppUsers.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = password,
            Role = "User",
            IsActive = true,
            IsEmailVerified = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        _db.SaveChanges();
        return true;
    }

    public bool ValidateCredentials(string email, string password)
    {
        email = email.Trim();
        // [TR] Burada YALNIZCA aktiflik ve parola kontrolü yapılır.
        // IsEmailVerified kontrolü AccountController.Login içinde ayrıca yapılır;
        // bu sayede EF Core tracking çakışması olmadan doğru hata mesajı gösterilir.
        var u = _db.AppUsers.AsNoTracking()
            .FirstOrDefault(x => x.Email.ToLower() == email.ToLower());
        return u != null && u.IsActive && u.Password == password;
    }

    public string? CreatePasswordResetToken(string email)
    {
        email = email.Trim();
        if (!_db.AppUsers.Any(u => u.Email.ToLower() == email.ToLower()))
            return null;

        var token = Guid.NewGuid().ToString("N");
        ResetTokens[token] = (email, DateTime.UtcNow.AddHours(1));
        return token;
    }

    public bool TryResetPassword(string token, string newPassword, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            errorMessage = "Geçersiz sıfırlama bağlantısı.";
            return false;
        }

        if (!ResetTokens.TryGetValue(token, out var entry))
        {
            errorMessage = "Bağlantı geçersiz veya süresi dolmuş.";
            return false;
        }

        if (entry.ExpiresUtc < DateTime.UtcNow)
        {
            ResetTokens.TryRemove(token, out _);
            errorMessage = "Bağlantı süresi dolmuş. Yeni istek gönderin.";
            return false;
        }

        var u = _db.AppUsers.FirstOrDefault(x => x.Email.ToLower() == entry.Email.ToLower());
        if (u == null)
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        u.Password = newPassword;
        _db.SaveChanges();
        ResetTokens.TryRemove(token, out _);
        return true;
    }

    public string? CreateEmailVerificationToken(string email)
    {
        email = email.Trim();
        if (!_db.AppUsers.Any(u => u.Email.ToLower() == email.ToLower()))
            return null;

        var token = Guid.NewGuid().ToString("N");
        VerifyTokens[token] = (email, DateTime.UtcNow.AddHours(24));
        return token;
    }

    public bool TryVerifyEmail(string token, out string? email, out string? errorMessage)
    {
        email = null;
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            errorMessage = "Geçersiz doğrulama bağlantısı.";
            return false;
        }

        if (!VerifyTokens.TryGetValue(token, out var entry))
        {
            errorMessage = "Bağlantı geçersiz veya süresi dolmuş.";
            return false;
        }

        if (entry.ExpiresUtc < DateTime.UtcNow)
        {
            VerifyTokens.TryRemove(token, out _);
            errorMessage = "Doğrulama bağlantısının süresi dolmuş.";
            return false;
        }

        var u = _db.AppUsers.FirstOrDefault(x => x.Email.ToLower() == entry.Email.ToLower());
        if (u == null)
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        u.IsEmailVerified = true;
        _db.SaveChanges();
        VerifyTokens.TryRemove(token, out _);
        email = u.Email;
        return true;
    }

    public string GetRole(string email)
    {
        email = email.Trim();
        return _db.AppUsers.AsNoTracking()
            .Where(u => u.Email.ToLower() == email.ToLower())
            .Select(u => u.Role)
            .FirstOrDefault() ?? "User";
    }

    public bool IsActive(string email)
    {
        email = email.Trim();
        return _db.AppUsers.AsNoTracking()
            .Any(u => u.Email.ToLower() == email.ToLower() && u.IsActive);
    }

    public bool IsEmailVerified(string email)
    {
        email = email.Trim();
        return _db.AppUsers.AsNoTracking()
            .Any(u => u.Email.ToLower() == email.ToLower() && u.IsEmailVerified);
    }

    public IReadOnlyList<SimpleAccountUserInfo> GetUsers()
    {
        return _db.AppUsers.AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new SimpleAccountUserInfo
            {
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAtUtc = u.CreatedAtUtc,
            })
            .ToList();
    }

    public SimpleAccountUserInfo? GetUser(string email)
    {
        email = email.Trim();
        var u = _db.AppUsers.AsNoTracking()
            .FirstOrDefault(x => x.Email.ToLower() == email.ToLower());
        if (u == null) return null;

        return new SimpleAccountUserInfo
        {
            Email = u.Email,
            Role = u.Role,
            IsActive = u.IsActive,
            IsEmailVerified = u.IsEmailVerified,
            CreatedAtUtc = u.CreatedAtUtc,
        };
    }

    public bool TrySetActive(string email, bool isActive, out string? errorMessage)
    {
        errorMessage = null;
        email = email.Trim();
        var u = _db.AppUsers.FirstOrDefault(x => x.Email.ToLower() == email.ToLower());
        if (u == null)
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        u.IsActive = isActive;
        _db.SaveChanges();
        return true;
    }

    public bool TryDeleteUser(string email, out string? errorMessage)
    {
        errorMessage = null;
        email = email.Trim();
        var u = _db.AppUsers.FirstOrDefault(x => x.Email.ToLower() == email.ToLower());
        if (u == null)
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        _db.AppUsers.Remove(u);
        _db.SaveChanges();
        return true;
    }
}
