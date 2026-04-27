using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace pdf_bitirme.Tests.Helpers;

/// <summary>
/// [TR] WebApplicationFactory ile yapılan entegrasyon testlerinde
///      cookie tabanlı kimlik doğrulamayı bypass etmek için kullanılır.
///      Her isteği "demo@university.edu" olarak kimliklendirilmiş sayar.
///
/// [TR] Neden gerekli:
///      AiController üzerinde [Authorize] olduğundan testlerde gerçek
///      login akışı simülasyonu yerine sahte bir auth scheme kullanmak,
///      testleri hızlı ve deterministik yapar.
///
/// MODIFICATION NOTES (TR)
///   - Farklı kullanıcı / rol senaryoları için Claims listesi parametrik yapılabilir.
///   - Headers tabanlı API key auth eklenebilir.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string TestUserEmail = "demo@university.edu";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, TestUserEmail),
            new Claim(ClaimTypes.Email, TestUserEmail),
            new Claim(ClaimTypes.Role, "User"),
        };
        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
