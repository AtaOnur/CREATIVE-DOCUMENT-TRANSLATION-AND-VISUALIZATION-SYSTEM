using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace pdf_bitirme.Services.Ai;

/*
 * [TR] Bu dosya ne işe yarar: Google AI (generativelanguage) üzerinde Gemini TTS modelleri için generateContent çağrısı.
 * [TR] Neden gerekli: OCR panelindeki düz metni sese çevirir; aynı Ai:Gemini:ApiKey ile AI metin işlemleri paylaşılabilir.
 * [TR] İlgili: IGeminiTtsSpeechService, GeminiAiOptions, DocumentsController.NarrateOcrSpeech
 *
 * MODIFICATION NOTES (TR)
 * - responseModalities yalnızca ["AUDIO"] olmalı; TEXT eklemek çoğu TTS modelinde InvalidArgument (400) verir.
 * - Model adı önizleme olabilir (gemini-*-preview-tts); Google güncelleyebilir — TtsModel appsettings’ten değiştirilir.
 * - Yanıtta inlineData yoksa kullanıcıya anlamlı hata; ayrıntı logda kalır.
 * - Zorluk: Orta.
 */

/// <summary>Gemini REST generateContent + AUDIO çıktısı için HttpClient tabanlı gerçekleştirme.</summary>
public class GeminiTtsSpeechService : IGeminiTtsSpeechService
{
    private static readonly JsonSerializerOptions TtsRequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly GeminiAiOptions _opt;
    private readonly ILogger<GeminiTtsSpeechService> _logger;

    public GeminiTtsSpeechService(
        HttpClient http,
        IOptions<GeminiAiOptions> options,
        ILogger<GeminiTtsSpeechService> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;

        var sec = Math.Clamp(_opt.TtsTimeoutSeconds, 15, 300);
        _http.Timeout = TimeSpan.FromSeconds(sec);
    }

    /// <inheritdoc />
    public async Task<GeminiSpeechSynthesisResult> SynthesizeAsync(
        string plainText,
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.ApiKey))
            throw new InvalidOperationException("Gemini API key is missing (Ai:Gemini:ApiKey or User Secrets).");

        var normalized = NormalizeTtsInput(plainText);
        var maxChars = Math.Clamp(_opt.TtsMaxTextCharacters, 250, 100_000);
        if (normalized.Length > maxChars)
            throw new InvalidOperationException($"Narration text is too long (maximum {maxChars} characters). Please shorten the text.");

        var model = string.IsNullOrWhiteSpace(_opt.TtsModel)
            ? "gemini-3.1-flash-tts-preview"
            : _opt.TtsModel.Trim();

        var voice = string.IsNullOrWhiteSpace(_opt.TtsVoiceName) ? "Kore" : _opt.TtsVoiceName.Trim();
        var lang = !string.IsNullOrWhiteSpace(languageCode)
            ? languageCode.Trim()
            : string.IsNullOrWhiteSpace(_opt.TtsLanguageCode) ? null : _opt.TtsLanguageCode.Trim();

        var url = $"{_opt.BaseUrl.TrimEnd('/')}/{model}:generateContent?key={Uri.EscapeDataString(_opt.ApiKey)}";

        // [TR] generationConfig.speechConfig: REST şemasında voiceConfig.prebuiltVoiceConfig.voiceName.
        var body = new TtsGeminiRequest
        {
            Contents =
            [
                new TtsContent { Role = "user", Parts = [new TtsPart { Text = normalized }] },
            ],
            GenerationConfig = new TtsGenerationConfigNested
            {
                // [TR] TTS modelleri yalnızca AUDIO çıktısı üretir; TEXT+AUDIO 400 ile reddedilir (ai.google.dev/speech-generation).
                ResponseModalities = ["AUDIO"],
                SpeechConfig = new TtsSpeechConfigNested
                {
                    VoiceConfig = new TtsVoiceConfigNested { PrebuiltVoiceConfig = new TtsPrebuiltVoice { VoiceName = voice } },
                    LanguageCode = lang,
                },
            },
        };

        HttpResponseMessage resp;
        try
        {
            resp = await _http.PostAsJsonAsync(url, body, TtsRequestJsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini TTS network request failed.");
            throw new InvalidOperationException("Could not connect to the Gemini narration API.", ex);
        }

        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini TTS HTTP error: {Code} - {Snippet}", (int)resp.StatusCode,
                raw.Length > 1200 ? raw[..1200] + "…" : raw);

            var apiMsg = TryExtractGeminiErrorMessage(raw);
            var hint = apiMsg ?? "No details in the response. Check the model ID, API key, or request body schema.";
            throw new InvalidOperationException(
                $"Gemini TTS rejected the request ({(int)resp.StatusCode}): {hint}");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        if (root.TryGetProperty("promptFeedback", out var pf) &&
            pf.TryGetProperty("blockReason", out var br) &&
            br.ValueKind == JsonValueKind.String &&
            br.GetString() is { } blocked && !string.IsNullOrWhiteSpace(blocked))
            throw new InvalidOperationException($"Gemini content filter: {blocked}");

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("Gemini TTS response does not contain candidate content.");

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var partsArr))
            throw new InvalidOperationException("Gemini TTS response does not contain parts.");

        foreach (var part in partsArr.EnumerateArray())
        {
            if (!part.TryGetProperty("inlineData", out var inline) &&
                !part.TryGetProperty("inline_data", out inline))
                continue;

            var mime = "application/octet-stream";
            if (inline.TryGetProperty("mimeType", out var mt) && mt.ValueKind == JsonValueKind.String)
                mime = mt.GetString() ?? mime;
            else if (inline.TryGetProperty("mime_type", out var mtSnake) && mtSnake.ValueKind == JsonValueKind.String)
                mime = mtSnake.GetString() ?? mime;

            if (!inline.TryGetProperty("data", out var dElem) || dElem.ValueKind != JsonValueKind.String)
                continue;

            var b64 = dElem.GetString();
            if (string.IsNullOrWhiteSpace(b64))
                continue;

            byte[] audio;
            try
            {
                audio = Convert.FromBase64String(b64);
            }
            catch
            {
                _logger.LogWarning("Gemini TTS inlineData base64 decoding failed.");
                continue;
            }

            if (audio.Length == 0)
                continue;

            return PrepareBrowserPlayableAudio(audio, mime);
        }

        throw new InvalidOperationException(
            "Gemini TTS did not return audio data (no inlineData or unexpected format). Try a different TtsModel.");
    }

    /// <summary>
    /// Gemini TTS çoğunlukla başlıksız ham PCM (L16 / 24 kHz / mono) verir; HTML5 Audio yalnızca WAV/MP3 vb. çalar.
    /// RIFF değilse PCM kabul edip WAV sarmalayıcı eklenir (ai.google.dev speech-generation örnekleri pcm+ffmpeg).
    /// </summary>
    private static GeminiSpeechSynthesisResult PrepareBrowserPlayableAudio(byte[] bytes, string mimeFromApi)
    {
        if (IsRiffWaveContainer(bytes))
        {
            return new GeminiSpeechSynthesisResult
            {
                AudioBytes = bytes,
                ContentType = MapAudioMimeToContentType(mimeFromApi),
            };
        }

        if (ShouldWrapPcmAsWav(mimeFromApi))
        {
            var rate = ParseSampleRateFromMime(mimeFromApi) ?? 24000;
            var wav = WrapPcm16LeMonoToWav(bytes, rate);
            return new GeminiSpeechSynthesisResult
            {
                AudioBytes = wav,
                ContentType = "audio/wav",
            };
        }

        return new GeminiSpeechSynthesisResult
        {
            AudioBytes = bytes,
            ContentType = MapAudioMimeToContentType(mimeFromApi),
        };
    }

    private static bool IsRiffWaveContainer(ReadOnlySpan<byte> b) =>
        b.Length >= 12
        && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
        && b[8] == (byte)'W' && b[9] == (byte)'A' && b[10] == (byte)'V' && b[11] == (byte)'E';

    /// <summary>TTS çıktısı bilinen sıkıştırılmış biçim değilse ham PCM varsayılır.</summary>
    private static bool ShouldWrapPcmAsWav(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime)) return true;
        if (mime.Contains("mpeg", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("mp3", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("opus", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("ogg", StringComparison.OrdinalIgnoreCase))
            return false;
        if (mime.Contains("wav", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("wave", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static int? ParseSampleRateFromMime(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime)) return null;
        var idx = mime.AsSpan().IndexOf("rate=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var tail = mime[(idx + 5)..];
        var sb = new System.Text.StringBuilder();
        foreach (var c in tail)
        {
            if (char.IsDigit(c)) sb.Append(c);
            else if (sb.Length > 0) break;
        }
        return int.TryParse(sb.ToString(), out var r) && r > 0 ? r : null;
    }

    private static byte[] WrapPcm16LeMonoToWav(ReadOnlySpan<byte> pcmS16leMono, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var dataSize = pcmS16leMono.Length;
        var riffChunkSize = 36 + dataSize;

        var buf = new byte[44 + dataSize];
        buf[0] = (byte)'R'; buf[1] = (byte)'I'; buf[2] = (byte)'F'; buf[3] = (byte)'F';
        WriteLe32(buf, 4, (uint)riffChunkSize);
        buf[8] = (byte)'W'; buf[9] = (byte)'A'; buf[10] = (byte)'V'; buf[11] = (byte)'E';
        buf[12] = (byte)'f'; buf[13] = (byte)'m'; buf[14] = (byte)'t'; buf[15] = (byte)' ';
        WriteLe32(buf, 16, 16);
        WriteLe16(buf, 20, 1);
        WriteLe16(buf, 22, channels);
        WriteLe32(buf, 24, (uint)sampleRate);
        WriteLe32(buf, 28, (uint)byteRate);
        WriteLe16(buf, 32, blockAlign);
        WriteLe16(buf, 34, bitsPerSample);
        buf[36] = (byte)'d'; buf[37] = (byte)'a'; buf[38] = (byte)'t'; buf[39] = (byte)'a';
        WriteLe32(buf, 40, (uint)dataSize);
        pcmS16leMono.CopyTo(buf.AsSpan(44));
        return buf;
    }

    private static void WriteLe32(byte[] buf, int o, uint v)
    {
        buf[o] = (byte)v;
        buf[o + 1] = (byte)(v >> 8);
        buf[o + 2] = (byte)(v >> 16);
        buf[o + 3] = (byte)(v >> 24);
    }

    private static void WriteLe16(byte[] buf, int o, short v)
    {
        buf[o] = (byte)v;
        buf[o + 1] = (byte)(v >> 8);
    }

    private static string NormalizeTtsInput(string text)
    {
        var t = text.Trim().Replace('\0', ' ');
        t = t.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        return t.Replace("\u00A0", " ").Trim();
    }

    /// <summary>Google hata gövdesi { "error": { "message": "..." } }</summary>
    private static string? TryExtractGeminiErrorMessage(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (!doc.RootElement.TryGetProperty("error", out var err))
                return null;
            if (err.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String &&
                msg.GetString() is { Length: > 0 } m)
                return m.Length > 500 ? m[..500] + "…" : m;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string MapAudioMimeToContentType(string mime)
    {
        if (string.IsNullOrWhiteSpace(mime)) return "audio/mpeg";
        if (mime.Contains("mpeg", StringComparison.OrdinalIgnoreCase) ||
            mime.Contains("mp3", StringComparison.OrdinalIgnoreCase))
            return "audio/mpeg";
        if (mime.Contains("wav", StringComparison.OrdinalIgnoreCase) ||
            mime.Contains("pcm", StringComparison.OrdinalIgnoreCase) ||
            mime.Contains("L16", StringComparison.OrdinalIgnoreCase))
            return "audio/wav";
        if (mime.Contains("opus", StringComparison.OrdinalIgnoreCase) ||
            mime.Contains("ogg", StringComparison.OrdinalIgnoreCase))
            return "audio/ogg";
        return mime;
    }

    // ── İstek DTO — JSON camelCase Gemini REST ile uyumlu ─────────────────────
    private sealed class TtsGeminiRequest
    {
        public List<TtsContent> Contents { get; set; } = [];
        [System.Text.Json.Serialization.JsonPropertyName("generationConfig")]
        public TtsGenerationConfigNested GenerationConfig { get; set; } = new();
    }

    private sealed class TtsContent
    {
        public string Role { get; set; } = "user";
        public List<TtsPart> Parts { get; set; } = [];
    }

    private sealed class TtsPart
    {
        public string Text { get; set; } = "";
    }

    private sealed class TtsGenerationConfigNested
    {
        [System.Text.Json.Serialization.JsonPropertyName("responseModalities")]
        public List<string> ResponseModalities { get; set; } = [];

        [System.Text.Json.Serialization.JsonPropertyName("speechConfig")]
        public TtsSpeechConfigNested SpeechConfig { get; set; } = new();
    }

    private sealed class TtsSpeechConfigNested
    {
        [System.Text.Json.Serialization.JsonPropertyName("voiceConfig")]
        public TtsVoiceConfigNested VoiceConfig { get; set; } = new();

        /// <summary>Örn. tr-TR — null ise özellik JSON'da yazılmaz.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [System.Text.Json.Serialization.JsonPropertyName("languageCode")]
        public string? LanguageCode { get; set; }
    }

    private sealed class TtsVoiceConfigNested
    {
        [System.Text.Json.Serialization.JsonPropertyName("prebuiltVoiceConfig")]
        public TtsPrebuiltVoice PrebuiltVoiceConfig { get; set; } = new();
    }

    private sealed class TtsPrebuiltVoice
    {
        [System.Text.Json.Serialization.JsonPropertyName("voiceName")]
        public string VoiceName { get; set; } = "";
    }
}
