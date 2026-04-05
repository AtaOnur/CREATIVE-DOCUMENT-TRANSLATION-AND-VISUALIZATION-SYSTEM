import Link from "next/link";
import type { MockDocument } from "@/lib/documents/types";
import { DocumentStatusBadge } from "@/components/documents/document-status-badge";
import { Button } from "@/components/ui/button";
import { formatDateTime } from "@/lib/documents/format";

export function DashboardContinue({ documents }: { documents: MockDocument[] }) {
  if (documents.length === 0) {
    return (
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900">Kaldığın yerden devam</h2>
        <p className="mt-2 text-sm text-slate-600">
          Devam edecek açık iş yok. Yeni PDF yükleyerek başlayabilirsin.
        </p>
        <Button className="mt-4" asChild>
          <Link href="/app/upload">PDF yükle</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900">Kaldığın yerden devam</h2>
      <p className="mt-1 text-xs text-slate-500">Tamamlanmamış belgeler (öncelik sırasıyla).</p>
      <ul className="mt-4 divide-y divide-slate-100">
        {documents.map((doc) => (
          <li key={doc.id} className="flex items-center gap-3 py-3 first:pt-0">
            <div className="min-w-0 flex-1">
              <Link
                href={`/app/documents/${doc.id}`}
                className="truncate font-medium text-slate-900 hover:underline"
              >
                {doc.title}
              </Link>
              <p className="text-xs text-slate-500">{formatDateTime(doc.updatedAt)}</p>
            </div>
            <DocumentStatusBadge status={doc.status} />
          </li>
        ))}
      </ul>
      <Button variant="outline" className="mt-4 w-full sm:w-auto" asChild>
        <Link href="/app/documents">Tüm belgeler</Link>
      </Button>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Tamamlanmamış belgeler listesi veya boş durumda yükleme çağrısı.
 * [TR] Neden gerekli: Kullanıcıyı iş akışına geri sokmak için tipik pano deseni.
 * [TR] Sistem içinde: /app/dashboard
 *
 * MODIFICATION NOTES (TR)
 * - Son düzenlenen sayfa / bölge: belge detayından okunacak alanlarla zenginleştirilebilir.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
