using Microsoft.Extensions.Options;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: Model adına bakarak isteği doğru AI sağlayıcısına yönlendirir.
 *      Gemini modelleri      → GeminiAiService
 *      HuggingFace modelleri → HuggingFaceAiService
 *      Groq modelleri        → GroqAiService
 * [TR] Neden gerekli: Kullanıcı farklı task'ler için farklı sağlayıcılardan model seçebilsin.
 * [TR] İlgili: IAiService, GeminiAiService, HuggingFaceAiService, GroqAiService, AiOptions
 *
 * MODIFICATION NOTES (TR)
 * - Yeni sağlayıcı eklemek: Provider == "OpenAI" gibi yeni bir dal eklenir,
 *   ilgili servis yazılır, Program.cs'de kayıt edilir.
 * - Sağlayıcı bulunamazsa varsayılan olarak Gemini kullanılır (güvenli fallback).
 * - Zorluk: Kolay.
 *
 * JÜRI MODİFİKASYON NOTLARI (TR)
 * - "Aynı anda iki sağlayıcıdan sonuç alınabilir mi?" → Task.WhenAll ile paralel çağrılabilir.
 * - "Maliyet takibi eklenebilir mi?" → ActivityEntry'e provider bilgisi eklenir.
 * - "Groq ile HuggingFace farkı?" → Groq GPU tabanlı hızlı, HF CPU tabanlı ücretsiz küçük modeller.
 */
public class MultiProviderAiService : IAiService
{
    private readonly GeminiAiService _gemini;
    private readonly HuggingFaceAiService _huggingFace;
    private readonly GroqAiService _groq;
    private readonly StabilityAiService _stability;
    private readonly AiOptions _aiOptions;

    public MultiProviderAiService(
        GeminiAiService gemini,
        HuggingFaceAiService huggingFace,
        GroqAiService groq,
        StabilityAiService stability,
        IOptions<AiOptions> aiOptions)
    {
        _gemini    = gemini;
        _huggingFace = huggingFace;
        _groq      = groq;
        _stability = stability;
        _aiOptions = aiOptions.Value;
    }

    public async Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        // [TR] Model ID'sine bakarak hangi sağlayıcının kullanılacağı belirlenir.
        //      appsettings.json → Ai:Models listesinde her modelin Provider alanı kontrol edilir.
        //      Eşleşme yoksa güvenli fallback: Gemini.
        var provider = _aiOptions.Models
            .FirstOrDefault(m => string.Equals(m.Id, request.ModelName, StringComparison.OrdinalIgnoreCase))
            ?.Provider ?? "Gemini";

        return provider switch
        {
            "HuggingFace" => await _huggingFace.ProcessAsync(documentTitle, request, cancellationToken),
            "Groq"        => await _groq.ProcessAsync(documentTitle, request, cancellationToken),
            "Stability"   => await _stability.ProcessAsync(documentTitle, request, cancellationToken),
            "Gemini"      => await _gemini.ProcessAsync(documentTitle, request, cancellationToken),
            _             => await _gemini.ProcessAsync(documentTitle, request, cancellationToken)
        };
    }
}
