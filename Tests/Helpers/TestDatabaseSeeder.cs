using Microsoft.Extensions.DependencyInjection;
using pdf_bitirme.Data;
using pdf_bitirme.Models;
using pdf_bitirme.Models.Entities;

namespace pdf_bitirme.Tests.Helpers;

/// <summary>
/// [TR] WebApplicationFactory tabanlı HTTP testlerinde AppDbContext'e seed verisi yazar.
/// </summary>
public static class TestDatabaseSeeder
{
    public static async Task<Document> SeedDocumentAsync(
        TestWebApplicationFactory factory,
        string ownerEmail,
        Guid? id = null,
        string title = "HTTP Test Document",
        bool isBanned = false,
        string banReason = "",
        DateTime? createdAtUtc = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = createdAtUtc ?? DateTime.UtcNow;

        var doc = new Document
        {
            Id = id ?? Guid.NewGuid(),
            OwnerEmail = ownerEmail.Trim(),
            Title = title,
            FileName = "http-test.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            Status = DocumentStatus.UPLOADED,
            IsBanned = isBanned,
            BanReason = banReason,
            BannedAtUtc = isBanned ? now : null,
            StorageRelativePath = "uploads/http-test.pdf",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }
}
