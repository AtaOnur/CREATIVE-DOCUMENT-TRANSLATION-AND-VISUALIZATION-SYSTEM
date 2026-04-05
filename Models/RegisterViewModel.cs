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
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gerekli.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Parola en az {2} karakter olmalı.")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarı gerekli.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    [Display(Name = "Parola (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
