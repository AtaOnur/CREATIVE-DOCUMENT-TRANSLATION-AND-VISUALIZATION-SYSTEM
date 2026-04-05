using System.ComponentModel.DataAnnotations;

namespace pdf_bitirme.Models;

/*
 * [TR] Bu dosya ne işe yarar: “Şifremi unuttum” formu — yalnızca e-posta toplar.
 * [TR] İlgili: Views/Account/ForgotPassword.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Gerçek gönderimde servis katmanı e-posta kuyruğunu tetikler; burada demo jeton linki gösterilir.
 * - Zorluk: Kolay.
 */
public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;
}
