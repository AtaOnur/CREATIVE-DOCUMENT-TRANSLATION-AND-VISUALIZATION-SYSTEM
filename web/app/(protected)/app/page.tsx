import { redirect } from "next/navigation";

export default function AppRootPage() {
  redirect("/app/dashboard");
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app kökü — doğrudan panoya yönlendirme.
 * [TR] Neden gerekli: Kısa URL ile tutarlı giriş noktası.
 * [TR] Sistem içinde: middleware korumalı /app.
 *
 * MODIFICATION NOTES (TR)
 * - Rol bazlı yönlendirme: admin → /app/admin vb.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
