using Microsoft.EntityFrameworkCore;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.UnitTests;

/*
 * [TR] DocumentService — banlı belge erişimi, workspace rehberi ve admin AI sonucu okuma testleri.
 *      SQLite + gerçek DocumentService; dış HTTP/API yok.
 *
 * JÜRİ NOTU (TR):
 *   Yeni ürün özellikleri (moderasyon ban ekranı, ilk belge rehberi, admin sonuç görüntüleme)
 *   servis katmanında birim testlerle doğrulanır.
 */
public class DocumentServiceFeatureTests
{
    private const string OwnerEmail = "owner@university.edu";
    private const string OtherEmail = "other@university.edu";

    [Fact]
    public async Task GetOwnerDocumentAccessAsync_WhenDocumentMissing_ReturnsNotFound()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();

        var result = await ctx.Service.GetOwnerDocumentAccessAsync(OwnerEmail, Guid.NewGuid());

        Assert.Equal(OwnerDocumentAccessStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetOwnerDocumentAccessAsync_WhenDocumentBanned_ReturnsBannedWithReason()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var doc = await ctx.SeedDocumentAsync(OwnerEmail, isBanned: true, banReason: "Copyright violation");

        var result = await ctx.Service.GetOwnerDocumentAccessAsync(OwnerEmail, doc.Id);

        Assert.Equal(OwnerDocumentAccessStatus.Banned, result.Status);
        Assert.Equal(doc.Title, result.Title);
        Assert.Equal("Copyright violation", result.BanReason);
    }

    [Fact]
    public async Task GetOwnerDocumentAccessAsync_WhenDocumentAllowed_ReturnsAllowed()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var doc = await ctx.SeedDocumentAsync(OwnerEmail);

        var result = await ctx.Service.GetOwnerDocumentAccessAsync(OwnerEmail, doc.Id);

        Assert.Equal(OwnerDocumentAccessStatus.Allowed, result.Status);
        Assert.Equal(doc.Title, result.Title);
    }

    [Fact]
    public async Task GetWorkspaceAsync_FirstDocument_ShowsWorkspaceGuide()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var doc = await ctx.SeedDocumentAsync(OwnerEmail, createdAtUtc: DateTime.UtcNow.AddMinutes(-5));

        var workspace = await ctx.Service.GetWorkspaceAsync(OwnerEmail, doc.Id);

        Assert.NotNull(workspace);
        Assert.True(workspace!.ShowWorkspaceGuide);
    }

    [Fact]
    public async Task GetWorkspaceAsync_SecondDocument_DoesNotShowWorkspaceGuide()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var first = await ctx.SeedDocumentAsync(OwnerEmail, title: "First", createdAtUtc: DateTime.UtcNow.AddHours(-2));
        var second = await ctx.SeedDocumentAsync(OwnerEmail, title: "Second", createdAtUtc: DateTime.UtcNow.AddHours(-1));

        var workspace = await ctx.Service.GetWorkspaceAsync(OwnerEmail, second.Id);

        Assert.NotNull(workspace);
        Assert.False(workspace!.ShowWorkspaceGuide);
        Assert.Equal(first.Id, (await ctx.Service.GetWorkspaceAsync(OwnerEmail, first.Id))!.Id);
    }

    [Fact]
    public async Task GetWorkspaceAsync_WhenGuideAlreadyCompleted_DoesNotShowWorkspaceGuide()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var doc = await ctx.SeedDocumentAsync(OwnerEmail);
        await ctx.Service.MarkWorkspaceGuideCompletedAsync(OwnerEmail);

        var workspace = await ctx.Service.GetWorkspaceAsync(OwnerEmail, doc.Id);

        Assert.NotNull(workspace);
        Assert.False(workspace!.ShowWorkspaceGuide);
    }

    [Fact]
    public async Task MarkWorkspaceGuideCompletedAsync_PersistsFlagForUser()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();

        await ctx.Service.MarkWorkspaceGuideCompletedAsync(OwnerEmail);

        var settings = await ctx.Db.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserEmail == OwnerEmail);
        Assert.NotNull(settings);
        Assert.True(settings!.WorkspaceGuideCompleted);
    }

    [Fact]
    public async Task GetAiResultPageByIdAsync_ReturnsResultWithoutOwnershipFilter()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var doc = await ctx.SeedDocumentAsync(OtherEmail, title: "Other User Doc");
        var ai = await ctx.SeedAiResultAsync(doc, OtherEmail, outputText: "Translated text");

        var page = await ctx.Service.GetAiResultPageByIdAsync(ai.Id);

        Assert.NotNull(page);
        Assert.Equal(ai.Id, page!.AiResultId);
        Assert.Equal("Other User Doc", page.DocumentTitle);
        Assert.Equal("Translated text", page.OutputText);
    }

    [Fact]
    public async Task GetAiResultPageAsync_OtherUsersResult_ReturnsNull()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var doc = await ctx.SeedDocumentAsync(OtherEmail);
        var ai = await ctx.SeedAiResultAsync(doc, OtherEmail);

        var page = await ctx.Service.GetAiResultPageAsync(OwnerEmail, ai.Id);

        Assert.Null(page);
    }
}
