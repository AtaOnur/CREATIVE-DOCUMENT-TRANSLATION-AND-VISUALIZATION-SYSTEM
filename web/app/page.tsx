import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "@/lib/auth/config";

export default function HomePage() {
  if (cookies().get(AUTH_COOKIE)?.value) {
    redirect("/app");
  }
  redirect("/login");
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Kök / — oturum varsa /app, yoksa /login yönlendirmesi.
 * [TR] Neden gerekli: Tek giriş noktası; auth sonrası doğru alana düşmek.
 * [TR] Sistem içinde: Site kök URL’si.
 *
 * MODIFICATION NOTES (TR)
 * - Pazarlama landing: oturumsuz kullanıcıya farklı sayfa göstermek için koşul genişletilir.
 * - locale prefix: /tr /en yapısına göre redirect güncellenir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
