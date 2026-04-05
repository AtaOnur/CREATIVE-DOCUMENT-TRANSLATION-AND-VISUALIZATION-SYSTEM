using System.ComponentModel.DataAnnotations;
using pdf_bitirme.Helpers;

namespace pdf_bitirme.Models;

/*
 * [TR] Bu dosya ne işe yarar: Yeni parola belirleme formu — e-postadan gelen jeton gizli alanda taşın
 * [TR] İlgili: Views/Account/ResetPassword.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Jeton URL’de kalabilir (GET) veya yalnızca POST gövdesinde taşınmalı (güvenlik ince ayarı).
 * - Zorluk: Kolay.
 */
public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola gerekli.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Parola en az {2} karakter olmalı.")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarı gerekli.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Parolalar eşleşmiyor.")]
    [Display(Name = "Yeni parola (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
