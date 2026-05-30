# pdf_bitirme.Tests — Test Yapısı

Bu proje, `pdf_bitirme` ASP.NET Core MVC uygulamasının **Unit / Integration /
Functional / Non-Functional** testlerini barındırır.

## Klasör Yapısı

```
Tests/
├── pdf_bitirme.Tests.csproj
├── Helpers/
│   ├── MockHttpMessageHandler.cs    # HttpClient için yeniden kullanılabilir HTTP mock
│   ├── TestHostEnvironment.cs       # IWebHostEnvironment için sahte (geçici klasör)
│   ├── TestAuthHandler.cs           # WebApplicationFactory için cookie auth bypass
│   ├── TestWebApplicationFactory.cs # Program.cs'i test ortamında ayağa kaldırır
│   ├── DocumentServiceTestContext.cs # DocumentService birim test DB'si
│   ├── TestDatabaseSeeder.cs        # HTTP testleri için seed verisi
│   └── HuggingFaceServiceBuilder.cs # Tek satırda HuggingFaceAiService kurar
├── UnitTests/
│   ├── HuggingFaceAiServiceTests.cs       # AI servis (9 test)
│   ├── HuggingFaceEdgeCaseTests.cs        # Edge case + çoklu dil (10 + Theory)
│   └── DocumentServiceFeatureTests.cs     # Ban, rehber, admin sonuç (9 test)
├── IntegrationTests/
│   ├── AiControllerIntegrationTests.cs    # AI controller + servis (5 test)
│   ├── AiControllerHttpTests.cs           # HTTP-level AI (3 test)
│   ├── AiControllerAdminResultTests.cs    # Admin Open Result (3 test)
│   ├── DocumentsControllerIntegrationTests.cs  # Ban + rehber kapatma (2 test)
│   └── DocumentsControllerHttpTests.cs    # HTTP-level Documents (2 test)
├── FunctionalTests/
│   └── AiUserFlowTests.cs              # Gerçek kullanıcı akışı (5 test)
└── NonFunctionalTests/
    └── PerformanceAndFailureTests.cs   # Stopwatch + hata simülasyonu (6 test)
```

## Çalıştırma

```bash
# Çözüm kökünden
dotnet test

# Yalnızca bu projeden
cd Tests
dotnet test

# Belirli bir kategori
dotnet test --filter "FullyQualifiedName~UnitTests"
dotnet test --filter "FullyQualifiedName~IntegrationTests"
dotnet test --filter "FullyQualifiedName~FunctionalTests"
dotnet test --filter "FullyQualifiedName~NonFunctionalTests"
```

## Tasarım Kararları

### 1) Gerçek HTTP isteği YOK

`HuggingFaceAiService` (ve mocklanması mümkün diğer dış servisler) içeride
`HttpClient` kullanır. Birim testlerde `MockHttpMessageHandler` ile bu istekler
yakalanır; HuggingFace'in gerçek router'ına bağlanılmaz. Bu sayede:

* Testler internet bağlantısından bağımsızdır
* Her çalıştırmada deterministic sonuçlar üretir
* Rate-limit ve API key gerektirmez

### 2) Sentiment / NER neden yok?

Orijinal test prompt'unda `AnalyzeSentimentAsync` ve `ExtractEntitiesAsync`
metotları belirtilmiş; ancak bu projede o metotlar **mevcut değildir**.
`HuggingFaceAiService`'in tek public API yüzeyi şu beş operasyonu yönlendirir:

| Operation     | Endpoint Türü | Test Kapsamı |
|---------------|---------------|--------------|
| Translate     | Chat (LLM)    | ✅ |
| Summarize     | Chat (LLM)    | ✅ |
| Rewrite       | Chat (LLM)    | ✅ |
| CreativeWrite | Chat (LLM)    | ✅ |
| Visualize     | Image (FLUX)  | ✅ |

Sentiment / NER eklenirse aynı pattern ile test edilebilir (aşağıdaki
"Yeni Test Ekleme" bölümüne bakınız).

### 3) AntiForgery & Auth

`POST /Ai/Process` üzerinde `[ValidateAntiForgeryToken]` olduğundan tam HTTP
seviyesinde test (WebApplicationFactory) anti-forgery token kombinasyonu
gerektirir. Bunun yerine:

* **Controller-level integration**:
  `AiControllerIntegrationTests` → AiController doğrudan inşa edilir,
  `Process()` action metodu çağrılır.
* **HTTP-level integration**:
  `AiControllerHttpTests` → yalnızca `GET /Ai/ModelsForTask` endpoint'ini
  kapsar (anti-forgery yok).

## Yeni Test Ekleme

### Yeni operasyon (örn. SentimentAnalysis) eklendiğinde

```csharp
[Fact]
public async Task Sentiment_WithPositiveText_ReturnsLabelAndScore()
{
    var json = HuggingFaceServiceBuilder.ChatJson("""{"label":"POSITIVE","score":0.94}""");
    var (svc, _) = HuggingFaceServiceBuilder.Build(MockHttpMessageHandler.Json(json));

    var req = new AiProcessRequestViewModel
    {
        OperationType = "Sentiment",
        InputText = "I love this!",
        ModelName = "...",
        DocumentId = Guid.NewGuid()
    };

    var result = await svc.ProcessAsync("Doc", req);

    Assert.Contains("POSITIVE", result.OutputText);
    Assert.Contains("0.94", result.OutputText);
}
```

### Yeni AI sağlayıcısı (örn. OpenAI) eklendiğinde

`HuggingFaceServiceBuilder` deseninden ilham alarak `OpenAiServiceBuilder`
yardımcısı yazıp aynı klasör altında `OpenAiServiceTests.cs` ekleyin.

## Bağımlılıklar

| Paket                                  | Sürüm   | Amaç                                   |
|----------------------------------------|---------|----------------------------------------|
| Microsoft.NET.Test.Sdk                 | 17.11.1 | Test runner altyapısı                  |
| xunit                                  | 2.9.2   | Test framework                         |
| xunit.runner.visualstudio              | 2.8.2   | VS Test Explorer / dotnet test         |
| Moq                                    | 4.20.72 | IDocumentService gibi tipler için      |
| Microsoft.AspNetCore.Mvc.Testing       | 8.0.11  | WebApplicationFactory<Program>         |

## CI Önerisi

```yaml
- name: Test
  run: dotnet test pdf_bitirme.sln --collect:"XPlat Code Coverage" --logger "trx"
```
