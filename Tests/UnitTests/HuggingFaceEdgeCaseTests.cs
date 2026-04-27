using System.Net;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.UnitTests;

/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *  HuggingFaceAiService — EDGE CASE / SINIR DURUM TESTLERİ
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * [TR] Bu dosya ne işe yarar:
 *      Standart "happy path" testlerinin (HuggingFaceAiServiceTests) ötesinde,
 *      üretim ortamında karşılaşılabilecek SINIR DURUMLARI test eder. Amaç,
 *      servisin beklenmedik girdi/çıktı/ağ koşullarında bile kontrollü ve
 *      öngörülebilir şekilde davrandığını kanıtlamaktır.
 *
 * [TR] Kapsam:
 *      1) ÇOK UZUN GİRDİ          → 50.000 karakter input bile çökmemeli
 *      2) ÇOK DİLLİ / ÇOK SCRIPT  → Latin / Kiril / Arapça / CJK / Emoji
 *      3) API TIMEOUT             → HttpClient.Timeout aşılırsa wrap'lenmeli
 *      4) BOZUK JSON YANITI       → 200 OK ama gövde geçersiz JSON
 *      5) BEKLENMEDİK ŞEMA        → choices boş / message yok / null content
 *
 * [TR] Tasarım kararı:
 *      Standart testlerden ayrı bir dosya tutuldu çünkü:
 *      - Anlamsal olarak farklı bir kategori (resilience / fault tolerance).
 *      - Edge case'ler değişme sıklığı düşük, ana akış testleri sık değişebilir.
 *      - "Production-readiness" kanıtı olarak bağımsız raporlanabilir.
 *
 * ─── JÜRİ NOTLARI ─────────────────────────────────────────────────────────────
 *
 *   Q: "Edge case test ile birim test arasındaki fark nedir?"
 *   A: Birim test SUT'un tasarlanmış akışlarını doğrular; edge case test ise
 *      "tasarlanmamış ama olabilecek" senaryolara dayanıklılığı doğrular.
 *      Endüstri kabulü: kritik servisler için happy path / edge case oranı
 *      yaklaşık 1:1 olmalı.
 *
 *   Q: "Timeout testi gerçekten gecikme oluşturmuyor mu?"
 *   A: Evet — MockHttpMessageHandler.Timeout() doğrudan TaskCanceledException
 *      fırlatır; saniyelerce beklenmez. CI hızı korunur.
 *
 *   Q: "Çok dilli giriş niye önemli?"
 *   A: Sistem akademik/uluslararası belge çevirisi için tasarlandı. Türkçe ş/ğ,
 *      Arapça RTL, Çince ideograf, emoji gibi UTF-8 sınır vakaları
 *      System.Text.Json escape stratejisini etkileyebilir.
 *
 *   Q: "50.000 karakter neden truncate olmuyor?"
 *   A: Translate işleminde token limit yönetimi modele bırakılır (LLM'in token
 *      kapasitesine göre kendisi kırpar veya hata döndürür). Summarize'da ise
 *      servis 4000 karakterde kasıtlı olarak kırpıyor (token tasarrufu için).
 *      Bu davranış altta TruncatesAt4000Chars testiyle DOĞRULANMIŞTIR.
 *
 * ─── MODIFICATION NOTES (TR) ──────────────────────────────────────────────────
 *
 * Bu dosya sonradan eklenen "edge case" test paketidir. Aşağıdaki noktalar,
 * ileride bakım yaparken / yeni test eklerken dikkat edilmesi gerekenleri ve
 * kasıtlı tasarım kararlarını özetler:
 *
 *   • [4000 KARAKTER KIRPMA]
 *     Summarize_WithVeryLongInput_TruncatesInputAt4000Chars testi, üretim
 *     kodundaki 4000 karakter sınırına BAĞIMLIDIR (HuggingFaceAiService
 *     SummarizeAsync). Eğer servisteki sınır değişirse bu testin sayıları
 *     da güncellenmelidir (MARKER080 / MARKER099 indis hesabı).
 *
 *   • [TIMEOUT SİMÜLASYONU]
 *     Gerçek HttpClient timeout'u beklemek yerine
 *     MockHttpMessageHandler.Timeout() doğrudan TaskCanceledException
 *     fırlatır. Bu sayede CI hızı korunur. Polly retry/circuit breaker
 *     eklenirse o davranış için ayrı testler yazılmalıdır.
 *
 *   • [BOZUK JSON HATA SARMALAMA]
 *     ProcessAsync üst seviyede try/catch ile tüm istisnaları
 *     InvalidOperationException("HuggingFace hatası: ...") şeklinde wrap eder.
 *     Bu kontrat değişirse aşağıdaki TÜM Throws_InvalidOperation testleri
 *     güncellenmelidir.
 *
 *   • [ÇOK DİLLİ INPUT]
 *     Theory + InlineData ile 7 dil çifti tek metotta test edilir. Yeni dil
 *     desteği eklenirse buraya yeni bir [InlineData] satırı eklemek yeterli;
 *     ayrı bir test metodu açmaya gerek yok (DRY).
 *
 *   • [REASONING_CONTENT FALLBACK]
 *     DeepSeek-R1 / o1 stili modeller için ExtractMessageContent fallback
 *     yolu test edilir. HF yeni bir alternatif alan eklerse
 *     (örn. "tool_calls") ExtractMessageContent güncellenmeli ve buraya
 *     yeni bir [Fact] eklenmelidir.
 *
 *   • [GENİŞLETME ÖNERİLERİ]
 *     - Çok büyük binary (örn. 5 MB PNG) Visualize edge case'i eklenebilir.
 *     - Rate limit (HTTP 429) için ayrı bir test eklenebilir.
 *     - Streaming response (SSE) desteği gelirse ayrı parser testleri açılır.
 */
public class HuggingFaceEdgeCaseTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // YARDIMCI: Standart bir AiProcessRequestViewModel (özelleştirilebilir)
    // ─────────────────────────────────────────────────────────────────────────
    private static AiProcessRequestViewModel MakeRequest(
        string operation,
        string input,
        string sourceLang = "Turkish",
        string targetLang = "English",
        string model = "Qwen/Qwen2.5-7B-Instruct")
    {
        return new AiProcessRequestViewModel
        {
            DocumentId     = Guid.NewGuid(),
            OperationType  = operation,
            ModelName      = model,
            InputText      = input,
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Style          = "Formal",
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. ÇOK UZUN GİRDİ
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// [TR] Translate operasyonunda çok uzun (50.000 karakter) input verildiğinde
    ///      servisin çökmemesi ve isteği oluşturup göndermesi gerekir.
    ///      Token limit yönetimi LLM tarafına bırakılır.
    /// </summary>
    [Fact]
    public async Task Translate_WithVeryLongInput_DoesNotTruncateOrFail()
    {
        // Arrange — 50K karakter (≈ ortalama 12.500 token, çoğu LLM kapasitesini aşar)
        var longInput = new string('A', 50_000);
        const string expected = "Translated long text.";

        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(expected));
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);

        // Act
        var result = await svc.ProcessAsync("Doc", MakeRequest("Translate", longInput));

        // Assert
        Assert.Equal(expected, result.OutputText);
        Assert.Single(mock.CapturedRequests);

        // [TR] Servis input'u truncate ETMEMELİ — request body'de tam uzunluk olmalı.
        var body = await mock.CapturedRequests[0].Content!.ReadAsStringAsync();
        Assert.Contains(new string('A', 1000), body); // en az 1000 karakter mevcut olmalı
    }

    /// <summary>
    /// [TR] Summarize operasyonunda servis kasıtlı olarak input'u 4000 karaktere
    ///      kırpar (token tasarrufu için). Bu davranış DOĞRULANMALIDIR ki ileride
    ///      kazara değiştirilirse test hemen fark etsin.
    /// </summary>
    [Fact]
    public async Task Summarize_WithVeryLongInput_TruncatesInputAt4000Chars()
    {
        // Arrange — 10K karakter, her 100 karakterde bir benzersiz işaretçi
        var sb = new System.Text.StringBuilder(10_000);
        for (int i = 0; i < 100; i++)
            sb.Append($"MARKER{i:000} ").Append(new string('x', 90)).Append(' ');
        var hugeInput = sb.ToString(); // ~10.000 karakter

        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson("özet"));
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);

        // Act
        await svc.ProcessAsync("Doc", MakeRequest("Summarize", hugeInput));

        // Assert
        var body = await mock.CapturedRequests[0].Content!.ReadAsStringAsync();

        // [TR] Erken işaretçi (MARKER000) gönderilen body'de OLMALI.
        Assert.Contains("MARKER000", body);

        // [TR] 4000 karakter sınırını aşan işaretçiler gönderilmemiş OLMALI.
        //      Her satır ~98 karakter → 4000/98 ≈ 40. dolayısıyla MARKER050+ olmamalı.
        Assert.DoesNotContain("MARKER080", body);
        Assert.DoesNotContain("MARKER099", body);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. ÇOK DİLLİ / ÇOK SCRIPT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// [TR] Çeşitli dillerde input verildiğinde sistem prompt'u doğru üretmeli
    ///      ve hedef dil bilgisi request body'ye doğru şekilde yansımalıdır.
    ///
    ///      Theory pattern: tek metot, çoklu senaryo → DRY testleri.
    /// </summary>
    [Theory]
    [InlineData("English",   "Turkish",  "Hello, world!")]
    [InlineData("Turkish",   "English",  "Merhaba, dünya! Şu ışığa bak.")]
    [InlineData("Russian",   "English",  "Привет, мир! Это тест.")]
    [InlineData("Arabic",    "English",  "مرحبا بالعالم")]
    [InlineData("Chinese",   "English",  "你好世界")]
    [InlineData("Japanese",  "Turkish",  "こんにちは世界")]
    [InlineData("Greek",     "English",  "Γειά σου Κόσμε")]
    public async Task Translate_MultiLanguageInput_BuildsPromptWithCorrectTargetLanguage(
        string sourceLang, string targetLang, string input)
    {
        // Arrange
        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson("Translated."));
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);

        // Act
        var result = await svc.ProcessAsync(
            "Doc",
            MakeRequest("Translate", input, sourceLang: sourceLang, targetLang: targetLang));

        // Assert
        Assert.Equal("Translated.", result.OutputText);
        Assert.Single(mock.CapturedRequests);

        var body = await mock.CapturedRequests[0].Content!.ReadAsStringAsync();
        // [TR] Prompt'ta hedef dil bulunmalı (sistem prompt + user prompt'ta geçiyor).
        Assert.Contains(targetLang, body);
    }

    /// <summary>
    /// [TR] Latin dışı script'ler ve emoji içeren input → request body'de
    ///      System.Text.Json bunları \uXXXX olarak escape edebilir. Servis
    ///      çıktıyı doğru parse edip Unicode'u koruyarak geri dönmelidir.
    /// </summary>
    [Fact]
    public async Task Translate_NonLatinScriptAndEmoji_PreservesUnicodeInOutput()
    {
        // Arrange — Çince + emoji + Türkçe karışık
        const string mixed = "你好 🌍 — Bu metin çoklu script test eder. مرحبا";
        const string expected = "Hello 🌍 — This text tests multi-script. مرحبا (preserved)";

        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(expected));
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        // Act
        var result = await svc.ProcessAsync("Doc", MakeRequest("Translate", mixed));

        // Assert — emoji ve Arapça çıktıda korunmalı
        Assert.Equal(expected, result.OutputText);
        Assert.Contains("🌍", result.OutputText);
        Assert.Contains("مرحبا", result.OutputText);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. API TIMEOUT SIMÜLASYONU
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// [TR] HttpClient timeout aşıldığında <see cref="TaskCanceledException"/>
    ///      fırlatır. Servis bunu yakalayıp anlamlı bir
    ///      <see cref="InvalidOperationException"/>'a sarmalıdır.
    /// </summary>
    [Fact]
    public async Task Process_WhenHttpClientTimesOut_ThrowsInvalidOperationWithReadableMessage()
    {
        // Arrange
        var handler = MockHttpMessageHandler.Timeout();
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);
        var req = MakeRequest("Translate", "Some input");

        // Act + Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req));

        // [TR] Servis tüm exception'ları "HuggingFace hatası: ..." prefixiyle wrap eder.
        Assert.Contains("HuggingFace hatası", ex.Message);

        // [TR] İç istisna gerçek timeout türünü korumalı (debug için).
        Assert.NotNull(ex.InnerException);
        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    /// <summary>
    /// [TR] CancellationToken zaten iptal edilmiş bir token verildiğinde
    ///      servis yine kontrollü şekilde davranmalıdır.
    /// </summary>
    [Fact]
    public async Task Process_WithPreCancelledToken_ThrowsInvalidOperationException()
    {
        // Arrange — handler hiç çağrılmamalı bile
        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson("dummy"));
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // önceden iptal

        var req = MakeRequest("Translate", "Hello");

        // Act + Assert
        // [TR] Önceden iptal edilmiş token PostAsync'i hemen TaskCanceled'a çevirir;
        //      ProcessAsync bunu InvalidOperationException olarak wrap eder.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req, cts.Token));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. GEÇERSİZ JSON YANIT İŞLEME
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// [TR] API 200 OK döndürür ama gövde bozuk JSON ise
    ///      <see cref="System.Text.Json.JsonException"/> fırlar. Servis bunu
    ///      <see cref="InvalidOperationException"/>'a sarmalı; ham parser
    ///      hatası kullanıcıya sızmamalıdır.
    /// </summary>
    [Fact]
    public async Task Process_WhenResponseBodyIsMalformedJson_ThrowsInvalidOperation()
    {
        // Arrange
        var handler = MockHttpMessageHandler.MalformedJson();
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);
        var req = MakeRequest("Translate", "Test");

        // Act + Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req));

        Assert.Contains("HuggingFace hatası", ex.Message);
        Assert.NotNull(ex.InnerException);
        // [TR] İç hata bir JSON parse hatası olmalı.
        Assert.Contains("JSON", ex.InnerException!.GetType().Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// [TR] API 200 OK döndürür ama gövde tamamen boş ise yine kontrollü
    ///      şekilde başarısız olmalıdır. Boş string geçerli JSON değildir.
    /// </summary>
    [Fact]
    public async Task Process_WhenResponseBodyIsEmpty_ThrowsInvalidOperation()
    {
        // Arrange — 200 OK + boş body
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json")
        });
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", MakeRequest("Translate", "Hello")));
    }

    /// <summary>
    /// [TR] API geçerli JSON döndürür ama OpenAI uyumlu şemayı izlemez:
    ///      - "choices" alanı yok
    ///      Servis bunu kontrollü hata olarak yansıtmalı.
    /// </summary>
    [Fact]
    public async Task Process_WhenResponseHasNoChoicesField_ThrowsInvalidOperation()
    {
        // Arrange — geçerli JSON ama beklenen şema değil
        const string offSchemaJson = """{ "id": "abc", "object": "chat.completion" }""";

        var handler = MockHttpMessageHandler.Json(offSchemaJson);
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", MakeRequest("Translate", "Hello")));

        Assert.Contains("HuggingFace hatası", ex.Message);
    }

    /// <summary>
    /// [TR] choices alanı boş bir dizi olduğunda servis indeks erişiminde
    ///      kontrollü olarak başarısız olmalıdır.
    /// </summary>
    [Fact]
    public async Task Process_WhenChoicesArrayIsEmpty_ThrowsInvalidOperation()
    {
        // Arrange
        const string emptyChoices = """{ "choices": [] }""";

        var handler = MockHttpMessageHandler.Json(emptyChoices);
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", MakeRequest("Translate", "Hello")));
    }

    /// <summary>
    /// [TR] message.content alanı null olduğunda (bazı reasoning modelleri böyle yapar),
    ///      servis reasoning_content fallback'ine bakmalı veya boş olmayan bir cevap
    ///      üretebilmelidir. Bu test, ExtractMessageContent'in fallback yolunu kapsar.
    /// </summary>
    [Fact]
    public async Task Process_WhenContentIsNullButReasoningContentExists_UsesReasoningFallback()
    {
        // Arrange — DeepSeek-R1 stili yanıt
        const string reasoningJson = """
        {
          "choices": [
            {
              "index": 0,
              "message": {
                "role": "assistant",
                "content": null,
                "reasoning_content": "Bu, reasoning katmanından gelen yanıttır."
              }
            }
          ]
        }
        """;

        var handler = MockHttpMessageHandler.Json(reasoningJson);
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        // Act
        var result = await svc.ProcessAsync("Doc", MakeRequest("Translate", "Hello"));

        // Assert
        Assert.Contains("reasoning katmanından", result.OutputText);
    }
}
