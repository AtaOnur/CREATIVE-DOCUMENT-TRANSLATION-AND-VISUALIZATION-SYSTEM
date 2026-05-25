using System.ComponentModel.DataAnnotations;
using pdf_bitirme.Helpers;

namespace pdf_bitirme.Models;

/*
 * [TR] Bu dosya ne işe yarar: Kayıt formu modeli.
 * [TR] İlgili: Views/Account/Register.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Parola karmaşıklığı (büyük harf, rakam) kuralları güçlendirilebilir.
 * - Kullanıcı adı / tam ad alanları.
 * - Zorluk: Kolay.
 */
public class RegisterViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters.")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
