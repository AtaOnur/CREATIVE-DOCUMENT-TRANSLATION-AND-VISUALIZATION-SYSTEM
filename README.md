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

