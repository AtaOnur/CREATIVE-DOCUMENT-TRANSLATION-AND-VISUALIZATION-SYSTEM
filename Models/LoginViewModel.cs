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
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }

    /// <summary>Giriş sonrası yönlendirme (opsiyonel).</summary>
    public string? ReturnUrl { get; set; }
}
