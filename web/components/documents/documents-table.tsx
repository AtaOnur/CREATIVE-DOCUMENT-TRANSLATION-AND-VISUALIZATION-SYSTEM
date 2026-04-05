import Link from "next/link";
import type { MockDocument } from "@/lib/documents/types";
import { formatBytes, formatDateTime } from "@/lib/documents/format";
import { DocumentStatusBadge } from "./document-status-badge";
import { deleteDocumentAction } from "@/lib/documents/actions";
import { Button } from "@/components/ui/button";

export function DocumentsTable({ documents }: { documents: MockDocument[] }) {
  if (documents.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-slate-200 bg-white py-16 text-center text-sm text-slate-500">
        Bu filtrelere uygun belge yok. Yeni PDF yüklemek için{" "}
        <Link href="/app/upload" className="font-medium text-slate-900 underline-offset-4 hover:underline">
          yükleme
        </Link>{" "}
        sayfasını kullanın.
      </div>
    );
  }

  return (
    <>
      {/* masaüstü: tablo */}
      <div className="hidden overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm lg:block">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs font-medium uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">Belge</th>
              <th className="px-4 py-3">Durum</th>
              <th className="px-4 py-3">Boyut</th>
              <th className="px-4 py-3">Güncellendi</th>
              <th className="px-4 py-3 text-right">İşlemler</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {documents.map((doc) => (
              <tr key={doc.id} className="text-slate-700 hover:bg-slate-50/80">
                <td className="px-4 py-3">
                  <div className="font-medium text-slate-900">{doc.title}</div>
                  <div className="text-xs text-slate-500">{doc.fileName}</div>
                </td>
                <td className="px-4 py-3">
                  <DocumentStatusBadge status={doc.status} />
                </td>
                <td className="px-4 py-3 text-slate-600">{formatBytes(doc.sizeBytes)}</td>
                <td className="px-4 py-3 text-slate-600">{formatDateTime(doc.updatedAt)}</td>
                <td className="px-4 py-3 text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="outline" size="sm" asChild>
                      <Link href={`/app/documents/${doc.id}`}>Aç</Link>
                    </Button>
                    <form action={deleteDocumentAction}>
                      <input type="hidden" name="id" value={doc.id} />
                      <Button type="submit" variant="outline" size="sm" className="text-red-700 hover:bg-red-50">
                        Sil
                      </Button>
                    </form>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* mobil: kartlar */}
      <ul className="space-y-3 lg:hidden">
        {documents.map((doc) => (
          <li key={doc.id} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="font-medium text-slate-900">{doc.title}</p>
                <p className="truncate text-xs text-slate-500">{doc.fileName}</p>
              </div>
              <DocumentStatusBadge status={doc.status} />
            </div>
            <p className="mt-2 text-xs text-slate-500">
              {formatBytes(doc.sizeBytes)} · {formatDateTime(doc.updatedAt)}
            </p>
            <div className="mt-3 flex gap-2">
              <Button variant="outline" size="sm" className="flex-1" asChild>
                <Link href={`/app/documents/${doc.id}`}>Aç</Link>
              </Button>
              <form action={deleteDocumentAction} className="flex-1">
                <input type="hidden" name="id" value={doc.id} />
                <Button type="submit" variant="outline" size="sm" className="w-full text-red-700 hover:bg-red-50">
                  Sil
                </Button>
              </form>
            </div>
          </li>
        ))}
      </ul>
    </>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Belge listesi — geniş ekranda tablo, mobilde kart; aç/sil eylemleri.
 * [TR] Neden gerekli: Savunmada ana özellik setinin görünür olması.
 * [TR] Sistem içinde: /app/documents
 *
 * MODIFICATION NOTES (TR)
 * - Sayfalama: API ve query parametreleri ile slice.
 * - Toplu silme: seçim kutuları ve toplu action.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
