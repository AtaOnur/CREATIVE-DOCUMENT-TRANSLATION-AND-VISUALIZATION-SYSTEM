using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using pdf_bitirme.Data;
using pdf_bitirme.Models;
using pdf_bitirme.Models.Entities;
using pdf_bitirme.Services;

namespace pdf_bitirme.Tests.Helpers;

/// <summary>
/// [TR] DocumentService birim testleri için izole SQLite veritabanı + servis örneği.
///      Her test kendi geçici DB dosyasını kullanır (test izolasyonu).
///
/// MODIFICATION NOTES (TR)
///   - SeedDocument / SeedAiResult yardımcıları yeni senaryolar için genişletilebilir.
/// </summary>
public sealed class DocumentServiceTestContext : IAsyncDisposable
{
    private readonly string _dbPath;

    public AppDbContext Db { get; }
    public DocumentService Service { get; }

    private DocumentServiceTestContext(AppDbContext db, DocumentService service, string dbPath)
    {
        Db = db;
        Service = service;
        _dbPath = dbPath;
    }

    public static async Task<DocumentServiceTestContext> CreateAsync()
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            "pdf_bitirme_unit_" + Guid.NewGuid().ToString("N") + ".db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath.Replace('\\', '/')}")
            .Options;

        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Models:0:Id"] = "mock-gpt",
            })
            .Build();

        var env = new TestHostEnvironment();
        var service = new DocumentService(db, env, configuration, NullLogger<DocumentService>.Instance);

        return new DocumentServiceTestContext(db, service, dbPath);
    }

    public async Task<Document> SeedDocumentAsync(
        string ownerEmail,
        string title = "Test Document",
        bool isBanned = false,
        string banReason = "",
        DateTime? createdAtUtc = null)
    {
        var now = createdAtUtc ?? DateTime.UtcNow;
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            OwnerEmail = ownerEmail.Trim(),
            Title = title,
            FileName = "sample.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            Status = DocumentStatus.UPLOADED,
            IsBanned = isBanned,
            BanReason = banReason,
            BannedAtUtc = isBanned ? now : null,
            StorageRelativePath = "uploads/sample.pdf",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        Db.Documents.Add(doc);
        await Db.SaveChangesAsync();
        return doc;
    }

    public async Task<AiResult> SeedAiResultAsync(
        Document document,
        string userEmail,
        string outputText = "AI output")
    {
        var entity = new AiResult
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            UserEmail = userEmail.Trim(),
            OperationType = "Translate",
            ModelName = "mock-gpt",
            SourceLanguage = "en",
            TargetLanguage = "tr",
            Style = "Formal",
            InputText = "Hello",
            OutputText = outputText,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        Db.AiResults.Add(entity);
        await Db.SaveChangesAsync();
        return entity;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch { /* best-effort */ }
    }
}
