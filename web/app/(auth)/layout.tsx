import Link from "next/link";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-50 to-slate-100">
      <div className="mx-auto flex min-h-screen w-full max-w-lg flex-col px-4 py-10 sm:py-16">
        <Link href="/" className="mb-8 flex items-center gap-2 text-slate-700 hover:text-slate-900">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-slate-900 text-sm font-bold text-white">
            CD
          </span>
          <span className="font-semibold tracking-tight">Creative Doc</span>
        </Link>
        {children}
      </div>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Giriş/kayıt/şifre sayfaları için ortak üst marka ve geniş arka plan.
 * [TR] Neden gerekli: Route grubunda tekrarlayan çerçeveyi tek yerde toplar.
 * [TR] Sistem içinde: (auth) altındaki sayfalar.
 *
 * MODIFICATION NOTES (TR)
 * - Kurumsal logo görseli, karanlık mod: arka plan ve header güncellenir.
 * - Dil seçici: layout üstüne küçük seçim eklenebilir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
