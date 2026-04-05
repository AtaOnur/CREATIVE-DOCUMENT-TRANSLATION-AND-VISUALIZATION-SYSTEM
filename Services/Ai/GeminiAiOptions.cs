namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: Gemini API bağlantı ayarlarını (API key, model, token limiti) taşır.
 * [TR] Neden gerekli: Yapılandırmayı koddan ayırarak appsettings/user-secrets üzerinden yönetmek için.
 * [TR] İlgili: GeminiAiService, Program.cs, appsettings.json (Ai:Gemini bölümü)
 *
 * MODIFICATION NOTES (TR)
 * - Farklı Gemini model sürümleri (gemini-1.5-pro, gemini-2.0-flash vb.) DefaultModel ile seçilir.
 * - Temperature ve MaxOutputTokens ince ayar için buradan kontrol edilir.
 * - Gelecekte safety settings (HarmBlockThreshold) eklenebilir.
 * - Bu yapı bitirme projesi kapsamında bilinçli olarak basit tutulmuştur; sonra genişletilebilir.
 * - Zorluk: Kolay.
 */
public class GeminiAiOptions
{
    /// <summary>Google AI Studio'dan alınan API anahtarı.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gemini REST API temel adresi.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/";

    /// <summary>Varsayılan model adı (appsettings'ten okunur).</summary>
    public string DefaultModel { get; set; } = "gemini-2.0-flash";

    /// <summary>Maksimum üretilecek token sayısı.</summary>
    public int MaxOutputTokens { get; set; } = 2048;

    /// <summary>Yaratıcılık seviyesi (0.0 = deterministik, 1.0 = çok yaratıcı).</summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Görsel üretim için kullanılacak model.
    /// Boş bırakılırsa Visualize operasyonu metin prompt döner (fallback).
    /// </summary>
    public string ImageModel { get; set; } = "gemini-2.0-flash-preview-image-generation";
}
