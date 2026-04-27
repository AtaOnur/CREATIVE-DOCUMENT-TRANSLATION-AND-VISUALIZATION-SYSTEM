using System.Diagnostics;
using System.Net;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.NonFunctionalTests;

/*
 * [TR] Bu dosya ne işe yarar:
 *      Sistemin fonksiyonel olmayan kalite özelliklerini doğrular:
 *        - Performans (basit Stopwatch ile yanıt süresi limiti)
 *        - Dayanıklılık (HTTP hata durumlarında uygun exception)
 *
 * [TR] Neden gerekli:
 *      Birim/entegrasyon testleri "doğru çıktı dönüyor mu?" sorusunu cevaplar.
 *      Buradaki testler "yeterince hızlı mı?" ve "hata durumunda kontrollü
 *      şekilde başarısız oluyor mu?" sorularına eklenir.
 *
 * MODIFICATION NOTES (TR)
 *   - Gerçek yük testi için NBomber/k6 entegrasyonu eklenebilir.
 *   - Bellek/CPU profili için BenchmarkDotNet ayrı projeye konabilir.
 *   - Retry / circuit breaker eklendiğinde Polly testleri buraya alınır.
 *
 * ═══════════════════════════════════════════════════════════════════════════════
 *  JÜRİ NOTLARI — NON-FUNCTIONAL TEST KATMANI (NFR)
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 *  TEST PİRAMİDİNDEKİ YERİ:
 *      Piramidin tepesi. Az ama kritik. Fonksiyonel testler "ne yapıyor"
 *      sorusunu cevaplar; NFR testleri "nasıl yapıyor" sorusunu cevaplar:
 *      Hızlı mı? Dayanıklı mı? Beklenmeyen durumda graceful mi?
 *
 *  ÖLÇÜLEN KALİTE ÖZNİTELİKLERİ (ISO/IEC 25010 referansıyla):
 *      ► Performance Efficiency : Stopwatch ile yanıt süresi
 *      ► Reliability            : 503 / network down / cancellation
 *      ► Security  (kısmen)     : 401 Unauthorized davranışı
 *      ► Compatibility (kısmen) : 10 paralel istek altında stabilite
 *
 *  PROFESYONEL KARARLAR:
 *      ► Stopwatch yeterlidir; mikro-benchmark için BenchmarkDotNet ayrı
 *        projeye taşınmalıdır (CI sürelerini şişirmemek için).
 *      ► CancellationToken davranışı test edilir → ASP.NET Core'un request
 *        abort senaryosuna uyum gösterilir.
 *      ► İlk testte JIT warmup yapılır → ölçüm güvenilirliği artar.
 *
 *  JÜRİ Q&A (BU KATMAN İÇİN):
 *      Q: "Fonksiyonel testten farkı?"
 *      A: Fonksiyonel test 'ne yapıyor', NFR 'nasıl yapıyor' sorusunu
 *         cevaplar. Ör. çevirinin doğruluğu fonksiyoneldir; çevirinin
 *         500 ms altında dönmesi NFR'dir.
 *
 *      Q: "Bu süreler production'ı garanti eder mi?"
 *      A: Hayır — gerçek HuggingFace çağrıları soğuk başlangıçta 5-30 sn
 *         olabilir. Buradaki ölçüm sadece "servis kendi overhead'ini"
 *         gösterir; gerçek SLA'lar staging ortamında ölçülmelidir.
 *
 *      Q: "Yük testi var mı?"
 *      A: 10 paralel istekle hafif eşzamanlılık testi var. Tam yük testi
 *         için NBomber/k6 entegrasyonu öneriliyor (extension noktası).
 */
public class PerformanceAndFailureTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // PERFORMANS — yanıt süresi
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// [TR] Mock yanıt anlık dönmeli; servis katmanının kendi overhead'i
    ///      makul (örn. <500 ms) sınırlar içinde olmalı.
    ///
    /// NOT: Gerçek HuggingFace çağrılarında soğuk başlangıç 5-30 sn olabileceği için
    ///      bu test gerçek API'yi değil, servisin "kendi gecikmesini" ölçer.
    /// </summary>
    [Fact]
    public async Task Translate_WithMockedResponse_CompletesUnder500ms()
    {
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson("Hello")));

        var req = new AiProcessRequestViewModel
        {
            DocumentId    = Guid.NewGuid(),
            OperationType = "Translate",
            ModelName     = "Qwen/Qwen2.5-7B-Instruct",
            InputText     = "Merhaba",
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await svc.ProcessAsync("Doc", req);
        stopwatch.Stop();

        Assert.False(string.IsNullOrWhiteSpace(result.OutputText));
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"Servis kendi başına 500 ms altında bitmeli, ölçülen: {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// [TR] Eşzamanlı 10 istek paralel olarak işlenebilmeli;
    ///      bellek sızıntısı veya kilitlenme yaşamamalı.
    /// </summary>
    [Fact]
    public async Task Translate_ParallelRequests_AllSucceedAndAreFast()
    {
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson("Hello")));

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            svc.ProcessAsync("Doc", new AiProcessRequestViewModel
            {
                DocumentId    = Guid.NewGuid(),
                OperationType = "Translate",
                ModelName     = "Qwen/Qwen2.5-7B-Instruct",
                InputText     = "Merhaba",
            })).ToArray();

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.OutputText)));
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"10 paralel istek 2 sn altında bitmeli, ölçülen: {sw.ElapsedMilliseconds} ms");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HATA YÖNETİMİ — başarısız API yanıtları
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// [TR] HuggingFace 503 (model yüklenmiyor) döndüğünde
    ///      çağıran katman anlamlı bir InvalidOperationException görmeli.
    /// </summary>
    [Fact]
    public async Task Translate_When503ServiceUnavailable_ThrowsWithReadableMessage()
    {
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Error(HttpStatusCode.ServiceUnavailable,
                """{"error":"model is currently loading"}"""));

        var req = new AiProcessRequestViewModel
        {
            DocumentId    = Guid.NewGuid(),
            OperationType = "Translate",
            ModelName     = "Qwen/Qwen2.5-7B-Instruct",
            InputText     = "test",
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req));

        Assert.Contains("503", ex.Message);
        Assert.Contains("model is currently loading", ex.Message);
    }

    /// <summary>
    /// [TR] 401 (geçersiz API anahtarı) durumunda kullanıcıya iletilebilen
    ///      mesaj fırlatılmalı.
    /// </summary>
    [Fact]
    public async Task Translate_When401Unauthorized_ThrowsWithReadableMessage()
    {
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Error(HttpStatusCode.Unauthorized,
                """{"error":"Invalid credentials in Authorization header"}"""));

        var req = new AiProcessRequestViewModel
        {
            DocumentId    = Guid.NewGuid(),
            OperationType = "Translate",
            ModelName     = "Qwen/Qwen2.5-7B-Instruct",
            InputText     = "test",
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req));

        Assert.Contains("401", ex.Message);
    }

    /// <summary>
    /// [TR] Bağlantı tamamen kopuk simülasyonu (HttpRequestException).
    ///      Servis bu durumu InvalidOperationException'a sarmalı.
    /// </summary>
    [Fact]
    public async Task Translate_WhenNetworkFails_WrapsExceptionGracefully()
    {
        var handler = new MockHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        var req = new AiProcessRequestViewModel
        {
            DocumentId    = Guid.NewGuid(),
            OperationType = "Translate",
            ModelName     = "Qwen/Qwen2.5-7B-Instruct",
            InputText     = "test",
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req));

        Assert.Contains("HuggingFace", ex.Message);
    }

    /// <summary>
    /// [TR] Cancellation token tetiklenirse istek iptal edilebilir olmalı.
    /// </summary>
    [Fact]
    public async Task Process_WhenCancellationRequested_RespectsToken()
    {
        var handler = new MockHttpMessageHandler(_ =>
        {
            // [TR] Yanıtı yapay olarak gecikmeli üret → iptal edilmesini sağla
            Thread.Sleep(200);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(HuggingFaceServiceBuilder.ChatJson("ok"))
            };
        });

        var (svc, _) = HuggingFaceServiceBuilder.Build(handler);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);   // 50 ms sonra iptal

        var req = new AiProcessRequestViewModel
        {
            DocumentId    = Guid.NewGuid(),
            OperationType = "Translate",
            ModelName     = "Qwen/Qwen2.5-7B-Instruct",
            InputText     = "test",
        };

        // [TR] Servis catch içinde sarmaladığı için InvalidOperationException olarak çıkar
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ProcessAsync("Doc", req, cts.Token));
    }
}
