import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { AppShell } from "@/components/layout/app-shell";

export default function AppSectionLayout({ children }: { children: React.ReactNode }) {
  const session = getSession();
  if (!session) {
    redirect("/login");
  }
  return <AppShell user={session}>{children}</AppShell>;
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app/* için korumalı kabuk — oturum yoksa login; varsa sidebar + üst çubuk.
 * [TR] Neden gerekli: middleware ile çift kontrol + sunucu bileşeninde kullanıcı bilgisi.
 * [TR] Sistem içinde: /app altındaki tüm sayfalar.
 *
 * MODIFICATION NOTES (TR)
 * - Rol bazlı menü: session’a rol eklenince sidebar filtrelenir.
 * - Onboarding boş sayfa: ilk girişte layout içinde yönlendirme.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
