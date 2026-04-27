using System.Net;
using System.Text.Json;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.IntegrationTests;

/*
 * [TR] Bu dosya ne işe yarar:
 *      Tam HTTP pipeline'ı (routing → middleware → controller → JSON çıktı)
 *      WebApplicationFactory<Program> ile test eder.
 *      Cookie auth, TestAuthHandler ile bypass edilir.
 *
 * [TR] Neden GET kullanıyoruz:
 *      AiController.Process eyleminde [ValidateAntiForgeryToken] vardır;
 *      bunu HTTP-level test etmek anti-forgery cookie+header kombinasyonu
 *      gerektirir → controller-level entegrasyon (AiControllerIntegrationTests)
 *      bu kapsamı zaten karşılar. Burada güvenli, kolay test edilebilir
 *      GET /Ai/ModelsForTask endpoint'i tercih edildi.
 *
 * ═══════════════════════════════════════════════════════════════════════════════
 *  JÜRİ NOTLARI — INTEGRATION TEST KATMANI (HTTP-LEVEL)
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 *  TEST PİRAMİDİNDEKİ YERİ:
 *      Controller-level entegrasyonun bir üstü. Tüm ASP.NET Core middleware
 *      pipeline'ı (routing, model binding, auth, JSON çıktı, content negotiation)
 *      gerçek olarak çalıştırılır.
 *
 *  ALTYAPI:
 *      ► WebApplicationFactory<Program> in-memory test server kurar.
 *      ► Cookie auth yerine TestAuthHandler ile [Authorize] bypass edilir.
 *      ► SQLite, izole geçici dosya kullanır (TestWebApplicationFactory).
 *      ► Ai:Provider = Mock → gerçek API'ye istek gitmez.
 *      ► Email:Smtp:Enabled = false → mail gönderilmez.
 *
 *  JÜRİ Q&A (BU KATMAN İÇİN):
 *      Q: "Production ile aynı mı çalışıyor?"
 *      A: Evet, runtime'da Program.cs aynı şekilde yürütülür; sadece
 *         configuration override edilir (mock provider, geçici DB).
 *         Bu, "test as production" yaklaşımıdır.
 *
 *      Q: "Test izolasyonu nasıl?"
 *      A: Her TestWebApplicationFactory örneği kendi geçici SQLite dosyasını
 *         kullanır; testler arasında veri sızıntısı imkânsızdır. Dispose'da
 *         dosya silinir.
 *
 *      Q: "Neden Program.cs'i partial yapmak gerekti?"
 *      A: WebApplicationFactory<TEntryPoint>, TEntryPoint sınıfının test
 *         assembly'sinden erişilebilir olmasını ister. .NET 6+ top-level
 *         statements ile gizli "Program" oluştuğundan, dışa açmak için
 *         "public partial class Program {}" eklemek standart pratiktir.
 */
public class AiControllerHttpTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AiControllerHttpTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetModelsForTask_ReturnsOk_AndJsonArray()
    {
        var response = await _client.GetAsync("/Ai/ModelsForTask?task=Translate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(json));

        // [TR] JSON dizi yapısı doğrulanır; her eleman id/label/provider içermeli
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() > 0,
            "Translate task'ı destekleyen en az bir model olmalı");

        var first = doc.RootElement[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("label", out _));
        Assert.True(first.TryGetProperty("provider", out _));
    }

    [Fact]
    public async Task GetModelsForTask_WithoutTask_Returns400()
    {
        var response = await _client.GetAsync("/Ai/ModelsForTask");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetModelsForTask_WithUnknownTask_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/Ai/ModelsForTask?task=NonExistent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }
}
