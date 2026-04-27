using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using pdf_bitirme.Services.Ai;

namespace pdf_bitirme.Tests.Helpers;

/// <summary>
/// [TR] Tüm HuggingFaceAiService testlerinde kullanılan ortak fabrikadır.
///      Her testte HttpClient + IOptions + IWebHostEnvironment + ILogger
///      kurulumu yapmak yerine tek satırda servis döner.
///
/// KULLANIM:
///   var (svc, mock) = HuggingFaceServiceBuilder.WithJsonResponse(jsonBody);
///   await svc.ProcessAsync(...);
/// </summary>
public static class HuggingFaceServiceBuilder
{
    /// <summary>Verilen handler ile yapılandırılmış bir servis döner.</summary>
    public static (HuggingFaceAiService service, MockHttpMessageHandler handler) Build(
        MockHttpMessageHandler handler,
        AiOptions? options = null)
    {
        var http = new HttpClient(handler);

        var aiOptions = options ?? new AiOptions
        {
            HuggingFace = new HuggingFaceApiOptions
            {
                ApiKey         = "test-key",
                BaseUrl        = "https://router.huggingface.co/v1/chat/completions",
                TimeoutSeconds = 30
            }
        };

        var svc = new HuggingFaceAiService(
            http,
            Options.Create(aiOptions),
            new TestHostEnvironment(),
            NullLogger<HuggingFaceAiService>.Instance);

        return (svc, handler);
    }

    /// <summary>Standart başarılı OpenAI-uyumlu chat completions yanıtı.</summary>
    public static string ChatJson(string content)
    {
        // [TR] Gerçek HuggingFace Router yanıt formatı:
        //      { "choices": [ { "message": { "role":"assistant", "content":"..." } } ] }
        var escaped = System.Text.Json.JsonEncodedText.Encode(content);
        return $$"""
        {
          "id": "test-1",
          "object": "chat.completion",
          "choices": [
            {
              "index": 0,
              "message": { "role": "assistant", "content": "{{escaped}}" },
              "finish_reason": "stop"
            }
          ]
        }
        """;
    }
}
