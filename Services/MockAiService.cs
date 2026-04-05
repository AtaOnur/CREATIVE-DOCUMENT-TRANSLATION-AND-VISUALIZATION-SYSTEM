using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: AI işlemleri için mock sağlayıcı.
 * [TR] Neden gerekli: Gerçek model entegrasyonu olmadan uçtan uca demo akışı.
 * [TR] İlgili: IAiService, AiController
 *
 * MODIFICATION NOTES (TR)
 * - Gerçek model entegrasyonunda bu sınıf yerine sağlayıcı adaptörü yazılabilir.
 * - Translation quality scoring ve akademik atıf modu eklenebilir.
 * - Visualize provider daha gerçekçi görsel URL üretebilir.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Kolay.
 */
public class MockAiService : IAiService
{
    public async Task<AiServiceResult> ProcessAsync(
        string documentTitle,
        AiProcessRequestViewModel request,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(900, cancellationToken);

        var operation = (request.OperationType ?? "").Trim();
        var text = request.InputText?.Trim() ?? string.Empty;
        var style = request.Style?.Trim() ?? "Formal";
        var instruction = request.CustomInstruction?.Trim() ?? string.Empty;

        var result = new AiServiceResult();
        switch (operation)
        {
            case "Translate":
                result.OutputText =
                    $"[Mock Translate | {request.SourceLanguage} -> {request.TargetLanguage} | Style: {style}]\n\n" +
                    $"Document: {documentTitle}\n" +
                    $"Input: {text}\n\n" +
                    $"Output: This is a mock translation result prepared for graduation demo.";
                break;

            case "Summarize":
                result.OutputText =
                    "[Mock Summarize]\n\n" +
                    "Key points:\n" +
                    "- Main argument extracted from selected PDF region.\n" +
                    "- Supporting details grouped briefly.\n" +
                    "- Conclusion simplified for quick reading.\n\n" +
                    $"Source excerpt: {text}";
                break;

            case "Rewrite":
                result.OutputText =
                    $"[Mock Rewrite | Instruction: {instruction}]\n\n" +
                    "This is a rewritten variant based on your custom instruction.\n\n" +
                    $"Original: {text}";
                break;

            case "CreativeWrite":
                result.OutputText =
                    $"[Mock Creative Write | Style: {style}]\n\n" +
                    "Inspired by the extracted text, this section demonstrates a creative extension.\n" +
                    "It keeps core meaning while adding narrative tone.";
                break;

            case "Visualize":
                result.OutputText =
                    "[Mock Visualize] Text-to-image prompt prepared from selected PDF text.";
                // [TR] Demo için statik placeholder görsel URL'si.
                result.OutputImageUrl = "https://dummyimage.com/960x540/0f3d4c/ffffff&text=CreativeDoc+Mock+Visualization";
                break;

            default:
                result.OutputText = "[Mock AI] Unsupported operation type.";
                break;
        }

        return result;
    }
}

