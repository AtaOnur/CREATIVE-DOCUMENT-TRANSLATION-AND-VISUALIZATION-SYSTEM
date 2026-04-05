import Link from "next/link";
import { Button } from "@/components/ui/button";
import { FileText } from "lucide-react";

export function DashboardEmpty() {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-slate-300 bg-white px-6 py-16 text-center shadow-sm">
      <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-slate-100 text-slate-600">
        <FileText className="h-7 w-7" />
      </div>
      <h2 className="text-lg font-semibold text-slate-900">Henüz belgen yok</h2>
      <p className="mt-2 max-w-md text-sm text-slate-600">
        Bu alanda PDF belgelerin özetini göreceksin. İlk dosyayı yükleyerek başla; yalnızca PDF desteklenir (
        <span className="whitespace-nowrap">genel görüntü OCR — gelecek çalışma</span>).
      </p>
      <div className="mt-6 flex flex-wrap justify-center gap-2">
        <Button asChild>
          <Link href="/app/upload">PDF yükle</Link>
        </Button>
        <Button variant="outline" asChild>
          <Link href="/app/documents">Belgeler sayfası</Link>
        </Button>
      </div>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Belge sayısı sıfırken panoda boş durum ve net çağrı düğmeleri.
 * [TR] Neden gerekli: İlk kullanım ve savunma demosunda kırık hissi önler.
 * [TR] Sistem içinde: /app/dashboard
 *
 * MODIFICATION NOTES (TR)
 * - Şablon/indirme linki: örnek PDF sunma.
 * - Çoklu depo entegrasyonu: “Drive’dan içe aktar” gibi ikincil CTA.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
