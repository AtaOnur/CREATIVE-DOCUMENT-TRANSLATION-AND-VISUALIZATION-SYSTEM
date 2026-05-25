using System.ComponentModel.DataAnnotations;

namespace pdf_bitirme.Models;

/*
 * [TR] Bu dosya ne işe yarar: Giriş formu modeli ve doğrulama kuralları.
 * [TR] İlgili: Views/Account/Login.cshtml, AccountController
 *
 * MODIFICATION NOTES (TR)
 * - “Beni hatırla” için bool özellik ve cookie expires ayarı.
 * - İki adımlı doğrulama alanları eklenebilir.
 * - Zorluk: Kolay.
 */
public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>Giriş sonrası yönlendirme (opsiyonel).</summary>
    public string? ReturnUrl { get; set; }
}
