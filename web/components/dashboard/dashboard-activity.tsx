import type { ActivityEntry } from "@/lib/documents/types";
import { formatDateTime } from "@/lib/documents/format";

export function DashboardActivity({ entries }: { entries: ActivityEntry[] }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900">Son aktiviteler</h2>
      <p className="mt-1 text-xs text-slate-500">Mock günlük; gerçek uygulamada veritabanı veya olay kuyruğundan gelir.</p>
      {entries.length === 0 ? (
        <p className="mt-6 text-sm text-slate-500">Henüz kayıt yok.</p>
      ) : (
        <ul className="mt-4 space-y-3">
          {entries.map((a) => (
            <li key={a.id} className="border-l-2 border-slate-200 pl-3 text-sm">
              <span className="text-xs text-slate-400">{formatDateTime(a.at)}</span>
              <p className="text-slate-700">{a.message}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Son olayların zaman çizelgesi (mock aktivite listesi).
 * [TR] Neden gerekli: Panoda hareket hissi ve savunmada denetlenebilirlik hikâyesi.
 * [TR] Sistem içinde: /app/dashboard
 *
 * MODIFICATION NOTES (TR)
 * - AuditLog tablosu ile değiştirme: sunucu sorgusu + sayfalama.
 * - Filtre (yalnızca yükleme / yalnızca hata): query parametresi.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
