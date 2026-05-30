using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using pdf_bitirme.Controllers;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;
using pdf_bitirme.Services.Ai;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.IntegrationTests;

/*
 * [TR] AiController.Result — Admin rolü başka kullanıcının AI sonucunu salt okunur görüntüleyebilir;
 *      normal kullanıcı yalnızca kendi sonucuna erişir.
 */
public class AiControllerAdminResultTests
{
    private static AiController BuildController(IDocumentService documentService, bool isAdmin)
    {
        var aiMock = new Mock<IAiService>();
        var options = Options.Create(new AiOptions { Models = [] });

        var roles = isAdmin
            ? new[] { new Claim(ClaimTypes.Role, "Admin") }
            : Array.Empty<Claim>();

        var controller = new AiController(documentService, aiMock.Object, options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, TestAuthHandler.TestUserEmail),
                        new Claim(ClaimTypes.Email, TestAuthHandler.TestUserEmail),
                        ..roles,
                    ], "Test")),
                },
            },
        };

        return controller;
    }

    [Fact]
    public async Task Result_WhenAdmin_UsesGetAiResultPageByIdAsync()
    {
        var aiResultId = Guid.NewGuid();
        var docMock = new Mock<IDocumentService>();
        docMock.Setup(s => s.GetAiResultPageByIdAsync(aiResultId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResultPageViewModel
            {
                AiResultId = aiResultId,
                DocumentTitle = "Moderated Document",
                OutputText = "Admin-visible output",
            });

        var controller = BuildController(docMock.Object, isAdmin: true);
        var result = await controller.Result(aiResultId, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AiResultPageViewModel>(viewResult.Model);
        Assert.Equal("Moderated Document", model.DocumentTitle);
        Assert.True((bool)controller.ViewBag.IsAdminModerationView!);
        docMock.Verify(s => s.GetAiResultPageByIdAsync(aiResultId, It.IsAny<CancellationToken>()), Times.Once);
        docMock.Verify(
            s => s.GetAiResultPageAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Result_WhenNotAdmin_UsesOwnershipScopedLookup()
    {
        var aiResultId = Guid.NewGuid();
        var docMock = new Mock<IDocumentService>();
        docMock.Setup(s => s.GetAiResultPageAsync(TestAuthHandler.TestUserEmail, aiResultId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResultPageViewModel { AiResultId = aiResultId, OutputText = "Owner output" });

        var controller = BuildController(docMock.Object, isAdmin: false);
        var result = await controller.Result(aiResultId, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        docMock.Verify(
            s => s.GetAiResultPageAsync(TestAuthHandler.TestUserEmail, aiResultId, It.IsAny<CancellationToken>()),
            Times.Once);
        docMock.Verify(s => s.GetAiResultPageByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Result_WhenNotAdminAndOtherUsersResult_ReturnsNotFound()
    {
        var aiResultId = Guid.NewGuid();
        var docMock = new Mock<IDocumentService>();
        docMock.Setup(s => s.GetAiResultPageAsync(TestAuthHandler.TestUserEmail, aiResultId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiResultPageViewModel?)null);

        var controller = BuildController(docMock.Object, isAdmin: false);
        var result = await controller.Result(aiResultId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
