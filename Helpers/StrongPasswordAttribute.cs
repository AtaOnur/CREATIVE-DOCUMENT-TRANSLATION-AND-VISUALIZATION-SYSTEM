using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace pdf_bitirme.Helpers;

/*
 * [TR] Bu dosya ne işe yarar: Parola güçlülük kuralını (büyük harf, rakam, özel karakter) doğrulayan özel attribute.
 * [TR] Neden gerekli: [StringLength] yalnızca uzunluğu kontrol eder; içerik kuralları için özel attribute gerekir.
 * [TR] İlgili: RegisterViewModel, ResetPasswordViewModel
 *
 * MODIFICATION NOTES (TR)
 * - Kural sayısı artırılabilir (örn. küçük harf zorunlu, art arda tekrar yasağı).
 * - Client-side için data-val-* attribute'ları eklenerek JS validasyonu sağlanabilir.
 * - Zorluk: Kolay.
 */
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class StrongPasswordAttribute : ValidationAttribute
{
    public StrongPasswordAttribute()
    {
        ErrorMessage = "Parola en az bir büyük harf, bir rakam ve bir özel karakter (@!#$%^&*) içermelidir.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var password = value as string;
        if (string.IsNullOrWhiteSpace(password))
            return ValidationResult.Success; // Boşluk kontrolü [Required] ile yapılıyor.

        if (!Regex.IsMatch(password, @"[A-Z]"))
            return new ValidationResult("Parola en az bir büyük harf içermelidir.");

        if (!Regex.IsMatch(password, @"[0-9]"))
            return new ValidationResult("Parola en az bir rakam içermelidir.");

        if (!Regex.IsMatch(password, @"[@!#$%^&*\(\)\-_=\+\[\]\{\};:'"",<>\.\?/\\|`~]"))
            return new ValidationResult("Parola en az bir özel karakter içermelidir (@!#$%^&* vb.).");

        return ValidationResult.Success;
    }
}
