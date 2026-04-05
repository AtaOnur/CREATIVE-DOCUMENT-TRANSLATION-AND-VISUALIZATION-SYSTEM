import { cn } from "@/lib/utils";
import type { DocumentStatusValue } from "@/lib/types/enums";

const labels: Record<DocumentStatusValue, string> = {
  UPLOADED: "Yüklendi",
  PROCESSING: "İşleniyor",
  OCR_READY: "OCR hazır",
  AI_READY: "AI hazır",
  COMPLETED: "Tamamlandı",
  FAILED: "Hata",
};

const styles: Record<DocumentStatusValue, string> = {
  UPLOADED: "border-slate-200 bg-slate-50 text-slate-700",
  PROCESSING: "border-blue-200 bg-blue-50 text-blue-800",
  OCR_READY: "border-amber-200 bg-amber-50 text-amber-900",
  AI_READY: "border-violet-200 bg-violet-50 text-violet-900",
  COMPLETED: "border-emerald-200 bg-emerald-50 text-emerald-900",
  FAILED: "border-red-200 bg-red-50 text-red-800",
};

export function DocumentStatusBadge({ status }: { status: DocumentStatusValue }) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-medium",
        styles[status]
      )}
    >
      {labels[status]}
    </span>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Belge yaşam döngüsü durumu için renkli rozet (sunum dostu).
 * [TR] Neden gerekli: Listede ve panoda durumun hızlı okunması.
 * [TR] Sistem içinde: belge tablosu ve detay.
 *
 * MODIFICATION NOTES (TR)
 * - Özel iş akışı durumları: labels + styles haritasına ekleme.
 * - Erişilebilirlik: aria-label veya title ile tam açıklama.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
