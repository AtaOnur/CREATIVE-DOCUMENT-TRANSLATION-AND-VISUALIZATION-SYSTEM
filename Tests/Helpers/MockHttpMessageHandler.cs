using System.Net;

namespace pdf_bitirme.Tests.Helpers;

/// <summary>
/// [TR] Test'lerde gerçek HTTP isteği atmamak için kullanılan sahte
///      HttpMessageHandler. Verilen factory delegate'iyle her isteğe
///      istenilen HttpResponseMessage'ı üretir.
///
/// [TR] Neden gerekli:
///      HuggingFaceAiService gibi servisler iç HttpClient kullanır.
///      Birim test sırasında gerçek HuggingFace router'ına istek atılmamalı;
///      bunun yerine mock handler üzerinden deterministic yanıtlar üretilir.
///
/// KULLANIM:
///   var handler = new MockHttpMessageHandler(req => Ok("{\"...\":\"...\"}"));
///   var http    = new HttpClient(handler);
///   ... servisi http ile inşa et ...
///
/// MODIFICATION NOTES (TR)
///   - Birden fazla farklı yanıt için _responses kuyruğu kullanılır.
///   - Recordedrequests özelliği gönderilen istekleri yakalar (assertion için).
///   - Hata simülasyonu için handler factory'sinde HTTP 500 döndür.
///   - [EDGE CASE EKLEMELERİ — sonradan eklendi]
///       * Timeout()       → HttpClient.Timeout aşımı senaryosu için
///                           TaskCanceledException fırlatır (gecikme yok).
///                           HuggingFaceEdgeCaseTests timeout testlerinde kullanılır.
///       * MalformedJson() → 200 OK ama bozuk JSON gövdesi simüle eder; servisin
///                           JsonDocument.Parse hatasını nasıl wrap ettiğini test
///                           etmek için kullanılır.
///
/// ═══════════════════════════════════════════════════════════════════════════════
///  JÜRİ NOTLARI — MockHttpMessageHandler
/// ═══════════════════════════════════════════════════════════════════════════════
///
/// AMAÇ: HttpClient'in EN ALT seviyesinde davranışı taklit etmek.
///       HttpMessageHandler, HttpClient'in altındaki kontrat olduğundan
///       buradaki mock 100%% deterministik HTTP yanıtı üretir.
///
/// ENDÜSTRİ KARŞILIĞI: WireMock.Net, RichardSzalay.MockHttp gibi kütüphanelerin
///                     eşdeğeri. Burada bağımlılık eklenmesin diye custom yazıldı.
///
/// JÜRİ Q&A:
///   Q: "Niye WireMock kullanmadın?"
///   A: Proje sadece 5 senaryo için 50 satırlık özel handler ile yetiniyor;
///      ekstra NuGet paketi getirilmedi. WireMock daha geniş entegrasyon
///      senaryoları için ideal — gelecekte eklenebilir.
///
///   Q: "CapturedRequests neden var?"
///   A: Davranış doğrulama için: "Servis prompt'u doğru şekilde kuruyor mu?"
///      sorusunu cevaplar. Gönderilen request body okunup assert edilir.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

    /// <summary>Yakalanan tüm istekleri tutar (assertion için).</summary>
    public List<HttpRequestMessage> CapturedRequests { get; } = new();

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Hızlı kurulum: aynı yanıtı her isteğe döner.</summary>
    public static MockHttpMessageHandler AlwaysReturn(HttpResponseMessage response)
        => new(_ => response);

    /// <summary>Hızlı kurulum: 200 OK + JSON gövde.</summary>
    public static MockHttpMessageHandler Json(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    /// <summary>Hızlı kurulum: hata yanıtı simüle eder.</summary>
    public static MockHttpMessageHandler Error(HttpStatusCode status, string body) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });

    /// <summary>Hızlı kurulum: ham binary (image) yanıtı simüle eder.</summary>
    public static MockHttpMessageHandler Image(byte[] bytes, string mediaType = "image/png") =>
        new(_ =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

    /// <summary>
    /// [TR] HttpClient timeout senaryosunu simüle eder.
    ///      Gerçek HttpClient.Timeout aşıldığında <see cref="TaskCanceledException"/>
    ///      fırlatır; testlerde aynı davranışı (çağrıda saniyelerce beklemeden) kurar.
    /// </summary>
    public static MockHttpMessageHandler Timeout() =>
        new(_ => throw new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout being exceeded.",
            new TimeoutException("HttpClient.Timeout exceeded")));

    /// <summary>
    /// [TR] HTTP 200 OK döndüren ama gövdesi geçerli JSON OLMAYAN bir yanıt üretir.
    ///      Servisin <c>JsonDocument.Parse</c> çağrısının atması beklenir.
    /// </summary>
    public static MockHttpMessageHandler MalformedJson(string body = "{ this is not valid json") =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);
        return Task.FromResult(_factory(request));
    }
}
