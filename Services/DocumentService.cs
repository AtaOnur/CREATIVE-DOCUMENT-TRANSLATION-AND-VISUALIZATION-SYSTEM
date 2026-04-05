using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using pdf_bitirme.Data;
using pdf_bitirme.Models;
using pdf_bitirme.Models.Entities;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: SQLite + yerel disk üzerinde belge, OCR sonucu ve AI sonucu kalıcılığı.
 * [TR] Neden gerekli: MVP kalıcılığı; savunmada uçtan uca “yükle → OCR → AI işlem → sonuç” anlatımı.
 * [TR] İlgili: AppDbContext, DocumentsController, IWebHostEnvironment.ContentRootPath
 *
 * MODIFICATION NOTES (TR)
 * - DOCX yükleme ve sunucu tarafı PDF dönüşümü sonraki çalışma.
 * - Cloud storage + imzalı indirme URL’si.
 * - Dosya boyutu üst sınırı yapılandırmadan okunabilir.
 * - Belge önizleme görüntüsü (thumbnail) üretimi.
 * - Belge durumlari OCR_READY / AI_READY / COMPLETED akisinda guncellenir.
 * - OCR yalnızca PDF içi seçili bölge; genel görüntü OCR future work.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Yüksek (üretim güvenliği ve ölçek).
 */
public class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(AppDbContext db, IWebHostEnvironment env, IConfiguration configuration, ILogger<DocumentService> logger)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var q = _db.Documents.AsNoTracking().Where(d => d.OwnerEmail == userEmail);
        var allForStats = await q.ToListAsync(cancellationToken);
        var stats = BuildDashboardStats(allForStats);
        var recent = allForStats
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Take(8)
            .Select(ToRow)
            .ToList();

        var openStatuses = new[] { DocumentStatus.UPLOADED, DocumentStatus.PROCESSING, DocumentStatus.OCR_READY, DocumentStatus.AI_READY };
        var continueList = allForStats
            .Where(d => openStatuses.Contains(d.Status))
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Take(5)
            .Select(ToRow)
            .ToList();

        var activity = await _db.ActivityEntries.AsNoTracking()
            .Where(a => a.UserEmail == userEmail)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(10)
            .Select(a => new ActivityRowViewModel { Message = a.Message, AtUtc = a.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return new DashboardViewModel
        {
            UserEmail = userEmail,
            Stats = stats,
            RecentDocuments = recent,
            RecentActivity = activity,
            ContinueWorking = continueList,
            HasAnyDocuments = allForStats.Count > 0,
        };
    }

    public async Task<DocumentsIndexViewModel> ListAsync(
        string userEmail,
        string? searchQuery,
        string? statusFilter,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var q = _db.Documents.AsNoTracking().Where(d => d.OwnerEmail == userEmail);

        var t = searchQuery?.Trim();
        if (!string.IsNullOrEmpty(t))
        {
            var lower = t.ToLowerInvariant();
            q = q.Where(d =>
                d.Title.ToLower().Contains(lower) ||
                d.FileName.ToLower().Contains(lower) ||
                d.Id.ToString().ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter)
            && Enum.TryParse<DocumentStatus>(statusFilter.Trim(), ignoreCase: true, out var st))
        {
            q = q.Where(d => d.Status == st);
        }

        var list = (await q
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ToListAsync(cancellationToken))
            .Select(ToRow)
            .ToList();

        return new DocumentsIndexViewModel
        {
            SearchQuery = searchQuery,
            StatusFilter = statusFilter,
            Documents = list,
            HasAny = list.Count > 0,
        };
    }

    public async Task<DocumentDetailsViewModel?> GetDetailsAsync(string userEmail, Guid id, CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var d = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerEmail == userEmail, cancellationToken);
        if (d == null) return null;
        return new DocumentDetailsViewModel
        {
            Id = d.Id,
            Title = d.Title,
            FileName = d.FileName,
            Status = d.Status,
            SizeBytes = d.SizeBytes,
            CreatedAtUtc = d.CreatedAtUtc,
            UpdatedAtUtc = d.UpdatedAtUtc,
        };
    }

    public async Task<DocumentWorkspaceViewModel?> GetWorkspaceAsync(string userEmail, Guid id, CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var d = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerEmail == userEmail, cancellationToken);
        if (d == null) return null;

        var latestOcr = await _db.OcrResults.AsNoTracking()
            .Where(x => x.DocumentId == d.Id && x.UserEmail == userEmail)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var settings = await _db.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserEmail == userEmail, cancellationToken);
        var availableModels = GetAvailableAiModels();
        var mostUsedModel = await _db.AiResults.AsNoTracking()
            .Where(x => x.UserEmail == userEmail && availableModels.Contains(x.ModelName))
            .GroupBy(x => x.ModelName)
            .Select(g => new { Model = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Model)
            .Select(x => x.Model)
            .FirstOrDefaultAsync(cancellationToken);
        var selectedModel = !string.IsNullOrWhiteSpace(mostUsedModel)
            ? mostUsedModel
            : (availableModels.Contains(settings?.DefaultAiModel ?? string.Empty) ? settings!.DefaultAiModel : availableModels.FirstOrDefault() ?? "mock-gpt");

        return new DocumentWorkspaceViewModel
        {
            Id = d.Id,
            Title = d.Title,
            FileName = d.FileName,
            Status = d.Status,
            SizeBytes = d.SizeBytes,
            CurrentPage = 1,
            TotalPages = 1,
            ZoomPercent = 100,
            SelectionMode = false,
            SelectedRegion = new RegionSelectionViewModel
            {
                PageNumber = 1,
                X = 0,
                Y = 0,
                Width = 0,
                Height = 0,
            },
            // [TR] Eski OCR motorlarından kalan prefix'li placeholder metinleri gösterme.
            // Örn: "[Tesseract OCR] Metin tespit edilemedi..." veya "[Mock OCR]..."
            // gibi dahili hata mesajları kullanıcıya gösterilmez; bunun yerine boş bırakılır.
            OcrTextPlaceholder = FilterOcrPlaceholder(latestOcr?.ExtractedText),
            LastOcrResultId = latestOcr?.Id,
            DefaultAiModel = selectedModel,
            DefaultTranslationStyle = settings?.DefaultTranslationStyle ?? "Formal",
            ThemePreference = settings?.ThemePreference ?? "System",
            AvailableAiModels = availableModels,
        };
    }

    // [TR] Eski OCR motorlarından kalan prefix'li placeholder metinleri filtreler.
    // "[Tesseract OCR] ...", "[Mock OCR] ..." gibi dahili mesajlar boş string döner.
    private static string FilterOcrPlaceholder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (text.StartsWith("[Tesseract", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        if (text.StartsWith("[Mock OCR", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        if (text.StartsWith("[PaddleOCR", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return text;
    }

    public async Task<(Stream? Stream, string? ContentType, string? DownloadName)> OpenPdfAsync(
        string userEmail,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var d = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerEmail == userEmail, cancellationToken);
        if (d == null) return (null, null, null);

        var abs = Path.Combine(_env.ContentRootPath, d.StorageRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(abs)) return (null, null, null);

        var stream = new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, "application/pdf", d.FileName);
    }

    public async Task<string?> GetPdfPhysicalPathAsync(
        string userEmail,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var d = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerEmail == userEmail, cancellationToken);
        if (d == null) return null;

        var abs = Path.Combine(_env.ContentRootPath, d.StorageRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(abs) ? abs : null;
    }

    public async Task<(bool Ok, Guid? OcrResultId, string? ExtractedText, string? ErrorMessage)> SaveOcrResultAsync(
        string userEmail,
        Guid documentId,
        RegionSelectionViewModel region,
        string extractedText,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var doc = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId && x.OwnerEmail == userEmail, cancellationToken);
        if (doc == null)
            return (false, null, null, "Belge bulunamadı.");

        var entity = new OcrResult
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserEmail = userEmail,
            PageNumber = region.PageNumber,
            X = region.X,
            Y = region.Y,
            Width = region.Width,
            Height = region.Height,
            ExtractedText = extractedText,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        _db.OcrResults.Add(entity);
        await UpdateDocumentStatusAsync(documentId, DocumentStatus.OCR_READY, cancellationToken);
        AddActivity(userEmail, $"\"{doc.Title}\" için seçili bölgeden mock OCR metni üretildi.");
        await _db.SaveChangesAsync(cancellationToken);
        return (true, entity.Id, entity.ExtractedText, null);
    }

    public async Task<(bool Ok, string? ErrorMessage)> UpdateOcrTextAsync(
        string userEmail,
        Guid ocrResultId,
        string text,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var entity = await _db.OcrResults
            .FirstOrDefaultAsync(x => x.Id == ocrResultId && x.UserEmail == userEmail, cancellationToken);
        if (entity == null)
            return (false, "OCR sonucu bulunamadı.");

        entity.ExtractedText = text ?? string.Empty;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, Guid? AiResultId, string? ErrorMessage)> SaveAiResultAsync(
        string userEmail,
        Guid documentId,
        AiProcessRequestViewModel request,
        AiServiceResult aiResult,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var doc = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId && x.OwnerEmail == userEmail, cancellationToken);
        if (doc == null)
            return (false, null, "Belge bulunamadı.");

        var entity = new AiResult
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserEmail = userEmail,
            OperationType = request.OperationType ?? string.Empty,
            ModelName = request.ModelName ?? string.Empty,
            SourceLanguage = request.SourceLanguage ?? string.Empty,
            TargetLanguage = request.TargetLanguage ?? string.Empty,
            Style = request.Style ?? string.Empty,
            CustomInstruction = request.CustomInstruction ?? string.Empty,
            SourcePageNumber = request.SourcePageNumber <= 0 ? 1 : request.SourcePageNumber,
            NoteTitle = BuildDefaultNoteTitle(request.OperationType, doc.Title),
            UserNote = string.Empty,
            InputText = request.InputText ?? string.Empty,
            OutputText = aiResult.OutputText ?? string.Empty,
            OutputImageUrl = aiResult.OutputImageUrl ?? string.Empty,
            IsSaved = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        _db.AiResults.Add(entity);
        await UpdateDocumentStatusAsync(documentId, DocumentStatus.AI_READY, cancellationToken);
        AddActivity(userEmail, $"\"{doc.Title}\" için {entity.OperationType} AI işlemi üretildi.");
        await _db.SaveChangesAsync(cancellationToken);
        return (true, entity.Id, null);
    }

    public async Task<AiResultPageViewModel?> GetAiResultPageAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var data = await (
            from a in _db.AiResults.AsNoTracking()
            join d in _db.Documents.AsNoTracking() on a.DocumentId equals d.Id
            where a.Id == aiResultId && a.UserEmail == userEmail && d.OwnerEmail == userEmail
            select new AiResultPageViewModel
            {
                AiResultId = a.Id,
                DocumentId = d.Id,
                DocumentTitle = d.Title,
                OperationType = a.OperationType,
                ModelName = a.ModelName,
                SourceLanguage = a.SourceLanguage,
                TargetLanguage = a.TargetLanguage,
                SourcePageNumber = a.SourcePageNumber,
                Style = a.Style,
                CustomInstruction = a.CustomInstruction,
                InputText = a.InputText,
                OutputText = a.OutputText,
                OutputImageUrl = a.OutputImageUrl,
                IsSaved = a.IsSaved,
                UpdatedAtUtc = a.UpdatedAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return data;
    }

    public async Task<(bool Ok, string? ErrorMessage)> MarkAiResultSavedAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var entity = await _db.AiResults
            .FirstOrDefaultAsync(x => x.Id == aiResultId && x.UserEmail == userEmail, cancellationToken);
        if (entity == null)
            return (false, "AI sonucu bulunamadı.");

        entity.IsSaved = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await UpdateDocumentStatusAsync(entity.DocumentId, DocumentStatus.COMPLETED, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<AiProcessRequestViewModel?> GetAiRequestFromResultAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var entity = await _db.AiResults.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == aiResultId && x.UserEmail == userEmail, cancellationToken);
        if (entity == null)
            return null;

        return new AiProcessRequestViewModel
        {
            DocumentId = entity.DocumentId,
            OperationType = entity.OperationType,
            ModelName = entity.ModelName,
            SourceLanguage = entity.SourceLanguage,
            TargetLanguage = entity.TargetLanguage,
            Style = entity.Style,
            CustomInstruction = entity.CustomInstruction,
            InputText = entity.InputText,
            SourcePageNumber = entity.SourcePageNumber,
        };
    }

    public async Task<NotebookIndexViewModel> GetNotebookAsync(
        string userEmail,
        string? searchQuery,
        string? operationFilter,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var q = from a in _db.AiResults.AsNoTracking()
                join d in _db.Documents.AsNoTracking() on a.DocumentId equals d.Id
                where a.UserEmail == userEmail && d.OwnerEmail == userEmail && a.IsSaved
                select new { a, d };

        var s = searchQuery?.Trim();
        if (!string.IsNullOrWhiteSpace(s))
        {
            var lower = s.ToLowerInvariant();
            q = q.Where(x =>
                x.a.NoteTitle.ToLower().Contains(lower) ||
                x.d.Title.ToLower().Contains(lower) ||
                x.a.OutputText.ToLower().Contains(lower) ||
                x.a.UserNote.ToLower().Contains(lower));
        }

        var f = operationFilter?.Trim();
        if (!string.IsNullOrWhiteSpace(f))
            q = q.Where(x => x.a.OperationType == f);

        var list = await q.OrderByDescending(x => x.a.CreatedAtUtc).Take(300).ToListAsync(cancellationToken);
        var rows = list.Select(x => new NotebookRowViewModel
        {
            AiResultId = x.a.Id,
            DocumentId = x.d.Id,
            Title = string.IsNullOrWhiteSpace(x.a.NoteTitle) ? BuildDefaultNoteTitle(x.a.OperationType, x.d.Title) : x.a.NoteTitle,
            SourceDocument = x.d.Title,
            PageNumber = x.a.SourcePageNumber,
            OperationType = x.a.OperationType,
            Style = x.a.Style,
            CreatedAtUtc = x.a.CreatedAtUtc,
            PreviewContent = BuildPreviewText(x.a.OutputText, x.a.OutputImageUrl),
        }).ToList();

        return new NotebookIndexViewModel
        {
            SearchQuery = searchQuery ?? string.Empty,
            OperationFilter = operationFilter ?? string.Empty,
            HasAny = rows.Count > 0,
            Items = rows,
        };
    }

    public async Task<AiResultPageViewModel?> GetNotebookDetailsAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var detail = await GetAiResultPageAsync(userEmail, aiResultId, cancellationToken);
        if (detail == null || !detail.IsSaved)
            return null;
        return detail;
    }

    public async Task<NotebookEditViewModel?> GetNotebookEditAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var data = await (
            from a in _db.AiResults.AsNoTracking()
            join d in _db.Documents.AsNoTracking() on a.DocumentId equals d.Id
            where a.Id == aiResultId && a.UserEmail == userEmail && d.OwnerEmail == userEmail && a.IsSaved
            select new NotebookEditViewModel
            {
                AiResultId = a.Id,
                DocumentId = d.Id,
                NoteTitle = string.IsNullOrWhiteSpace(a.NoteTitle) ? BuildDefaultNoteTitle(a.OperationType, d.Title) : a.NoteTitle,
                UserNote = a.UserNote,
                SourceDocument = d.Title,
                OperationType = a.OperationType,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return data;
    }

    public async Task<(bool Ok, string? ErrorMessage)> UpdateNotebookEntryAsync(
        string userEmail,
        NotebookEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var entity = await _db.AiResults
            .FirstOrDefaultAsync(x => x.Id == model.AiResultId && x.UserEmail == userEmail && x.IsSaved, cancellationToken);
        if (entity == null)
            return (false, "Notebook kaydı bulunamadı.");

        entity.NoteTitle = model.NoteTitle?.Trim() ?? string.Empty;
        entity.UserNote = model.UserNote?.Trim() ?? string.Empty;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? ErrorMessage)> DeleteNotebookEntryAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var entity = await _db.AiResults
            .FirstOrDefaultAsync(x => x.Id == aiResultId && x.UserEmail == userEmail && x.IsSaved, cancellationToken);
        if (entity == null)
            return (false, "Notebook kaydı bulunamadı.");

        _db.AiResults.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<SettingsViewModel> GetSettingsAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var data = await _db.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserEmail == userEmail, cancellationToken);

        return new SettingsViewModel
        {
            Username = userEmail,
            Email = userEmail,
            DefaultAiModel = data?.DefaultAiModel ?? "mock-gpt",
            DefaultTranslationStyle = data?.DefaultTranslationStyle ?? "Formal",
            ThemePreference = data?.ThemePreference ?? "System",
            AvailableAiModels = GetAvailableAiModels(),
        };
    }

    public async Task<(bool Ok, string? ErrorMessage)> UpdateSettingsAsync(
        string userEmail,
        SettingsViewModel model,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var entity = await _db.UserSettings
            .FirstOrDefaultAsync(x => x.UserEmail == userEmail, cancellationToken);

        if (entity == null)
        {
            entity = new UserSettings
            {
                Id = Guid.NewGuid(),
                UserEmail = userEmail,
            };
            _db.UserSettings.Add(entity);
        }

        var availableModels = GetAvailableAiModels();
        var requestedModel = (model.DefaultAiModel ?? "mock-gpt").Trim();
        entity.DefaultAiModel = availableModels.Contains(requestedModel) ? requestedModel : availableModels.FirstOrDefault() ?? "mock-gpt";
        entity.DefaultTranslationStyle = (model.DefaultTranslationStyle ?? "Formal").Trim();
        entity.ThemePreference = (model.ThemePreference ?? "System").Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? ErrorMessage)> UploadAsync(
        string userEmail,
        IFormFile file,
        string? title,
        CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        if (file == null || file.Length == 0)
            return (false, "Dosya seçilmedi.");

        if (file.Length > DocumentUploadConstants.MaxBytes)
            return (false, $"Dosya en fazla {DocumentUploadConstants.MaxBytes / (1024 * 1024)} MB olabilir.");

        if (!IsAllowedPdfName(file.FileName))
            return (false, "Yalnızca .pdf uzantılı dosya yükleyebilirsiniz.");

        if (!string.IsNullOrEmpty(file.ContentType)
            && !file.ContentType.Equals(DocumentUploadConstants.AllowedContentType, StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("application/x-pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("İçerik türü şüpheli ama uzantı PDF: {Type}", file.ContentType);
        }

        await using var probe = file.OpenReadStream();
        if (!await HasPdfMagicHeaderAsync(probe, cancellationToken))
            return (false, "Dosya geçerli bir PDF gibi görünmüyor (başlık kontrolü).");

        var id = Guid.NewGuid();
        var folder = HashFolder(userEmail);
        var physicalDir = Path.Combine(_env.ContentRootPath, "Data", "uploads", folder);
        Directory.CreateDirectory(physicalDir);
        var physicalFile = Path.Combine(physicalDir, $"{id}.pdf");

        await using (var saveStream = file.OpenReadStream())
        await using (var fs = new FileStream(physicalFile, FileMode.CreateNew, FileAccess.Write))
        {
            await saveStream.CopyToAsync(fs, cancellationToken);
        }

        var displayTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : title.Trim();

        var doc = new Document
        {
            Id = id,
            OwnerEmail = userEmail,
            Title = displayTitle,
            FileName = file.FileName,
            ContentType = DocumentUploadConstants.AllowedContentType,
            SizeBytes = file.Length,
            Status = DocumentStatus.UPLOADED,
            StorageRelativePath = Path.Combine("Data", "uploads", folder, $"{id}.pdf").Replace('\\', '/'),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        _db.Documents.Add(doc);
        AddActivity(userEmail, $"\"{displayTitle}\" PDF olarak yüklendi.");
        await _db.SaveChangesAsync(cancellationToken);

        // [TR] Mock iş akışı: gerçek OCR kuyruğu yok; durumu “işleniyor”a çekip hemen OCR hazır göstermek demo için yorumda bırakıldı.
        return (true, null);
    }

    public async Task<(bool Ok, string? ErrorMessage)> DeleteAsync(string userEmail, Guid id, CancellationToken cancellationToken = default)
    {
        userEmail = userEmail.Trim();
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && d.OwnerEmail == userEmail, cancellationToken);
        if (doc == null)
            return (false, "Belge bulunamadı.");

        var abs = Path.Combine(_env.ContentRootPath, doc.StorageRelativePath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            if (File.Exists(abs))
                File.Delete(abs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dosya silinemedi: {Path}", abs);
        }

        var title = doc.Title;
        var ocrRows = await _db.OcrResults.Where(x => x.DocumentId == id && x.UserEmail == userEmail).ToListAsync(cancellationToken);
        var aiRows = await _db.AiResults.Where(x => x.DocumentId == id && x.UserEmail == userEmail).ToListAsync(cancellationToken);
        if (ocrRows.Count > 0) _db.OcrResults.RemoveRange(ocrRows);
        if (aiRows.Count > 0) _db.AiResults.RemoveRange(aiRows);
        _db.Documents.Remove(doc);
        AddActivity(userEmail, $"\"{title}\" silindi.");
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private void AddActivity(string userEmail, string message)
    {
        _db.ActivityEntries.Add(new ActivityEntry
        {
            Id = Guid.NewGuid(),
            UserEmail = userEmail,
            Message = message,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static DashboardStatsViewModel BuildDashboardStats(List<Document> docs)
    {
        int C(DocumentStatus s) => docs.Count(d => d.Status == s);
        return new DashboardStatsViewModel
        {
            Total = docs.Count,
            Uploaded = C(DocumentStatus.UPLOADED),
            Processing = C(DocumentStatus.PROCESSING),
            OcrReady = C(DocumentStatus.OCR_READY),
            AiReady = C(DocumentStatus.AI_READY),
            Completed = C(DocumentStatus.COMPLETED),
            Failed = C(DocumentStatus.FAILED),
        };
    }

    private static bool IsAllowedPdfName(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return DocumentUploadConstants.AllowedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> HasPdfMagicHeaderAsync(Stream stream, CancellationToken ct)
    {
        var buf = new byte[4];
        var n = await stream.ReadAsync(buf.AsMemory(0, 4), ct);
        return n == 4 && buf.AsSpan().SequenceEqual(DocumentUploadConstants.PdfMagic);
    }

    private static string HashFolder(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }

    private static DocumentRowViewModel ToRow(Document d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        FileName = d.FileName,
        Status = d.Status,
        SizeBytes = d.SizeBytes,
        UpdatedAtUtc = d.UpdatedAtUtc,
    };

    private static string BuildDefaultNoteTitle(string? operationType, string documentTitle)
    {
        var op = string.IsNullOrWhiteSpace(operationType) ? "AI" : operationType;
        return $"{op} - {documentTitle}";
    }

    private static string BuildPreviewText(string outputText, string outputImageUrl)
    {
        if (!string.IsNullOrWhiteSpace(outputText))
            return outputText.Length <= 160 ? outputText : $"{outputText[..160]}...";
        if (!string.IsNullOrWhiteSpace(outputImageUrl))
            return "Gorsel uretim sonucu mevcut.";
        return "-";
    }

    /// <summary>
    /// [TR] appsettings'teki model listesine gore kullanilabilir AI model seceneklerini dondurur.
    /// </summary>
    private List<string> GetAvailableAiModels()
    {
        var models = _configuration.GetSection("Ai:Models").Get<List<string>>() ?? new List<string>();
        models = models
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (models.Count == 0)
            models.Add("mock-gpt");
        return models;
    }

    /// <summary>
    /// [TR] Belge durum gecisini tek noktadan gunceller.
    /// </summary>
    private async Task UpdateDocumentStatusAsync(Guid documentId, DocumentStatus next, CancellationToken cancellationToken)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (doc == null) return;
        doc.Status = next;
        doc.UpdatedAtUtc = DateTime.UtcNow;
    }
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Workspace modülü notu:
 * - GetWorkspaceAsync ve OpenPdfAsync ile Details sayfası artık gerçek çalışma alanı akışını besler.
 * - OCR bölgesel olarak çalışır; AI katmanı çeviri/özet/rewrite/creative/visualize mock akışı sağlar.
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu bölge seçimi eklendiğinde RegionSelection listesi service DTO’ya genişletilebilir.
 * - PDF stream için Range desteği (büyük dosyalarda performans) ileride eklenebilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * -----------------------------------------------------------------------------
 */
