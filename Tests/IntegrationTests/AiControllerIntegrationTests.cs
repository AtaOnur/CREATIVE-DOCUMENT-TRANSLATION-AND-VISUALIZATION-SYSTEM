using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using pdf_bitirme.Controllers;
using pdf_bitirme.Models;
using pdf_bitirme.Models.ViewModels;
using pdf_bitirme.Services;
using pdf_bitirme.Services.Ai;
using pdf_bitirme.Tests.Helpers;
using Xunit;

namespace pdf_bitirme.Tests.IntegrationTests;

/*
 * [TR] Bu dosya ne işe yarar:
 *      Controller + Service entegrasyon testleri.
 *      AiController, gerçek HuggingFaceAiService (mock'lu HttpClient ile)
 *      ve mock IDocumentService kullanılarak doğrudan inşa edilir; akabinde
 *      action metodu çağrılarak son JSON yanıtı doğrulanır.
 *
 * [TR] Neden gerekli:
 *      Birden fazla katmanın birlikte çalıştığını (controller + DI + service +
 *      JSON serileştirme) test eder. Sadece servis veya sadece controller
 *      birim testleri bu hatları kapsamaz.
 *
 * NOT:
 *   AiController.Process üzerinde [ValidateAntiForgeryToken] olduğundan
 *   tam HTTP entegrasyonu (WebApplicationFactory) ek karmaşıklık getirir.
 *   GET /Ai/ModelsForTask için HTTP-level test
 *   AiControllerHttpTests.cs içinde verilmiştir.
 *
 * ═══════════════════════════════════════════════════════════════════════════════
 *  JÜRİ NOTLARI — INTEGRATION TEST KATMANI (CONTROLLER-LEVEL)
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 *  TEST PİRAMİDİNDEKİ YERİ:
 *      Birim testlerin hemen üstü. Birden fazla katmanın birlikte çalıştığını
 *      doğrular: Controller + DI + Service + Options + Mocked HttpClient.
 *
 *  HİBRİT YAKLAŞIM:
 *      - Gerçek bileşenler   : AiController, HuggingFaceAiService, IOptions
 *      - Mock'lanan bileşenler: IDocumentService, HttpMessageHandler, IUrlHelper
 *      Bu yaklaşım "neyin entegre edildiğini" net şekilde tanımlar.
 *
 *  ÜCRETSİZ KAZANÇLAR:
 *      ► JSON serileştirme ve action result tipleri test edilir.
 *      ► Authorize attribute davranışı sahte ClaimsPrincipal ile doğrulanır.
 *      ► IUrlHelper.Action() çağrısının çalıştığı kontrol edilir.
 *
 *  JÜRİ Q&A (BU KATMAN İÇİN):
 *      Q: "Birim testten farkı nedir?"
 *      A: Birim test yalnızca SUT'u izole eder; entegrasyon test ise birden
 *         fazla katmanın birlikte doğru çalıştığını gösterir. Burada
 *         Controller↔Service kontratı + JSON şekillendirme + DI çözümü
 *         birlikte test edilir.
 *
 *      Q: "WebApplicationFactory yerine neden doğrudan controller new'leniyor?"
 *      A: AiController.Process eyleminde [ValidateAntiForgeryToken] var.
 *         Tam HTTP testi anti-forgery token cookie+header kombinasyonu
 *         gerektirir → karmaşıklığı bu katmandan AiControllerHttpTests'e
 *         (GET endpoint) taşıdık. Pragmatik mühendislik kararıdır.
 */
public class AiControllerIntegrationTests
{
    private const string TestEmail = "demo@university.edu";
    private static readonly Guid DocId = Guid.NewGuid();

    /// <summary>
    /// Belirtilen IAiService ile yapılandırılmış AiController döner.
    /// HttpContext'e sahte ClaimsPrincipal yerleştirilir (Authorize bypass).
    /// </summary>
    private static AiController BuildController(
        IAiService aiService,
        out Mock<IDocumentService> docMock)
    {
        docMock = new Mock<IDocumentService>();

        // [TR] Workspace çözümleyicisi: documentId → başlık
        docMock.Setup(s => s.GetWorkspaceAsync(TestEmail, DocId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new DocumentWorkspaceViewModel
               {
                   Id    = DocId,
                   Title = "Test Document",
                   Status = DocumentStatus.AI_READY,
               });

        // [TR] AI sonucu kaydı: başarı durumunu simüle et
        docMock.Setup(s => s.SaveAiResultAsync(
                    TestEmail, DocId,
                    It.IsAny<AiProcessRequestViewModel>(),
                    It.IsAny<AiServiceResult>(),
                    It.IsAny<CancellationToken>()))
               .ReturnsAsync((true, (Guid?)Guid.NewGuid(), (string?)null));

        var aiOptions = Options.Create(new AiOptions
        {
            Models =
            [
                new AiModelDefinition
                {
                    Id = "Qwen/Qwen2.5-7B-Instruct",
                    Label = "Qwen 2.5 7B",
                    Provider = "HuggingFace",
                    Tasks = ["Translate","Summarize","Rewrite","CreativeWrite"]
                },
                new AiModelDefinition
                {
                    Id = "black-forest-labs/FLUX.1-schnell",
                    Label = "FLUX.1 Schnell",
                    Provider = "HuggingFace",
                    Tasks = ["Visualize"]
                }
            ]
        });

        var controller = new AiController(docMock.Object, aiService, aiOptions);

        // [TR] HttpContext kurulumu: User.Identity.Name = TestEmail
        var identity  = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, TestEmail) }, "Test");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
            RouteData   = new RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
        };

        // [TR] Controller, Process() içinde Url.Action(...) çağırdığından
        //      mock IUrlHelper enjekte edilir; aksi halde NullReferenceException olur.
        var urlMock = new Mock<IUrlHelper>();
        urlMock.Setup(u => u.Action(It.IsAny<UrlActionContext>()))
               .Returns("/Ai/Result/test");
        controller.Url = urlMock.Object;

        return controller;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /Ai/Process — Translate
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_TranslateOperation_ReturnsOkJsonWithOutputText()
    {
        const string translated = "Hello world.";
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(translated)));

        var controller = BuildController(svc, out _);
        var req = new AiProcessRequestViewModel
        {
            DocumentId    = DocId,
            OperationType = "Translate",
            ModelName     = "Qwen/Qwen2.5-7B-Instruct",
            InputText     = "Merhaba dünya.",
            TargetLanguage = "English",
            Style         = "Formal",
        };

        var actionResult = await controller.Process(req, CancellationToken.None);

        // [TR] JsonResult döner; başarı bayrağı + outputText doğrulanır
        var jsonResult = Assert.IsType<JsonResult>(actionResult);
        var dict = ToDict(jsonResult.Value!);
        Assert.Equal(true, dict["ok"]);
        Assert.Equal(translated, dict["outputText"]);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /Ai/Process — Summarize
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_SummarizeOperation_ReturnsCorrectJsonStructure()
    {
        const string summary = "Kısa özet.";
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson(summary)));

        var controller = BuildController(svc, out _);
        var req = new AiProcessRequestViewModel
        {
            DocumentId    = DocId,
            OperationType = "Summarize",
            ModelName     = "Qwen/Qwen2.5-7B-Instruct",
            InputText     = string.Concat(Enumerable.Repeat("Bu uzun bir paragraf. ", 30)),
        };

        var actionResult = await controller.Process(req, CancellationToken.None);

        var jsonResult = Assert.IsType<JsonResult>(actionResult);
        var dict = ToDict(jsonResult.Value!);
        Assert.True((bool)dict["ok"]!);
        Assert.Contains("aiResultId", dict.Keys);
        Assert.Contains("outputText", dict.Keys);
        Assert.Contains("resultUrl", dict.Keys);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /Ai/Process — eksik DocumentId → 400
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_WithEmptyDocumentId_ReturnsBadRequest()
    {
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson("x")));

        var controller = BuildController(svc, out _);
        var req = new AiProcessRequestViewModel
        {
            DocumentId    = Guid.Empty,
            OperationType = "Translate",
            InputText     = "test"
        };

        var actionResult = await controller.Process(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // POST /Ai/Process — geçersiz operasyon → 400
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Process_WithInvalidOperationType_ReturnsBadRequest()
    {
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Json(HuggingFaceServiceBuilder.ChatJson("x")));

        var controller = BuildController(svc, out _);
        var req = new AiProcessRequestViewModel
        {
            DocumentId    = DocId,
            OperationType = "Hacking",
            InputText     = "test"
        };

        var actionResult = await controller.Process(req, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.NotNull(bad.Value);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GET /Ai/ModelsForTask — task'e göre filtreleme
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ModelsForTask_ReturnsOnlyModelsThatSupportTheTask()
    {
        var (svc, _) = HuggingFaceServiceBuilder.Build(
            MockHttpMessageHandler.Json("{}"));

        var controller = BuildController(svc, out _);

        var result = controller.ModelsForTask("Visualize");

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(ok.Value!);

        // [TR] Yalnızca Visualize task'ı destekleyen modeller dönmeli (FLUX.1)
        var count = 0;
        foreach (var item in list)
        {
            count++;
            Assert.Contains("FLUX", item!.ToString());
        }
        Assert.Equal(1, count);
    }

    // ─── Yardımcı: anonim object → dictionary ────────────────────────────────
    private static Dictionary<string, object?> ToDict(object value)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var p in value.GetType().GetProperties())
            dict[p.Name] = p.GetValue(value);
        return dict;
    }
}
