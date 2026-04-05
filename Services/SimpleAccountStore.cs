using System.Collections.Concurrent;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Bellek içi demo kullanıcı listesi ve sıfırlama jetonları — veritabanı yok.
 * [TR] Neden gerekli: Bitirme aşamasında sunum yapılabilir akış; karmaşık Identity kurulumu olmadan cookie oturumu.
 * [TR] İlgili: Program.cs (singleton kayıt), AccountController
 *
 * MODIFICATION NOTES (TR)
 * - Parolalar düz metin saklanıyor; yalnızca geliştirme/savunma içindir. Üretimde ASLA böyle kalmasın.
 * - Uygulama yeniden başlayınca kayıtlar (demo hariç) sıfırlanır; kalıcılık için DB şart.
 * - Zorluk: Kolay (demo) → Yüksek (üretim).
 */
public class SimpleAccountStore : ISimpleAccountStore
{
    private readonly ConcurrentDictionary<string, AccountRecord> _accountsByEmail =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Jeton → (e-posta, son geçerlilik UTC).</summary>
    private readonly ConcurrentDictionary<string, (string Email, DateTime ExpiresUtc)> _resetTokens = new();
    /// <summary>Jeton → (e-posta, son geçerlilik UTC).</summary>
    private readonly ConcurrentDictionary<string, (string Email, DateTime ExpiresUtc)> _emailVerifyTokens = new();

    public SimpleAccountStore()
    {
        // [TR] Önceden tanımlı demo kullanıcı hesabı.
        _accountsByEmail["demo@university.edu"] = new AccountRecord("demo123", "User", true, true, DateTime.UtcNow);
        // [TR] Basit admin hesabı (sunum için).
        _accountsByEmail["admin@university.edu"] = new AccountRecord("admin123", "Admin", true, true, DateTime.UtcNow);
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

        if (_accountsByEmail.ContainsKey(email))
        {
            errorMessage = "Bu e-posta ile zaten kayıt var.";
            return false;
        }

        _accountsByEmail[email] = new AccountRecord(password, "User", true, false, DateTime.UtcNow);
        return true;
    }

    public bool ValidateCredentials(string email, string password)
    {
        email = email.Trim();
        return _accountsByEmail.TryGetValue(email, out var u) && u.IsActive && u.IsEmailVerified && u.Password == password;
    }

    public string? CreatePasswordResetToken(string email)
    {
        email = email.Trim();
        if (!_accountsByEmail.ContainsKey(email))
            return null;

        var token = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddHours(1);
        _resetTokens[token] = (email, expires);
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

        if (!_resetTokens.TryGetValue(token, out var entry))
        {
            errorMessage = "Bağlantı geçersiz veya süresi dolmuş.";
            return false;
        }

        if (entry.ExpiresUtc < DateTime.UtcNow)
        {
            _resetTokens.TryRemove(token, out _);
            errorMessage = "Bağlantı süresi dolmuş. Yeni istek gönderin.";
            return false;
        }

        if (!_accountsByEmail.TryGetValue(entry.Email, out var oldRecord))
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        _accountsByEmail[entry.Email] = oldRecord with { Password = newPassword };
        _resetTokens.TryRemove(token, out _);
        return true;
    }

    public string? CreateEmailVerificationToken(string email)
    {
        email = email.Trim();
        if (!_accountsByEmail.ContainsKey(email))
            return null;

        var token = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow.AddHours(24);
        _emailVerifyTokens[token] = (email, expires);
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

        if (!_emailVerifyTokens.TryGetValue(token, out var entry))
        {
            errorMessage = "Bağlantı geçersiz veya süresi dolmuş.";
            return false;
        }

        if (entry.ExpiresUtc < DateTime.UtcNow)
        {
            _emailVerifyTokens.TryRemove(token, out _);
            errorMessage = "Doğrulama bağlantısının süresi dolmuş.";
            return false;
        }

        if (!_accountsByEmail.TryGetValue(entry.Email, out var oldRecord))
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        _accountsByEmail[entry.Email] = oldRecord with { IsEmailVerified = true };
        _emailVerifyTokens.TryRemove(token, out _);
        email = entry.Email;
        return true;
    }

    public string GetRole(string email)
    {
        email = email.Trim();
        return _accountsByEmail.TryGetValue(email, out var u) ? u.Role : "User";
    }

    public bool IsActive(string email)
    {
        email = email.Trim();
        return _accountsByEmail.TryGetValue(email, out var u) && u.IsActive;
    }

    public bool IsEmailVerified(string email)
    {
        email = email.Trim();
        return _accountsByEmail.TryGetValue(email, out var u) && u.IsEmailVerified;
    }

    public IReadOnlyList<SimpleAccountUserInfo> GetUsers()
    {
        return _accountsByEmail
            .Select(x => new SimpleAccountUserInfo
            {
                Email = x.Key,
                Role = x.Value.Role,
                IsActive = x.Value.IsActive,
                IsEmailVerified = x.Value.IsEmailVerified,
                CreatedAtUtc = x.Value.CreatedAtUtc,
            })
            .OrderBy(x => x.Email)
            .ToList();
    }

    public SimpleAccountUserInfo? GetUser(string email)
    {
        email = email.Trim();
        if (!_accountsByEmail.TryGetValue(email, out var u))
            return null;

        return new SimpleAccountUserInfo
        {
            Email = email,
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
        if (!_accountsByEmail.TryGetValue(email, out var oldRecord))
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        _accountsByEmail[email] = oldRecord with { IsActive = isActive };
        return true;
    }

    public bool TryDeleteUser(string email, out string? errorMessage)
    {
        errorMessage = null;
        email = email.Trim();
        if (!_accountsByEmail.TryRemove(email, out _))
        {
            errorMessage = "Kullanıcı bulunamadı.";
            return false;
        }

        return true;
    }

    private sealed record AccountRecord(
        string Password,
        string Role,
        bool IsActive,
        bool IsEmailVerified,
        DateTime CreatedAtUtc);
}
