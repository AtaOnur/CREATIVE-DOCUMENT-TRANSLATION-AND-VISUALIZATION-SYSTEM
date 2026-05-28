namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: OCR metninin Google Gemini TTS ile sese çevrilmesinin sözleşmesi.
 * [TR] Neden gerekli: Workspace seslendirme akışında controller Gemini generateContent (AUDIO) kullanır.
 * [TR] İlgili: GeminiTtsSpeechService, GeminiAiOptions (Ai:Gemini:TtsModel, ApiKey vb.), DocumentsController.NarrateOcrSpeech
 *
 * MODIFICATION NOTES (TR)
 * - Model/Google kotası nedeniyle yanıtta WAV gelebilir; ContentType MIME ile bildirilir.
 * - Gemini TTS model adları zamanla değişebilir — TtsModel appsettings ile güncellenir.
 * - Zorluk: Kolay.
 */

/// <summary>OCR çıktısı için sentez sonucu; tarayıcı File() ile doğru Content-Type gerektirir.</summary>
public sealed class GeminiSpeechSynthesisResult
{
    /// <summary>Ham ses baytları (MP3 veya WAV olabilir — ContentType’a bak).</summary>
    public required byte[] AudioBytes { get; init; }

    /// <summary>Örn. audio/mpeg veya audio/wav.</summary>
    public required string ContentType { get; init; }
}

public interface IGeminiTtsSpeechService
{
    /// <summary>
    /// [TR] Gemini TTS önizleme/üretim modeli ile generateContent çağırır (responseModalities: AUDIO).
    /// </summary>
    Task<GeminiSpeechSynthesisResult> SynthesizeAsync(string plainText, CancellationToken cancellationToken = default);
}
