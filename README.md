# Creative Document Translation and Visualisation System

Bitirme projesi olarak gelistirilmis ASP.NET Core MVC tabanli bir belge isleme calismasidir.

## Graduation Project Note

Bu depo, final-year / bitirme projesi sunumu icin sade ve anlatilabilir bir MVP olarak tasarlanmistir. Kod yapisi bilerek basit tutulmustur.

## Project Purpose

- PDF belge yukleme
- PDF icinden kullanici tarafindan secilen bolgeden OCR metin uretimi (mock veya Tesseract CLI)
- OCR metni uzerinde AI islemleri (mock): translate, summarize, rewrite, creative write, visualize
- Sonuclari kaydetme, notebook/history ve ayarlar ekranlari

## Current Scope

- MVC web uygulamasi (server-side render + basit JS)
- OCR sadece PDF icindeki secili bolgede
- AI islemleri mock servis ile
- SQLite persistence
- Demo hesap + demo veri

## Architecture Summary

- **Controllers**: `Home`, `Account`, `Dashboard`, `Documents`, `Ai`, `Notebook`, `Settings`
- **Services**:
  - `IDocumentService` / `DocumentService`: belge, OCR sonucu, AI sonucu, notebook ve settings is mantigi
  - `IOcrService` / `MockOcrService` / `TesseractCliOcrService`: secili PDF bolgesi OCR
  - `IAiService` / `MockAiService`: AI islem mock
  - `ISimpleAccountStore` / `SimpleAccountStore`: demo auth store
- **Data**: `AppDbContext` + SQLite (`Data/app.db`)
- **Entities**: `Document`, `ActivityEntry`, `OcrResult`, `AiResult`, `UserSettings`
- **Views**: Bootstrap tabanli, ortak layout + ortak alert partial + workspace JS

## Technologies Used

- ASP.NET Core MVC (.NET 8)
- Entity Framework Core + SQLite
- Bootstrap 5
- PDF.js (client-side PDF render)
- Vanilla JavaScript (workspace ve loading davranislari)
- Tesseract CLI + Poppler pdftoppm (opsiyonel gercek OCR)

## Setup Steps

1. `.NET 8 SDK` kurulu olmalidir.
2. Proje kokunde:
   - `dotnet restore`
   - `dotnet run`
3. Tarayicida acin: `https://localhost:xxxx`

### Optional: Real OCR with Tesseract

Mock yerine gercek OCR kullanmak icin:

1. Tesseract OCR kurun (Windows):
   - [Tesseract at UB Mannheim](https://github.com/UB-Mannheim/tesseract/wiki)
2. Poppler kurun (`pdftoppm` icin):
   - [Poppler for Windows](https://github.com/oschwartz10612/poppler-windows/releases)
3. `PATH` icine `tesseract.exe` ve `pdftoppm.exe` klasorlerini ekleyin
   - veya `appsettings.json` icinde tam yol verin:
     - `Ocr:TesseractPath`
     - `Ocr:PdfToPpmPath`
4. `appsettings.json`:
   - `Ocr:UseMock = false`
   - `Ocr:Language = tur+eng` (ihtiyaca gore degistirin)
5. Uygulamayi yeniden baslatin.

## Database Info

- DB dosyasi: `Data/app.db`
- Upload klasoru: `Data/uploads/`
- Baslangicta `EnsureCreated()` + temel tablolar olusturulur.

## Demo Data Info

Seed verisi otomatik eklenir:

- Demo hesap: `demo@university.edu` / `demo123`
- 1 adet demo PDF kaydi
- 1 adet kaydedilmis AI notebook girdisi
- 1 adet activity kaydi
- Demo user settings

## UI and UX Final Polish

- Ortak navbar/footer ve kart stili
- Ortak alert gosterimi: `Views/Shared/_AppAlerts.cshtml`
- POST formlarda loading-state: `wwwroot/js/site.js`
- Bos durum kartlari (dashboard/documents/notebook)
- Tutarli validation gorunumu ve sade feedback dili

## Current Limitations

- AI katmani mock implementasyon
- Tesseract OCR icin makinede `tesseract` ve `pdftoppm` kurulu olmali
- Migration yerine MVP-style startup SQL kullanimi
- Basit auth store (Identity degil)

## Out of Scope Features (Current Version)

- Team collaboration
- Enterprise admin panel
- Gelismis provider orkestrasyonu ve queue yapilari

## Future Work (Important)

- Bu surumde OCR **yalnizca PDF icindeki secilen bolgeye** uygulanmaktadir.
- Arbitrary image-to-text / genel image OCR **uygulanmamistir**.
- Genel image OCR gelecekteki calisma olarak planlanmistir.
- Gelismis PDF duzenleme (annotation editor, rotate/full tools) future work.
- Collaboration future work.
- Gelismis export / overlay future work.

## Possible Jury Modifications and How to Handle Them

Asagidaki bolum juri sorularina hizli cevap vermek icin hazirlanmistir.

### 1) DOCX upload ekleyin
- `DocumentUploadConstants` ve `DocumentService.UploadAsync` icinde uzanti/content-type genisletilir.
- Gerekirse DOCX -> PDF donusum adimi eklenir.

### 2) Full-page OCR ekleyin
- Workspace tarafinda "tum sayfa sec" butonu eklenir.
- `RegionSelectionViewModel` sayfa tam boyut koordinatlari ile doldurulur.

### 3) Multiple region selection ekleyin
- `RegionSelectionViewModel` listeye cevrilir.
- `ExtractText` endpoint'i dizi kabul edecek sekilde genisletilir.

### 4) Mock OCR'yi gercek OCR ile degistirin
- `IOcrService` imzasi korunur.
- `MockOcrService` yerine Tesseract/API implementasyonu yazilip DI'da degistirilir.

### 5) Daha fazla AI modeli destekleyin
- `IAiService` korunur.
- `MockAiService` yerine provider bazli implementasyon veya model-router eklenir.

### 6) PDF overlay export ekleyin
- Secili bolge + AI ciktilari icin export DTO'su eklenir.
- PDF uzerine katmanli render ile disa aktarma endpoint'i yazilir.

### 7) Cloud storage ekleyin
- `DocumentService` icindeki local file islemleri abstraction'a alinir.
- S3/Azure Blob adapter'i eklenir.

### 8) Admin panel ekleyin
- Role-based auth (Identity + roles) gecisi yapilir.
- Ayrı `AdminController` ile raporlama ve yonetim ekranlari eklenir.

### 9) Collaboration ekleyin
- Notebook kayitlarina sahiplik + paylasim tablosu eklenir.
- Basit yorum/izin modeli ile genisletilir.

### 10) Genel image OCR (future work)
- Ayrı upload akisi ve image preprocessing pipeline gerekir.
- Mevcut scope'u bozmamak icin `Documents` modulu disinda yeni bir modul olarak tasarlanmalidir.

---

Bu proje sunum odakli bir MVP'dir: sade kod, net katmanlar, kolay degistirilebilir tasarim.
