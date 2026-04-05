import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { AUTH_COOKIE } from "@/lib/auth/config";

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const hasSession = Boolean(request.cookies.get(AUTH_COOKIE)?.value);

  if (pathname.startsWith("/app")) {
    if (!hasSession) {
      const url = request.nextUrl.clone();
      url.pathname = "/login";
      url.searchParams.set("from", pathname);
      return NextResponse.redirect(url);
    }
  }

  if ((pathname === "/login" || pathname === "/register") && hasSession) {
    return NextResponse.redirect(new URL("/app", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/app/:path*", "/login", "/register"],
};

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app altını oturum çerezi yoksa /login’e yönlendirir; girişli kullanıcıyı login/register’dan uygulamaya alır.
 * [TR] Neden gerekli: Edge’de hızlı rota koruması (sunucu bileşeni ile çift kontrol).
 * [TR] Sistem içinde: Next.js kök middleware; her istekte matcher ile eşleşen path’ler.
 *
 * MODIFICATION NOTES (TR)
 * - Rol tabanlı koruma: JWT içinden rol okuma veya edge’de session store sorgusu (dikkat: edge süresi).
 * - Google OAuth: callback path’lerini matcher dışında bırakın veya açıkça izin verin.
 * - API route koruması: /api/* için ayrı kural veya ortak yardımcı.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
