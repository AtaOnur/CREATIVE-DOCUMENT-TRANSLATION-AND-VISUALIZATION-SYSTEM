using Microsoft.EntityFrameworkCore;
using pdf_bitirme.Models.Entities;

namespace pdf_bitirme.Data;

/*
 * [TR] Bu dosya ne işe yarar: EF Core bağlamı — Document ve ActivityEntry tabloları.
 * [TR] Neden gerekli: SQLite ile bitirme MVP’sinde yapılandırılabilir kalıcılık katmanı.
 * [TR] İlgili: Program.cs (UseSqlite), DocumentService
 *
 * MODIFICATION NOTES (TR)
 * - SQL Server / PostgreSQL: connection string ve provider değişimi.
 * - Fluent API ile indeks ve cascade kuralları genişletilir.
 * - Zorluk: Kolay (MVP) → Orta (üretim).
 */
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();
    public DbSet<OcrResult> OcrResults => Set<OcrResult>();
    public DbSet<AiResult> AiResults => Set<AiResult>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.OwnerEmail).HasMaxLength(320);
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.ContentType).HasMaxLength(120);
            e.Property(x => x.StorageRelativePath).HasMaxLength(600);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.OwnerEmail);
            e.HasIndex(x => x.UpdatedAtUtc);
        });

        modelBuilder.Entity<ActivityEntry>(e =>
        {
            e.ToTable("activity_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserEmail).HasMaxLength(320);
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<OcrResult>(e =>
        {
            e.ToTable("ocr_results");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserEmail).HasMaxLength(320);
            e.Property(x => x.ExtractedText).HasMaxLength(12000);
            e.HasIndex(x => x.DocumentId);
            e.HasIndex(x => x.UserEmail);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<AiResult>(e =>
        {
            e.ToTable("ai_results");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserEmail).HasMaxLength(320);
            e.Property(x => x.OperationType).HasMaxLength(48);
            e.Property(x => x.ModelName).HasMaxLength(80);
            e.Property(x => x.SourceLanguage).HasMaxLength(64);
            e.Property(x => x.TargetLanguage).HasMaxLength(64);
            e.Property(x => x.Style).HasMaxLength(64);
            e.Property(x => x.CustomInstruction).HasMaxLength(1200);
            e.Property(x => x.NoteTitle).HasMaxLength(160);
            e.Property(x => x.UserNote).HasMaxLength(1000);
            e.Property(x => x.InputText).HasMaxLength(12000);
            e.Property(x => x.OutputText).HasMaxLength(16000);
            e.Property(x => x.OutputImageUrl).HasMaxLength(1000);
            e.HasIndex(x => x.DocumentId);
            e.HasIndex(x => x.UserEmail);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<UserSettings>(e =>
        {
            e.ToTable("user_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserEmail).HasMaxLength(320);
            e.Property(x => x.DefaultAiModel).HasMaxLength(80);
            e.Property(x => x.DefaultTranslationStyle).HasMaxLength(64);
            e.Property(x => x.ThemePreference).HasMaxLength(32);
            e.HasIndex(x => x.UserEmail).IsUnique();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Password).HasMaxLength(256);
            e.Property(x => x.Role).HasMaxLength(32);
            e.HasIndex(x => x.Email).IsUnique();
        });
    }
}

/*
 * -----------------------------------------------------------------------------
 * [TR] OCR persistence notu:
 * - OcrResults ve AiResults tabloları eklendi; OCR metni ve AI çıktıları saklanır.
 *
 * MODIFICATION NOTES (TR)
 * - Gelecekte Document ile FK relation fluent olarak sıkılaştırılabilir.
 * - Confidence, engineVersion ve model kalite skoru alanları eklenebilir.
 * - Bu sürümde OCR yalnızca PDF içindeki seçilen bölgeye uygulanmaktadır.
 * - Genel resimden metin çıkarma future work olarak düşünülmüştür.
 * -----------------------------------------------------------------------------
 */
