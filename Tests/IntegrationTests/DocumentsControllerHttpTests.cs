using System.Net;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.IntegrationTests;

/*
 * [TR] DocumentsController HTTP pipeline testleri (WebApplicationFactory).
 *      Banlı belge Details GET isteği tam middleware zincirinden geçer.
 */
public class DocumentsControllerHttpTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public DocumentsControllerHttpTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDetails_BannedDocument_ReturnsBanWarningPage()
    {
        var docId = Guid.NewGuid();
        await TestDatabaseSeeder.SeedDocumentAsync(
            _factory,
            TestAuthHandler.TestUserEmail,
            id: docId,
            title: "Banned HTTP Doc",
            isBanned: true,
            banReason: "Moderation test reason");

        var response = await _client.GetAsync($"/Documents/Details/{docId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("topluluk kurallarına aykırı", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Banned HTTP Doc", html);
        Assert.Contains("Moderation test reason", html);
    }

    [Fact]
    public async Task GetDetails_AllowedDocument_ReturnsWorkspacePage()
    {
        var docId = Guid.NewGuid();
        await TestDatabaseSeeder.SeedDocumentAsync(
            _factory,
            TestAuthHandler.TestUserEmail,
            id: docId,
            title: "Allowed HTTP Doc");

        var response = await _client.GetAsync($"/Documents/Details/{docId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Allowed HTTP Doc", html);
        Assert.Contains("data-show-workspace-guide", html);
    }
}
