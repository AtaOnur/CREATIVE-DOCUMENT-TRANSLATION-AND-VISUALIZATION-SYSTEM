using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace pdf_bitirme.Tests.Helpers;

/// <summary>
/// [TR] HuggingFaceAiService.VisualizeAsync, üretilen görseli
///      <c>WebRootPath/ai-images/</c> altına yazar. Birim testlerde
///      gerçek wwwroot yerine geçici klasör kullanılır.
///
/// [TR] Neden gerekli:
///      ASP.NET Core <c>IWebHostEnvironment</c> arayüzünün test'te
///      sade bir uygulamasını sağlar.
/// </summary>
public sealed class TestHostEnvironment : IWebHostEnvironment
{
    public TestHostEnvironment()
    {
        var root = Path.Combine(Path.GetTempPath(), "pdf_bitirme_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ContentRootPath = root;
        WebRootPath     = Path.Combine(root, "wwwroot");
        Directory.CreateDirectory(WebRootPath);

        ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
        WebRootFileProvider     = new PhysicalFileProvider(WebRootPath);
    }

    public string EnvironmentName    { get; set; } = "Test";
    public string ApplicationName    { get; set; } = "pdf_bitirme.Tests";
    public string WebRootPath        { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string ContentRootPath    { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}
