/*
 * [TR] Bu dosya ne işe yarar: Uygulama giriş noktası — EF Core SQLite, kimlik çerezleri, belge servisi, boru hattı.
 * [TR] Neden gerekli: Kalıcı belge listesi ve pano; EnsureCreated ile MVP veritabanı dosyası oluşturulur.
 * [TR] İlgili: AppDbContext, DocumentService, AccountController
 *
 * MODIFICATION NOTES (TR)
 * - Migrate() ve migration betikleri üretim için tercih edilir.
 * - OpenId Connect / JWT API koruması.
 * - Demo seed verisi eklendi (sunum için hızlı başlangıç).
 * - Ocr:UseMock=false ile TesseractCliOcrService aktif edilebilir.
 * - Zorluk: Kolay–orta.
 */

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using pdf_bitirme.Data;
using pdf_bitirme.Models;
using pdf_bitirme.Models.Entities;
using pdf_bitirme.Services;
using pdf_bitirme.Services.Ai;
using pdf_bitirme.Services.Email;
using pdf_bitirme.Services.Ocr;

var builder = WebApplication.CreateBuilder(args);

// [TR] Kullanıcılar artık SQLite'ta kalıcı; uygulama her restart'ta sıfırlanmaz.
builder.Services.AddScoped<ISimpleAccountStore, DbSimpleAccountStore>();
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// ─── OCR SERVİSİ KAYDI ───────────────────────────────────────────────────────
// [TR] Ocr:Provider değerine göre hangi OCR sağlayıcısı kullanılacağı belirlenir.
//      "Paddle"    → PaddleOcrService  (Python tabanlı, yüksek doğruluk)
//      "Tesseract" → TesseractCliOcrService (CLI tabanlı, kurulumu kolay)
//      diğer/boş   → MockOcrService (geliştirme/sunum için)
var ocrProvider = builder.Configuration.GetValue<string>("Ocr:Provider") ?? "Mock";
if (ocrProvider.Equals("Paddle", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsWindows())
{
    builder.Services.AddScoped<IOcrService, PaddleOcrService>();
}
else if (ocrProvider.Equals("Tesseract", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsWindows())
{
    builder.Services.AddScoped<IOcrService, TesseractCliOcrService>();
}
else
{
    builder.Services.AddScoped<IOcrService, MockOcrService>();
}

// ─── AI SERVİSİ KAYDI ────────────────────────────────────────────────────────
// [TR] Ai:Provider değerine göre hangi yapay zeka sağlayıcısı kullanılacağı belirlenir.
//      "Multi"   → MultiProviderAiService (Gemini + HuggingFace + Groq, model adına yönlendirir)
//      "Gemini"  → GeminiAiService (yalnızca Google Gemini)
//      diğer     → MockAiService (geliştirme/sunum için)
builder.Services.Configure<GeminiAiOptions>(builder.Configuration.GetSection("Ai:Gemini"));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

var aiProvider = builder.Configuration.GetValue<string>("Ai:Provider") ?? "Mock";
if (aiProvider.Equals("Multi", StringComparison.OrdinalIgnoreCase))
{
    // [TR] Multi modunda Gemini, HuggingFace ve Groq servisleri kayıt edilir.
    //      MultiProviderAiService, model tanımındaki Provider alanına göre doğru servise yönlendirir.
    builder.Services.AddHttpClient<GeminiAiService>();
    builder.Services.AddHttpClient<HuggingFaceAiService>();
    builder.Services.AddHttpClient<GroqAiService>();
    builder.Services.AddHttpClient<StabilityAiService>();     // Stability AI eklendi
    builder.Services.AddScoped<IAiService, MultiProviderAiService>();
}
else if (aiProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<GeminiAiService>();
    builder.Services.AddScoped<IAiService, GeminiAiService>();
}
else
{
    builder.Services.AddScoped<IAiService, MockAiService>();
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data"));
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data", "uploads"));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS ocr_results (
            Id TEXT NOT NULL PRIMARY KEY,
            DocumentId TEXT NOT NULL,
            UserEmail TEXT NOT NULL,
            PageNumber INTEGER NOT NULL,
            X REAL NOT NULL,
            Y REAL NOT NULL,
            Width REAL NOT NULL,
            Height REAL NOT NULL,
            ExtractedText TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS ai_results (
            Id TEXT NOT NULL PRIMARY KEY,
            DocumentId TEXT NOT NULL,
            UserEmail TEXT NOT NULL,
            OperationType TEXT NOT NULL,
            ModelName TEXT NOT NULL,
            SourceLanguage TEXT NOT NULL,
            TargetLanguage TEXT NOT NULL,
            Style TEXT NOT NULL,
            CustomInstruction TEXT NOT NULL,
            InputText TEXT NOT NULL,
            OutputText TEXT NOT NULL,
            OutputImageUrl TEXT NOT NULL,
            IsSaved INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """);
    EnsureColumn(db, "ai_results", "SourcePageNumber", "ALTER TABLE ai_results ADD COLUMN SourcePageNumber INTEGER NOT NULL DEFAULT 1;");
    EnsureColumn(db, "ai_results", "NoteTitle", "ALTER TABLE ai_results ADD COLUMN NoteTitle TEXT NOT NULL DEFAULT '';");
    EnsureColumn(db, "ai_results", "UserNote", "ALTER TABLE ai_results ADD COLUMN UserNote TEXT NOT NULL DEFAULT '';");
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS user_settings (
            Id TEXT NOT NULL PRIMARY KEY,
            UserEmail TEXT NOT NULL,
            DefaultAiModel TEXT NOT NULL,
            DefaultTranslationStyle TEXT NOT NULL,
            ThemePreference TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """);
    SeedDemoData(app.Environment.ContentRootPath, db);
    SeedUsers(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>
/// [TR] SQLite tablosunda kolon yoksa ekler; varsa tekrar deneme yapmaz.
/// </summary>
static void EnsureColumn(AppDbContext db, string tableName, string columnName, string sqlIfMissing)
{
    if (ColumnExists(db, tableName, columnName))
        return;

    db.Database.ExecuteSqlRaw(sqlIfMissing);
}

/// <summary>
/// [TR] PRAGMA table_info ile kolon varligini kontrol eder.
/// </summary>
static bool ColumnExists(AppDbContext db, string tableName, string columnName)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
        connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info({tableName})";
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        var currentName = reader["name"]?.ToString();
        if (string.Equals(currentName, columnName, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

/// <summary>
/// [TR] Demo ve admin kullanıcıları DB'ye seed eder. Zaten varsa tekrar eklemez.
/// </summary>
static void SeedUsers(AppDbContext db)
{
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS app_users (
            Id TEXT NOT NULL PRIMARY KEY,
            Email TEXT NOT NULL,
            Password TEXT NOT NULL,
            Role TEXT NOT NULL,
            IsActive INTEGER NOT NULL,
            IsEmailVerified INTEGER NOT NULL,
            CreatedAtUtc TEXT NOT NULL
        );
        """);

    var now = DateTime.UtcNow;

    if (!db.AppUsers.Any(u => u.Email == "demo@university.edu"))
    {
        db.AppUsers.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "demo@university.edu",
            Password = "demo123",
            Role = "User",
            IsActive = true,
            IsEmailVerified = true,
            CreatedAtUtc = now,
        });
    }

    if (!db.AppUsers.Any(u => u.Email == "admin@university.edu"))
    {
        db.AppUsers.Add(new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@university.edu",
            Password = "admin123",
            Role = "Admin",
            IsActive = true,
            IsEmailVerified = true,
            CreatedAtUtc = now,
        });
    }

    db.SaveChanges();
}

static void SeedDemoData(string contentRootPath, AppDbContext db)
{
    const string demoEmail = "demo@university.edu";
    if (db.Documents.Any(d => d.OwnerEmail == demoEmail))
        return;

    var now = DateTime.UtcNow;
    var folder = "demo";
    var uploadsDir = Path.Combine(contentRootPath, "Data", "uploads", folder);
    Directory.CreateDirectory(uploadsDir);

    var doc1Id = Guid.NewGuid();
    var doc1Physical = Path.Combine(uploadsDir, $"{doc1Id}.pdf");
    if (!File.Exists(doc1Physical))
    {
        // [TR] Demo amaçlı çok küçük geçerli PDF içeriği.
        var demoPdf = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 0>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF";
        File.WriteAllText(doc1Physical, demoPdf);
    }

    db.Documents.Add(new Document
    {
        Id = doc1Id,
        OwnerEmail = demoEmail,
        Title = "Demo Belge - Akademik Ozet",
        FileName = "demo-akademik.pdf",
        ContentType = "application/pdf",
        SizeBytes = new FileInfo(doc1Physical).Length,
        Status = DocumentStatus.AI_READY,
        StorageRelativePath = Path.Combine("Data", "uploads", folder, $"{doc1Id}.pdf").Replace('\\', '/'),
        CreatedAtUtc = now.AddDays(-2),
        UpdatedAtUtc = now.AddDays(-1),
    });

    var aiId = Guid.NewGuid();
    db.AiResults.Add(new AiResult
    {
        Id = aiId,
        DocumentId = doc1Id,
        UserEmail = demoEmail,
        OperationType = "Summarize",
        ModelName = "mock-academic",
        SourceLanguage = "Turkish",
        TargetLanguage = "Turkish",
        SourcePageNumber = 2,
        Style = "Academic",
        CustomInstruction = "rewrite academically",
        NoteTitle = "Juri Sunumu Icin Ozet",
        UserNote = "Savunmada bu bolumu gosterecegim.",
        InputText = "Demo metin: OCR ile secilen alandan elde edilen metin ornegi.",
        OutputText = "Bu demo ozet, secili PDF bolgesindeki icerigi akademik ve kisa bicimde sunar.",
        OutputImageUrl = string.Empty,
        IsSaved = true,
        CreatedAtUtc = now.AddDays(-1),
        UpdatedAtUtc = now.AddHours(-3),
    });

    db.ActivityEntries.Add(new ActivityEntry
    {
        Id = Guid.NewGuid(),
        UserEmail = demoEmail,
        Message = "\"Demo Belge - Akademik Ozet\" seed verisi eklendi.",
        CreatedAtUtc = now.AddHours(-2),
    });

    db.UserSettings.Add(new UserSettings
    {
        Id = Guid.NewGuid(),
        UserEmail = demoEmail,
        DefaultAiModel = "mock-academic",
        DefaultTranslationStyle = "Academic",
        ThemePreference = "System",
        UpdatedAtUtc = now,
    });

    db.SaveChanges();
}
