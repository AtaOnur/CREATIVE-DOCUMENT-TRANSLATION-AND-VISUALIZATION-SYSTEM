using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using pdf_bitirme.Controllers;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;
using pdf_bitirme.Services.Ai;
using pdf_bitirme.Services.Ocr;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.IntegrationTests;

/*
 * [TR] DocumentsController — banlı belge ekranı ve workspace rehberi kapatma entegrasyon testleri.
 *      Gerçek DocumentService + SQLite; OCR/TTS bağımlılıkları mock.
 */
public class DocumentsControllerIntegrationTests
{
    private const string TestEmail = TestAuthHandler.TestUserEmail;

    private static DocumentsController BuildController(IDocumentService documentService)
    {
        var ocrMock = new Mock<IOcrService>();
        var ttsMock = new Mock<IGeminiTtsSpeechService>();
        var servicesMock = new Mock<IServiceProvider>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Models:0:Id"] = "mock-gpt",
            })
            .Build();

        var controller = new DocumentsController(
            documentService,
            ocrMock.Object,
            ttsMock.Object,
            servicesMock.Object,
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, TestEmail),
                        new Claim(ClaimTypes.Email, TestEmail),
                    ], "Test")),
                },
            },
        };

        return controller;
    }

    [Fact]
    public async Task Details_WhenDocumentBanned_ReturnsBannedView()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var doc = await ctx.SeedDocumentAsync(
            TestEmail,
            title: "Banned Thesis",
            isBanned: true,
            banReason: "Community rules violation");

        var controller = BuildController(ctx.Service);
        var result = await controller.Details(doc.Id, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Banned", viewResult.ViewName);
        var model = Assert.IsType<DocumentBannedViewModel>(viewResult.Model);
        Assert.Equal(doc.Id, model.Id);
        Assert.Equal("Banned Thesis", model.Title);
        Assert.Equal("Community rules violation", model.BanReason);
    }

    [Fact]
    public async Task DismissWorkspaceGuide_PersistsWorkspaceGuideCompleted()
    {
        await using var ctx = await DocumentServiceTestContext.CreateAsync();
        var controller = BuildController(ctx.Service);

        var result = await controller.DismissWorkspaceGuide(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var settings = await ctx.Db.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserEmail == TestEmail);
        Assert.NotNull(settings);
        Assert.True(settings!.WorkspaceGuideCompleted);
    }
}
