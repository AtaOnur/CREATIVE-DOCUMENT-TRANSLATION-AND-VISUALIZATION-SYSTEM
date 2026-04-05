import { cookies } from "next/headers";
import { AUTH_COOKIE } from "./config";
import type { SessionUser } from "./types";

export function getSession(): SessionUser | null {
  const raw = cookies().get(AUTH_COOKIE)?.value;
  if (!raw) return null;
  try {
    const data = JSON.parse(raw) as SessionUser;
    if (!data?.email || typeof data.email !== "string") return null;
    return {
      email: data.email,
      name: typeof data.name === "string" ? data.name : data.email.split("@")[0]!,
    };
  } catch {
    return null;
  }
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Sunucu bileşenlerinde httpOnly çerezden oturum okur.
 * [TR] Neden gerekli: Korumalı layout’ta kullanıcı adı göstermek ve sunucu tarafı kontrol.
 * [TR] Sistem içinde: app/(protected)/app/layout.tsx ve ileride sunucu eylemleri.
 *
 * MODIFICATION NOTES (TR)
 * - JWT veya session id + veritabanı sorgusu: parse mantığı burada veya ayrı bir serviste değişir.
 * - Çoklu cihaz oturumu: sunucu tarafı session tablosu ile eşlenir.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
