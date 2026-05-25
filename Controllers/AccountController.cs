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
            ViewBag.VerifiedMessage = "Email verification completed. You can now log in.";

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
                loginError = "Email or password is incorrect.";
            else if (!account.IsActive)
                loginError = "Your account is inactive. Please contact an administrator.";
            else
                loginError = "Email or password is incorrect.";

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
            ModelState.AddModelError(string.Empty, "Email verification is not complete. Please open the verification link.");
            return View(model);
        }

        await SignInAsync(model.Email, model.RememberMe);
        AddAuthActivity(model.Email, "User logged in.");
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
            ModelState.AddModelError(string.Empty, err ?? "Registration failed.");
            return View(model);
        }

        var verifyToken = _accounts.CreateEmailVerificationToken(model.Email);
        if (!string.IsNullOrWhiteSpace(verifyToken))
        {
            var verifyLink = Url.Action(nameof(VerifyEmail), "Account", new { token = verifyToken }, Request.Scheme) ?? "#";
            var (sent, sendError) = await _emailSender.SendEmailVerificationAsync(model.Email, verifyLink, HttpContext.RequestAborted);
            if (sent)
            {
                TempData["InfoMessage"] = "Registration successful. A verification email was sent; you can log in afterward.";
            }
            else
            {
                TempData["InfoMessage"] = $"Registration successful, but email could not be sent ({sendError}). You can verify using the demo link.";
                TempData["DemoVerifyLink"] = verifyLink;
            }
        }
        else
        {
            TempData["InfoMessage"] = "Registration successful. An unexpected issue occurred during email verification setup.";
        }
        AddAuthActivity(model.Email, "New user registration completed (email verification pending).");
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
            "If this address is registered in the system, password reset instructions have been sent. " +
            "(Demo: the link below is shown only for registered emails.)";
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AuthError"] = "Invalid reset link.";
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
            ModelState.AddModelError(string.Empty, err ?? "Reset failed.");
            return View(model);
        }

        TempData["AuthMessage"] = "Your password has been updated. You can log in.";
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
            AddAuthActivity(email, "User logged out.");
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
            TempData["AuthError"] = errorMessage ?? "Email verification failed.";
            return RedirectToAction(nameof(Login));
        }

        // [TR] TempData yerine query parametresi kullanıyoruz.
        // TempData cookie ile anti-forgery cookie aynı anda Set-Cookie edildiğinde
        // bazı tarayıcılarda token uyuşmazlığı (HTTP 400) oluşuyordu.
        if (!string.IsNullOrWhiteSpace(email))
        {
            AddAuthActivity(email, "Email verification completed.");
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
