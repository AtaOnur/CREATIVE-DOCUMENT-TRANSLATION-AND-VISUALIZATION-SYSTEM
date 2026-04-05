import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { documentStatuses } from "@/lib/types/enums";
import type { DocumentStatusValue } from "@/lib/types/enums";

type Props = {
  defaultQuery?: string;
  defaultStatus?: DocumentStatusValue | "";
};

const statusLabels: Record<DocumentStatusValue | "", string> = {
  "": "Tüm durumlar",
  UPLOADED: "Yüklendi",
  PROCESSING: "İşleniyor",
  OCR_READY: "OCR hazır",
  AI_READY: "AI hazır",
  COMPLETED: "Tamamlandı",
  FAILED: "Hata",
};

export function DocumentsFilterBar({ defaultQuery = "", defaultStatus = "" }: Props) {
  return (
    <form
      action="/app/documents"
      method="get"
      className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:flex-wrap sm:items-end"
    >
      <div className="min-w-[200px] flex-1 space-y-2">
        <Label htmlFor="q">Ara</Label>
        <Input id="q" name="q" placeholder="Başlık veya dosya adı…" defaultValue={defaultQuery} />
      </div>
      <div className="w-full min-w-[180px] space-y-2 sm:w-48">
        <Label htmlFor="status">Durum</Label>
        <select
          id="status"
          name="status"
          defaultValue={defaultStatus}
          className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400"
        >
          <option value="">{statusLabels[""]}</option>
          {documentStatuses.map((v) => (
            <option key={v} value={v}>
              {statusLabels[v]}
            </option>
          ))}
        </select>
      </div>
      <div className="flex gap-2">
        <Button type="submit">Filtrele</Button>
        <Button type="button" variant="outline" asChild>
          <Link href="/app/documents">Sıfırla</Link>
        </Button>
      </div>
    </form>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Belgeler sayfasında GET ile arama ve durum filtresi (paylaşılabilir URL).
 * [TR] Neden gerekli: Sunucu bileşeninde listeyi sade tutmak; JavaScript şart değil.
 * [TR] Sistem içinde: /app/documents
 *
 * MODIFICATION NOTES (TR)
 * - Gelişmiş filtre (tarih aralığı): ek query parametreleri ve alanlar.
 * - Sıfırla düğmesi: şu an aynı action’a gider; tam sıfırlama için `/app/documents` linki de kullanılabilir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
