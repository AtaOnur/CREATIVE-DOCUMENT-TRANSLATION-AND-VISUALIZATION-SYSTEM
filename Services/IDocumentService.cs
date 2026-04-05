using Microsoft.AspNetCore.Http;
using pdf_bitirme.Models.ViewModels;

namespace pdf_bitirme.Services;

/*
 * [TR] Bu dosya ne işe yarar: Belge işlemleri sözleşmesi — pano, liste, yükleme, silme, detay.
 * [TR] Neden gerekli: Denetleyici ince kalır; savunmada iş mantığı serviste gösterilir.
 * [TR] İlgili: DocumentService, DocumentsController, DashboardController
 *
 * MODIFICATION NOTES (TR)
 * - Bulut depoya delegasyon (S3, Blob).
 * - Arka plan kuyruğu ile durum geçişleri (Hangfire / Azure Queue).
 * - Çoklu dosya yükleme desteği.
 * - Zorluk: Orta.
 */
public interface IDocumentService
{
    Task<DashboardViewModel> GetDashboardAsync(string userEmail, CancellationToken cancellationToken = default);

    Task<DocumentsIndexViewModel> ListAsync(
        string userEmail,
        string? searchQuery,
        string? statusFilter,
        CancellationToken cancellationToken = default);

    Task<DocumentDetailsViewModel?> GetDetailsAsync(string userEmail, Guid id, CancellationToken cancellationToken = default);

    Task<DocumentWorkspaceViewModel?> GetWorkspaceAsync(string userEmail, Guid id, CancellationToken cancellationToken = default);

    Task<(Stream? Stream, string? ContentType, string? DownloadName)> OpenPdfAsync(
        string userEmail,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<string?> GetPdfPhysicalPathAsync(
        string userEmail,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, Guid? OcrResultId, string? ExtractedText, string? ErrorMessage)> SaveOcrResultAsync(
        string userEmail,
        Guid documentId,
        RegionSelectionViewModel region,
        string extractedText,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> UpdateOcrTextAsync(
        string userEmail,
        Guid ocrResultId,
        string text,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, Guid? AiResultId, string? ErrorMessage)> SaveAiResultAsync(
        string userEmail,
        Guid documentId,
        AiProcessRequestViewModel request,
        AiServiceResult aiResult,
        CancellationToken cancellationToken = default);

    Task<AiResultPageViewModel?> GetAiResultPageAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> MarkAiResultSavedAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default);

    Task<AiProcessRequestViewModel?> GetAiRequestFromResultAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default);

    Task<NotebookIndexViewModel> GetNotebookAsync(
        string userEmail,
        string? searchQuery,
        string? operationFilter,
        CancellationToken cancellationToken = default);

    Task<AiResultPageViewModel?> GetNotebookDetailsAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default);

    Task<NotebookEditViewModel?> GetNotebookEditAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> UpdateNotebookEntryAsync(
        string userEmail,
        NotebookEditViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> DeleteNotebookEntryAsync(
        string userEmail,
        Guid aiResultId,
        CancellationToken cancellationToken = default);

    Task<SettingsViewModel> GetSettingsAsync(string userEmail, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> UpdateSettingsAsync(
        string userEmail,
        SettingsViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> UploadAsync(
        string userEmail,
        IFormFile file,
        string? title,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorMessage)> DeleteAsync(string userEmail, Guid id, CancellationToken cancellationToken = default);
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Workspace servis notu:
 * - GetWorkspaceAsync ve OpenPdfAsync ile PDF çalışma alanı rotası desteklenir.
 *
 * MODIFICATION NOTES (TR)
 * - İleride OCR pipeline eklendiğinde ExtractTextFromRegion(...) imzası eklenebilir.
 * - Çoklu bölge DTO’su ayrı bir modelle taşınabilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * -----------------------------------------------------------------------------
 */
