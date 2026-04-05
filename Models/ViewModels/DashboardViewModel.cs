namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: Pano sayfasının tek bağlama modeli — özet, listeler, boş durum bayrakları.
 * [TR] İlgili: DashboardController, Views/Dashboard/Index.cshtml
 *
 * MODIFICATION NOTES (TR)
 * - Grafik veri noktaları (son 7 gün yükleme) eklenebilir.
 * - Kişiselleştirilmiş widget sırası.
 * - Zorluk: Kolay.
 */
public class DashboardViewModel
{
    public string UserEmail { get; set; } = string.Empty;
    public DashboardStatsViewModel Stats { get; set; } = new();
    public IReadOnlyList<DocumentRowViewModel> RecentDocuments { get; set; } = Array.Empty<DocumentRowViewModel>();
    public IReadOnlyList<ActivityRowViewModel> RecentActivity { get; set; } = Array.Empty<ActivityRowViewModel>();
    public IReadOnlyList<DocumentRowViewModel> ContinueWorking { get; set; } = Array.Empty<DocumentRowViewModel>();
    public bool HasAnyDocuments { get; set; }
}

public class DashboardStatsViewModel
{
    public int Total { get; set; }
    public int Uploaded { get; set; }
    public int Processing { get; set; }
    public int OcrReady { get; set; }
    public int AiReady { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
}

public class ActivityRowViewModel
{
    public string Message { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
}
