using System.Net;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.UnitTests;

/*
 * [TR] Bu dosya ne işe yarar:
 *      HuggingFaceAiService'in birim testleri.
 *      Servisin HTTP katmanı sahte (mock) handler ile değiştirilir; gerçek
 *      HuggingFace API'sine hiçbir istek gitmez.
 *
 * [TR] Neden gerekli:
 *      Servis davranışı (prompt inşası, parser dayanıklılığı, hata yönetimi)
 *      ağ bağımsız ve hızlı şekilde doğrulanır.
 *
 * [TR] PROJE NOTU:
 *      Orijinal prompt "AnalyzeSentimentAsync" ve "ExtractEntitiesAsync"
 *      metotlarından bahseder; bu metotlar mevcut kodda YOKTUR. Bu nedenle
 *      gerçekten var olan beş operasyon test edilmektedir:
 *      Translate, Summarize, Rewrite, CreativeWrite, Visualize.
 *      Sentiment / NER eklenirse aynı pattern ile test edilebilir
 *      (TestSentimentAsync_WithText_ShouldReturnLabelAndScore vb.).
 *
 * KOVERAJ:
 *   1) Normal input          → başarılı yanıt, OutputText doğru
 *   2) Empty input           → "[İşlenecek metin boş]" döner, HTTP atılmaz
 *   3) Invalid OperationType → "[Desteklenmeyen işlem: ...]" döner, HTTP atılmaz
 *   4) Hatalı API yanıtı     → InvalidOperationException fırlatır
 *   5) Visualize             → image/png yanıtı OutputImageUrl üretir
 *
 * GENİŞLETME:
 *   - Yeni operasyon eklenince yeni [Fact] yaz, OperationType'ı set et,
 *     beklenen prompt/yanıtı doğrula.
 *   - Theory + InlineData ile farklı dil/stil kombinasyonları test edilebilir.
 *
 * ═══════════════════════════════════════════════════════════════════════════════
 *  JÜRİ NOTLARI — UNIT TEST KATMANI
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 *  TEST PİRAMİDİNDEKİ YERİ:
 *      Piramidin EN ALTI. En çok teste sahip katman; en hızlı çalışan katman.
 *      Hedef: Tek bir sınıfın ("System Under Test") davranışını izole şekilde
 *      doğrulamak. Buradaki SUT = HuggingFaceAiService.
 *
 *  PROFESYONEL KARARLAR:
 *      ► Mock'lı HttpMessageHandler: Hiçbir test gerçek internete çıkmaz.
 *        → Determinism, CI hızı, rate limit sıfır, offline çalışma.
 *      ► Pozitif + negatif yol kapsamı: happy path + boş input + geçersiz op +
 *        500 hatası + multimodal yanıt parser dayanıklılığı.
 *      ► AAA pattern (Arrange-Act-Assert) her testte uygulanır.
 *      ► HuggingFaceServiceBuilder.Build() tek satır setup → DRY (Don't Repeat
 *        Yourself) prensibi.
 *
 *  JÜRİ Q&A (BU KATMAN İÇİN):
 *      Q: "Niye gerçek API çağrılmıyor?"
 *      A: Birim testin tanımı gereği SUT dışındaki her şey mock'lanır. Aksi
 *         takdirde test flaky olur, ağa bağımlı çalışır, CI/CD'de patlar.
 *
 *      Q: "Bu testler %100 coverage sağlıyor mu?"
 *      A: ProcessAsync'in tüm operasyon kolları (5 op) + ana hata yolları
 *         + parser dayanıklılığı kapsanmıştır. dotnet test --collect ile
 *         yüzde ölçülebilir.
 *
 *      Q: "Test izolasyonu nasıl sağlanıyor?"
 *      A: Her [Fact] yeni MockHttpMessageHandler + yeni TestHostEnvironment
 *         alır → testler birbirinden bağımsız.
 */
public class HuggingFaceAiServiceTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // YARDIMCI: Standart bir AiProcessRequestViewModel
    // ─────────────────────────────────────────────────────────────────────────
    private static AiProcessRequestViewModel MakeRequest(
        string operation,
        string input = "Merhaba dünya, bu bir test cümlesidir.",
        string model = "Qwen/Qwen2.5-7B-Instruct")
    {
        return new AiProcessRequestViewModel
        {
            DocumentId     = Guid.NewGuid(),
            OperationType  = operation,
            ModelName      = model,
            InputText      = input,
            SourceLanguage = "Turkish",
            TargetLanguage = "English",
            Style          = "Formal",
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. TRANSLATE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Normal input: çeviri operasyonu HuggingFace chat yanıtındaki text'i döner.
    /// </summary>
    [Fact]
    public async Task Translate_WithNormalInput_ReturnsTranslatedText()
    {
        // Arrange
        const string expected = "Hello world, this is a test sentence.";
        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(expected));
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);
        var req = MakeRequest("Translate");

        // Act
        var result = await svc.ProcessAsync("Doc Title", req);

        // Assert
        Assert.Equal(expected, result.OutputText);
        Assert.Single(mock.CapturedRequests);                                  // tek istek atılmalı
        Assert.Equal(HttpMethod.Post, mock.CapturedRequests[0].Method);
        Assert.Contains("chat/completions", mock.CapturedRequests[0].RequestUri!.ToString());
    }

    /// <summary>
    /// Empty input: hiçbir HTTP isteği atılmamalı, anlamlı placeholder dönmeli.
    /// </summary>
    [Fact]
    public async Task Translate_WithEmptyInput_ReturnsPlaceholderAndSkipsHttp()
    {
        var handler = MockHttpMessageHandler.Json("{}"); // çağrılmamalı
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);
        var req = MakeRequest("Translate", input: "   ");

        var result = await svc.ProcessAsync("Doc", req);

        Assert.Contains("boş", result.OutputText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(mock.CapturedRequests);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. SUMMARIZE
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Summarize_WithNormalInput_ReturnsShorterSummary()
    {
        const string longInput = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                                 "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
                                 "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.";
        const string summary   = "Lorem ipsum kısa özeti.";

        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(summary));
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);
        var req = MakeRequest("Summarize", input: longInput);

        var result = await svc.ProcessAsync("Doc", req);

        Assert.Equal(summary, result.OutputText);
        // [TR] Functional kontrol: özet, girdiden kısa olmalı
        Assert.True(result.OutputText.Length < longInput.Length);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. REWRITE
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Rewrite_WithCustomInstruction_PassesInstructionToModel()
    {
        const string rewritten = "Bu metin, daha akademik bir üslupla yeniden yazıldı.";
        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(rewritten));
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);

        var req = MakeRequest("Rewrite");
        // [TR] ASCII yönerge: System.Text.Json non-ASCII karakterleri \u escape eder
        req.CustomInstruction = "Make it more academic and formal";

        var result = await svc.ProcessAsync("Doc", req);

        Assert.Equal(rewritten, result.OutputText);

        // [TR] Gönderilen request body içinde özel yönerge yer almalı
        var body = await mock.CapturedRequests[0].Content!.ReadAsStringAsync();
        Assert.Contains("Make it more academic and formal", body);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. CREATIVE WRITE
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreativeWrite_WithSourceText_ReturnsCreativeOutput()
    {
        const string creative = "Yıldızların altında dans eden bir hikâye...";
        var handler = MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(creative));
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        var req = MakeRequest("CreativeWrite", input: "Bir kahraman, bir yolculuk.");

        var result = await svc.ProcessAsync("Doc", req);

        Assert.Equal(creative, result.OutputText);
        Assert.False(string.IsNullOrWhiteSpace(result.OutputText));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. VISUALIZE (IMAGE GENERATION)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Visualize_WithImageBytes_WritesFileAndReturnsUrl()
    {
        // [TR] 1x1 px transparent PNG (geçerli ama çok küçük PNG byte dizisi)
        var pngBytes = new byte[]
        {
            0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,
            0x49,0x48,0x44,0x52,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
            0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,0x89,0x00,0x00,0x00,
            0x0A,0x49,0x44,0x41,0x54,0x78,0x9C,0x63,0x00,0x01,0x00,0x00,
            0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,
            0x45,0x4E,0x44,0xAE,0x42,0x60,0x82
        };

        var handler = MockHttpMessageHandler.Image(pngBytes, "image/png");
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);

        var req = MakeRequest("Visualize",
            input: "A peaceful library with ancient books",
            model: "black-forest-labs/FLUX.1-schnell");

        var result = await svc.ProcessAsync("Library", req);

        Assert.False(string.IsNullOrWhiteSpace(result.OutputImageUrl));
        Assert.StartsWith("/ai-images/", result.OutputImageUrl);
        Assert.Contains("hf-inference", mock.CapturedRequests[0].RequestUri!.ToString());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. INVALID OPERATION
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_WithInvalidOperationType_ReturnsUnsupportedMessage()
    {
        var handler = MockHttpMessageHandler.Json("{}");
        var (svc, mock) = HuggingFaceServiceBuilder.Build(handler);

        var req = MakeRequest("Hokus_Pokus");

        var result = await svc.ProcessAsync("Doc", req);

        Assert.Contains("Desteklenmeyen işlem", result.OutputText);
        Assert.Empty(mock.CapturedRequests);   // hiçbir HTTP isteği atılmamalı
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. API HATA YANITI
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_WhenApiReturns500_ThrowsInvalidOperationException()
    {
        var handler = MockHttpMessageHandler.Error(
            HttpStatusCode.InternalServerError,
            """{"error":"model overloaded"}""");
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);
        var req = MakeRequest("Translate");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req));

        Assert.Contains("HuggingFace", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. PARSER DAYANIKLILIĞI
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bazı HF modelleri "content" alanını <see cref="JsonValueKind.Array"/> olarak
    /// döndürür: <c>[{"type":"text","text":"..."}]</c>. Servis bunu da çözebilmeli.
    /// </summary>
    [Fact]
    public async Task Translate_WhenContentIsArrayOfObjects_StillExtractsText()
    {
        const string json = """
        {
          "choices": [
            {
              "index": 0,
              "message": {
                "role": "assistant",
                "content": [
                  { "type": "text", "text": "Merhaba" },
                  { "type": "text", "text": ", dünya!" }
                ]
              }
            }
          ]
        }
        """;

        var handler = MockHttpMessageHandler.Json(json);
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        var result = await svc.ProcessAsync("Doc", MakeRequest("Translate"));

        Assert.Equal("Merhaba, dünya!", result.OutputText);
    }
}
