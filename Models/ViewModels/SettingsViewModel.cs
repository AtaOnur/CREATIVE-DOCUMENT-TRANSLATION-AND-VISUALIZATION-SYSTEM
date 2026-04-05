using System.ComponentModel.DataAnnotations;

namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Kullanıcı ayarları ekranı modeli.
 * [TR] Neden gerekli: Hesap bilgisi ve varsayılan AI tercihlerini tek formda toplar.
 * [TR] İlgili: SettingsController, Views/Settings/Index.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Çok dilli arayüz ve bildirim ayarları eklenebilir.
 * - API key ve gelişmiş güvenlik ayarları eklenebilir.
 * - Genel resim OCR desteği bu sürümde yer almamaktadır.
 * - Zorluk: Kolay.
 */
public class SettingsViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string DefaultAiModel { get; set; } = "mock-gpt";

    [Required, StringLength(64)]
    public string DefaultTranslationStyle { get; set; } = "Formal";

    [Required, StringLength(32)]
    public string ThemePreference { get; set; } = "System";

    public List<string> AvailableAiModels { get; set; } = new();
}

