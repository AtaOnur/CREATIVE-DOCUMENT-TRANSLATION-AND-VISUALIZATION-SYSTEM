import { FileStack, CheckCircle2, Loader2, AlertCircle } from "lucide-react";

type Stats = {
  total: number;
  processing: number;
  completed: number;
  failed: number;
};

export function DashboardSummaryCards({ stats }: { stats: Stats }) {
  const cards = [
    { label: "Toplam belge", value: stats.total, icon: FileStack, tone: "bg-white border-slate-200" },
    { label: "İşleniyor", value: stats.processing, icon: Loader2, tone: "bg-white border-blue-100" },
    { label: "Tamamlanan", value: stats.completed, icon: CheckCircle2, tone: "bg-white border-emerald-100" },
    { label: "Hata", value: stats.failed, icon: AlertCircle, tone: "bg-white border-red-100" },
  ];

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map(({ label, value, icon: Icon, tone }) => (
        <div
          key={label}
          className={`flex items-center gap-3 rounded-xl border p-4 shadow-sm ${tone}`}
        >
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-slate-50 text-slate-700">
            <Icon className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</p>
            <p className="text-2xl font-semibold text-slate-900">{value}</p>
          </div>
        </div>
      ))}
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Pano özet kartları (toplam, işleniyor, tamamlanan, hata).
 * [TR] Neden gerekli: Savunmada tek bakışta iş yükü özeti.
 * [TR] Sistem içinde: /app/dashboard
 *
 * MODIFICATION NOTES (TR)
 * - OCR hazır / AI hazır ayrı kartlar: stats nesnesi genişletilir.
 * - Tıklanınca filtreli liste: Link ile /app/documents?status=…
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
