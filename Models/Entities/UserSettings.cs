namespace pdf_bitirme.Models.Entities;

/*
 * [TR] Bu dosya ne işe yarar: Kullanıcı bazlı basit ayarları (model, stil, tema) saklar.
 * [TR] Neden gerekli: Settings ekranındaki tercihler kalıcı olsun ve tekrar girişte korunsun.
 * [TR] İlgili: SettingsController, AppDbContext, DocumentService
 *
 * MODIFICATION NOTES (TR)
 * - Bildirim ayarları ve çok dilli arayüz seçenekleri eklenebilir.
 * - API key ve gelişmiş admin ayarları eklenebilir.
 * - Genel resim OCR desteği bu sürümde yer almamaktadır.
 * - Zorluk: Kolay.
 */
public class UserSettings
{
    public Guid Id { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string DefaultAiModel { get; set; } = "mock-gpt";
    public string DefaultTranslationStyle { get; set; } = "Formal";
    public string ThemePreference { get; set; } = "System";
    public DateTime UpdatedAtUtc { get; set; }
}

