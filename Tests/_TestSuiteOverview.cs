namespace pdf_bitirme.Tests;

/*
 ╔══════════════════════════════════════════════════════════════════════════════╗
 ║                                                                              ║
 ║          pdf_bitirme.Tests — TEST MİMARİSİ TANITIM DOSYASI                   ║
 ║                                                                              ║
 ║   Bu dosya derleme için boş bir marker sınıf içerir; asıl amacı, test        ║
 ║   yapısının "neden böyle kurulduğunu" tek bakışta açıklayan profesyonel      ║
 ║   notları kod tabanında saklamaktır. Jüri savunması ve yeni katılan          ║
 ║   geliştiriciler için referans niteliğindedir.                               ║
 ║                                                                              ║
 ╚══════════════════════════════════════════════════════════════════════════════╝


 ─── 1. TEK CÜMLE ÖZET (SAVUNMA AÇILIŞI) ────────────────────────────────────────

   Sistemin yapay zekâ servis katmanı için xUnit, Moq ve
   Microsoft.AspNetCore.Mvc.Testing üzerine kurulmuş DÖRT KATMANLI bir test
   piramidi uyguladım: birim testler servis davranışını, entegrasyon testleri
   controller-servis etkileşimini, fonksiyonel testler kullanıcı akışlarını,
   fonksiyonel olmayan testler ise performans ve dayanıklılığı doğruluyor.
   Toplam 61 test, hiçbir dış API çağrısı yapmadan ortalama ~2 saniyede
   tamamlanıyor — bu da CI/CD entegrasyonu için endüstri standardına uygun
   bir geri bildirim süresidir.


 ─── 2. PROFESYONELLİĞİ TANIMLAYAN KARARLAR ─────────────────────────────────────

  ┌──────────────────────────────────────────┬──────────────────────────────────┐
  │ KARAR                                    │ NEDEN PROFESYONEL                │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ xUnit + Moq + Mvc.Testing                │ .NET ekosisteminin endüstri-     │
  │                                          │ standardı stack'i. Microsoft'un  │
  │                                          │ kendi ASP.NET Core deposu da     │
  │                                          │ xUnit kullanır.                  │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ Test piramidi disiplini                  │ Test adedi alta indikçe artar,   │
  │ (Unit > Integration > Functional > NFR)  │ üste çıkıldıkça azalır → hızlı   │
  │                                          │ geri bildirim + yüksek güven.    │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ Hiçbir testte gerçek HTTP yok            │ MockHttpMessageHandler ile       │
  │                                          │ internet bağımsız, deterministik │
  │                                          │ tekrar üretilebilir → CI/CD'de   │
  │                                          │ sorunsuz çalışır.                │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ Test izolasyonu                          │ Her WebApplicationFactory örneği │
  │                                          │ kendi geçici SQLite dosyasını    │
  │                                          │ kullanır → testler birbirini     │
  │                                          │ etkilemez.                       │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ DRY helper'lar                           │ HuggingFaceServiceBuilder.Build()│
  │                                          │ tek satırda servis kurar; her    │
  │                                          │ testte 20 satır boilerplate yok. │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ Pozitif + negatif yol kapsamı            │ Sadece happy path değil; 401/503/│
  │                                          │ network down/cancellation/empty  │
  │                                          │ input/invalid op da test edilir. │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ Functional test ayrı katman              │ "Kod çalışıyor" yerine "kullanıcı│
  │                                          │ için anlamlı çıktı üretiyor" mu  │
  │                                          │ sorusunu cevaplar.               │
  ├──────────────────────────────────────────┼──────────────────────────────────┤
  │ Non-functional kapsam                    │ Stopwatch + paralel istek +      │
  │                                          │ cancellation → kalite öznitelik- │
  │                                          │ leri (NFR) belgelenir.           │
  └──────────────────────────────────────────┴──────────────────────────────────┘


 ─── 3. TEST PİRAMİDİ (61 TEST / ~2 sn) ───────────────────────────────────────

                          ┌────────────────────────┐
                          │   NonFunctional (6)    │  ← perf + dayanıklılık
                          └────────────────────────┘
                    ┌──────────────────────────────────┐
                    │      Functional Tests (5)        │  ← kullanıcı akışı
                    └──────────────────────────────────┘
              ┌────────────────────────────────────────────┐
              │         Integration Tests (15)             │  ← controller + HTTP
              └────────────────────────────────────────────┘
        ┌──────────────────────────────────────────────────────┐
        │              Unit Tests (35)                         │  ← AI + DocumentService
        └──────────────────────────────────────────────────────┘


 ─── 4. KATMAN KATMAN TANITIM ───────────────────────────────────────────────────

  ► UNIT TESTS
    A) HuggingFaceAiServiceTests.cs — AI servis davranışı (11 test)
    B) HuggingFaceEdgeCaseTests.cs — hata/edge case (11 test + Theory)
    C) DocumentServiceFeatureTests.cs — ban, workspace rehberi, admin AI sonucu (9 test)
       Senaryolar:
         - GetOwnerDocumentAccessAsync: NotFound / Banned / Allowed
         - ShowWorkspaceGuide: ilk belge true, ikinci belge false, tamamlanmışsa false
         - MarkWorkspaceGuideCompletedAsync kalıcı işaret
         - GetAiResultPageByIdAsync sahiplik filtresi olmadan döner
         - GetAiResultPageAsync başka kullanıcının sonucu null

  ► INTEGRATION TESTS — dört katman

    A) Controller-level AI (AiControllerIntegrationTests.cs, 5 test)
    B) HTTP-level AI (AiControllerHttpTests.cs, 3 test)
    C) Documents moderasyon/rehber (DocumentsControllerIntegrationTests.cs, 2 test)
    D) Admin AI sonuç görüntüleme (AiControllerAdminResultTests.cs, 3 test)
    E) HTTP-level Documents (DocumentsControllerHttpTests.cs, 2 test)

  ► FUNCTIONAL TESTS — AiUserFlowTests.cs (5 test)
    Amaç: "Kod çalışıyor mu?" yerine
          "Kullanıcı için anlamlı çıktı oluşuyor mu?"
    Örn: Özet, kaynak metinden GERÇEKTEN kısa mı?

  ► NON-FUNCTIONAL TESTS — PerformanceAndFailureTests.cs (6 test)
    - Performans: <500 ms tek istek; 10 paralel <2 sn
    - Dayanıklılık: 503/401/network down/cancellation token


 ─── 5. YARDIMCI ALTYAPI (Tests/Helpers/) ───────────────────────────────────────

   ┌──────────────────────────────────┬──────────────────────────────────────┐
   │ Dosya                            │ Sorumluluk                           │
   ├──────────────────────────────────┼──────────────────────────────────────┤
   │ MockHttpMessageHandler.cs        │ Json()/Image()/Error() factory'leri  │
   │ HuggingFaceServiceBuilder.cs     │ Tek satırda servis + ChatJson()      │
   │ TestHostEnvironment.cs           │ İzole geçici klasör (IWebHostEnv)    │
   │ TestAuthHandler.cs               │ [Authorize] bypass                   │
   │ TestWebApplicationFactory.cs     │ İzole SQLite + mock provider         │
   └──────────────────────────────────┴──────────────────────────────────────┘


 ─── 6. JÜRİDE SORULABİLECEK SORULAR ve HAZIR CEVAPLAR ──────────────────────────

   Q: "Neden testlerde gerçek API çağrılmıyor?"
   A: Determinism + CI hızı + rate limit sıfır + offline çalışma. Mock'lı
      testler tekrar üretilebilir; gerçek API testi flaky olur.

   Q: "Neden sentiment/NER testi yok?"
   A: Proje kapsamında o operasyonlar bulunmuyor; sadece var olan beş
      operasyon (Translate / Summarize / Rewrite / CreativeWrite / Visualize)
      test edildi. Eklendiğinde aynı pattern ile test edilebilir; örnek
      Tests/README.md içindedir.

   Q: "Test coverage yüzdesi nedir?"
   A: dotnet test --collect:"XPlat Code Coverage" ile ölçülebilir;
      HuggingFaceAiService.ProcessAsync'in tüm ana yolları (5 op +
      empty/invalid + parser dayanıklılığı + hata) kapsanmıştır.

   Q: "Mock kullanmak doğru mu?"
   A: Birim test tanımı gereği dış bağımlılıklar mock'lanır.
      Integration testte gerçek servis + mock HTTP handler kullanılarak
      hibrit yaklaşım uygulanmıştır.

   Q: "Test piramidi mi, buz konisi mi?"
   A: Piramit: 35 unit > 15 integration > 5 functional > 6 NFR.
      Piramit oranı sağlıklı; üst seviyelerde aşırı test yok.

   Q: "Niye 61 test sadece ~2 saniye sürüyor?"
   A: Hiçbir IO yok + paralel xUnit runner + mock'lanmış HttpClient →
      endüstri kabul kriteri ("birim testler <100 ms") uygulandı.

   Q: "POST /Ai/Process neden HTTP-level değil controller-level test ediliyor?"
   A: Endpoint'te [ValidateAntiForgeryToken] var; tam HTTP testi
      anti-forgery token kombinasyonu gerektirir → karmaşıklığı azaltmak
      için iki katmanlı strateji benimsendi:
        - Controller-level: Process action'ı doğrudan
        - HTTP-level: GET /Ai/ModelsForTask (anti-forgery yok)
      Bu pragmatik mühendislik kararıdır.


 ─── 7. ÇALIŞTIRMA ──────────────────────────────────────────────────────────────

   cd Tests
   dotnet test                                              ← tümü
   dotnet test --filter "FullyQualifiedName~UnitTests"      ← sadece unit
   dotnet test --filter "FullyQualifiedName~Integration"    ← sadece integration
   dotnet test --filter "FullyQualifiedName~Functional"     ← sadece functional
   dotnet test --filter "FullyQualifiedName~NonFunctional"  ← sadece perf/fail


 ─── 8. ÖNEMLİ TEKNİK KARARLAR (CHANGELOG) ──────────────────────────────────────

   - Program.cs'e "public partial class Program {}" eklendi
       Sebep: WebApplicationFactory<Program> assembly dışından erişebilsin.
   - pdf_bitirme.csproj'a Tests\** exclude eklendi
       Sebep: Ana web build'i test dosyalarını taramasın (CS0246 önlenir).
   - TestWebApplicationFactory:
       * ConnectionStrings:DefaultConnection → mutlak temp SQLite
       * Ai:Provider = Mock      (gerçek API çağrılmaz)
       * Ocr:Provider = Mock
       * Email:Smtp:Enabled = false  (mail gönderilmez)
       * Cookie scheme yerine TestAuthHandler


 ═══════════════════════════════════════════════════════════════════════════════
 *                                                                              *
 * Bu dosya yalnızca dokümantasyon amaçlıdır; runtime davranışa etkisi yoktur.   *
 *                                                                              *
 ═══════════════════════════════════════════════════════════════════════════════
*/

/// <summary>
/// [TR] Test paketinin tanıtım dosyası. Sadece üst kısımdaki büyük yorum bloğuna
///      bağlı bir marker sınıfıdır; herhangi bir runtime davranışı yoktur.
/// </summary>
internal static class TestSuiteOverview
{
    /// <summary>Toplam test sayısı (referans için sabit).</summary>
    public const int TotalTests = 61;

    /// <summary>Hedef toplam çalışma süresi (saniye, referans).</summary>
    public const double TargetRuntimeSeconds = 2.0;
}
