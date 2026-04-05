import dynamic from "next/dynamic";
import { notFound, redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { getDocumentById } from "@/lib/documents/mock-store";
import { getWorkspacePdfUrl } from "@/lib/documents/workspace-pdf-url";

const DocumentWorkspace = dynamic(
  () => import("@/components/documents/document-workspace").then((m) => m.DocumentWorkspace),
  {
    ssr: false,
    loading: () => (
      <div className="flex min-h-[320px] items-center justify-center rounded-xl border border-slate-200 bg-white text-sm text-slate-600">
        Çalışma alanı yükleniyor…
      </div>
    ),
  }
);

type Props = { params: { id: string } };

export default function DocumentDetailPage({ params }: Props) {
  const session = getSession();
  if (!session) redirect("/login");

  const doc = getDocumentById(session.email, params.id);
  if (!doc) notFound();

  const pdfUrl = getWorkspacePdfUrl(doc);

  return (
    <div className="flex min-h-[calc(100dvh-5rem)] flex-col pb-6">
      {/* [TR] flex-1 + min-h-0: çalışma alanı kabukta dikey alanı doldurur; PDF sütunu sığar. */}
      <div className="flex min-h-0 flex-1 flex-col">
        <DocumentWorkspace
          documentId={doc.id}
          title={doc.title}
          fileName={doc.fileName}
          status={doc.status}
          pdfUrl={pdfUrl}
        />
      </div>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app/documents/[id] — sunucuda oturum + belge doğrular, istemci çalışma alanını yükler.
 * [TR] Neden gerekli: react-pdf / PDF.js yalnızca tarayıcıda; dynamic(..., { ssr: false }) ile SSR hataları önlenir.
 * [TR] Sistem içinde: documents-table “Aç” bağlantıları, getWorkspacePdfUrl (mock PDF yolu).
 *
 * MODIFICATION NOTES (TR)
 * - Prisma ile birleştirme: getDocumentById yerine veritabanı + aynı props.
 * - PDF URL: imzalı blob URL veya API route stream.
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Orta (auth + depo entegrasyonu).
 * -----------------------------------------------------------------------------
 */
