using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using pdf_bitirme.Data;
using pdf_bitirme.Models.Entities;
using pdf_bitirme.Models;
using pdf_bitirme.Services;
using pdf_bitirme.Services.Email;

namespace pdf_bitirme.Controllers;

/*
 * [TR] Bu dosya ne işe yarar: Kimlik sayfaları — giriş, kayıt, şifre unuttum, şifre sıfırlama; cookie oturumu.
 * [TR] Neden gerekli: Savunmada uçtan uca kullanıcı akışı; basit bellek deposu ile DB’siz demo.
 * [TR] İlgili: SimpleAccountStore, Program.cs (AddAuthentication), Views/Account/*
 *
 * MODIFICATION NOTES (TR)
 * - ASP.NET Core Identity ile değiştirildiğinde çoğu eylem SignInManager / UserManager’e taşınır.
 * - Parola hash (PasswordHasher) ve kilitleme politikası üretim şartıdır.
 * - Genel görüntü OCR özelliği bu modülde yoktur; future work yalnızca belge iş akışı için geçerlidir.
 * - Zorluk: Orta (Identity geçişi).
 */
public class AccountController : Controller
{
    private readonly ISimpleAccountStore _accounts;
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;

    public AccountController(ISimpleAccountStore accounts, AppDbContext db, IEmailSender emailSender)
    {
        _accounts = accounts;
        _db = db;
        _emailSender = emailSender;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl, string? verified)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocalOrHome(returnUrl);

        // [TR] E-posta doğrulama sonrası başarı mesajı query param üzerinden gelir.
        // TempData cookie yerine ViewBag kullanmak, cookie çakışmasından kaynaklanan
        // HTTP 400 (anti-forgery token hatası) sorununu önler.
        if (verified == "1")
            ViewBag.VerifiedMessage = "E-posta doğrulaması tamamlandı. Şimdi giriş yapabilirsiniz.";

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // [TR] Adım 1: Parola ve aktiflik kontrolü (IsEmailVerified bu aşamada kontrol edilmez).
        if (!_accounts.ValidateCredentials(model.Email, model.Password))
        {
            var account = _accounts.GetUser(model.Email);
            string loginError;

            if (account == null)
                loginError = "E-posta veya parola hatalı.";
            else if (!account.IsActive)
                loginError = "Hesabınız pasif durumda. Yönetici ile iletişime geçin.";
            else
                loginError = "E-posta veya parola hatalı.";

            ModelState.AddModelError(string.Empty, loginError);
            return View(model);
        }

        // [TR] Adım 2: Parola doğru, aktif — şimdi e-posta doğrulamasını ayrıca kontrol et.
        //      Bu kontrolü ValidateCredentials'tan ayırarak EF Core tracking çakışmasını önlüyoruz.
        if (!_accounts.IsEmailVerified(model.Email))
        {
            var token = _accounts.CreateEmailVerificationToken(model.Email);
            if (!string.IsNullOrWhiteSpace(token))
            {
                var link = Url.Action(nameof(VerifyEmail), "Account", new { token }, Request.Scheme) ?? "#";
                var (sent, sendError) = await _emailSender.SendEmailVerificationAsync(model.Email, link, HttpContext.RequestAborted);
                if (sent)
                    TempData["InfoMessage"] = "Dogrulama e-postasi tekrar gonderildi.";
                else
                {
                    TempData["DemoVerifyLink"] = link;
                    TempData["InfoMessage"] = $"Mail gonderimi su an yapilamadi ({sendError}). Demo link asagida gosterildi.";
                }
            }
            ModelState.AddModelError(string.Empty, "E-posta doğrulaması tamamlanmamış. Lütfen doğrulama linkini açın.");
            return View(model);
        }

        await SignInAsync(model.Email, model.RememberMe);
        AddAuthActivity(model.Email, "Kullanıcı giriş yaptı.");
        await _db.SaveChangesAsync();
        return RedirectToLocalOrHome(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(DashboardController.Index), "Dashboard");

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (!_accounts.TryRegister(model.Email, model.Password, out var err))
        {
            ModelState.AddModelError(string.Empty, err ?? "Kayıt başarısız.");
            return View(model);
        }

        var verifyToken = _accounts.CreateEmailVerificationToken(model.Email);
        if (!string.IsNullOrWhiteSpace(verifyToken))
        {
            var verifyLink = Url.Action(nameof(VerifyEmail), "Account", new { token = verifyToken }, Request.Scheme) ?? "#";
            var (sent, sendError) = await _emailSender.SendEmailVerificationAsync(model.Email, verifyLink, HttpContext.RequestAborted);
            if (sent)
            {
                TempData["InfoMessage"] = "Kayit basarili. Dogrulama e-postasi gonderildi, sonra giris yapabilirsiniz.";
            }
            else
            {
                TempData["InfoMessage"] = $"Kayit basarili fakat mail gonderilemedi ({sendError}). Demo link ile dogrulama yapabilirsiniz.";
                TempData["DemoVerifyLink"] = verifyLink;
            }
        }
        else
        {
            TempData["InfoMessage"] = "Kayit basarili. E-posta dogrulama adiminda beklenmeyen bir sorun olustu.";
        }
        AddAuthActivity(model.Email, "Yeni kullanıcı kaydı tamamlandı (e-posta doğrulaması bekleniyor).");
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var token = _accounts.CreatePasswordResetToken(model.Email);
        if (token != null)
        {
            var link = Url.Action(nameof(ResetPassword), "Account", new { token }, Request.Scheme) ?? "#";
            TempData["DemoResetLink"] = link;
        }

        // [TR] Kullanıcı yoksa bile aynı genel mesaj — e-posta sızdırma riskini azaltır.
        TempData["ForgotGenericMessage"] =
            "Eğer bu adres sistemde kayıtlıysa, şifre sıfırlama yönergeleri gönderildi. " +
            "(Demo: aşağıdaki kutuda bağlantı yalnızca kayıtlı e-postalar için gösterilir.)";
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AuthError"] = "Geçersiz sıfırlama bağlantısı.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (!_accounts.TryResetPassword(model.Token, model.NewPassword, out var err))
        {
            ModelState.AddModelError(string.Empty, err ?? "Sıfırlama başarısız.");
            return View(model);
        }

        TempData["AuthMessage"] = "Parolanız güncellendi. Giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Çıkış herkese açık POST; çerez yoksa da güvenli şekilde yanıt verir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var email = User.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!string.IsNullOrWhiteSpace(email))
        {
            AddAuthActivity(email, "Kullanıcı çıkış yaptı.");
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string? token)
    {
        if (!_accounts.TryVerifyEmail(token ?? string.Empty, out var email, out var errorMessage))
        {
            TempData["AuthError"] = errorMessage ?? "E-posta doğrulama başarısız.";
            return RedirectToAction(nameof(Login));
        }

        // [TR] TempData yerine query parametresi kullanıyoruz.
        // TempData cookie ile anti-forgery cookie aynı anda Set-Cookie edildiğinde
        // bazı tarayıcılarda token uyuşmazlığı (HTTP 400) oluşuyordu.
        if (!string.IsNullOrWhiteSpace(email))
        {
            AddAuthActivity(email, "E-posta doğrulaması tamamlandı.");
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Login), new { verified = "1" });
    }

    private IActionResult RedirectToLocalOrHome(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(DashboardController.Index), "Dashboard");
    }

    private async Task SignInAsync(string email, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, _accounts.GetRole(email)),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(14)
                    : DateTimeOffset.UtcNow.AddHours(10),
            });
    }

    /// <summary>
    /// [TR] Giriş/çıkış/kayıt gibi kimlik olaylarını basit aktivite tablosuna ekler.
    /// </summary>
    private void AddAuthActivity(string email, string message)
    {
        _db.ActivityEntries.Add(new ActivityEntry
        {
            Id = Guid.NewGuid(),
            UserEmail = email,
            Message = message,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}
