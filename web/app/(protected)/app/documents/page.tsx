import Link from "next/link";
import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { filterDocuments } from "@/lib/documents/mock-store";
import { documentStatuses } from "@/lib/types/enums";
import type { DocumentStatusValue } from "@/lib/types/enums";
import { DocumentsFilterBar } from "@/components/documents/documents-filter-bar";
import { DocumentsTable } from "@/components/documents/documents-table";

function parseStatus(raw: string | undefined): DocumentStatusValue | "" {
  if (!raw) return "";
  return documentStatuses.includes(raw as DocumentStatusValue) ? (raw as DocumentStatusValue) : "";
}

type Props = {
  searchParams: Record<string, string | string[] | undefined>;
};

export default function DocumentsPage({ searchParams }: Props) {
  const session = getSession();
  if (!session) redirect("/login");

  const q = typeof searchParams.q === "string" ? searchParams.q : "";
  const status = parseStatus(typeof searchParams.status === "string" ? searchParams.status : undefined);
  const uploaded = searchParams.uploaded === "1";

  const documents = filterDocuments(session.email, { query: q, status: status || "" });

  return (
    <div className="mx-auto max-w-6xl space-y-6 pb-10">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Belgeler</h1>
          <p className="mt-1 text-sm text-slate-600">
            Yalnızca PDF. Arama ve durum filtresi adres çubuğuna yansır (paylaşılabilir bağlantı).
          </p>
        </div>
        <Link
          href="/app/upload"
          className="inline-flex h-10 items-center justify-center rounded-md bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800"
        >
          PDF yükle
        </Link>
      </div>

      {uploaded ? (
        <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900">
          Yükleme tamamlandı (mock depo). Liste aşağıda güncellenmiş olmalı.
        </p>
      ) : null}

      <DocumentsFilterBar defaultQuery={q} defaultStatus={status} />
      <DocumentsTable documents={documents} />
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app/documents — filtre çubuğu ve tablo/kart liste; mock veri.
 * [TR] Neden gerekli: Çekirdek belge yönetimi arayüzü.
 * [TR] Sistem içinde: sidebar Belgeler bağlantısı.
 *
 * MODIFICATION NOTES (TR)
 * - Sunucu tarafı sayfalama: ?page= ile slice.
 * - Dışa aktarma (CSV): ek route veya action.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
