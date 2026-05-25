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

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters.")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
