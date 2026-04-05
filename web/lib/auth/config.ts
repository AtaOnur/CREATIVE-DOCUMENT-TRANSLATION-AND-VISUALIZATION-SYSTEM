/**
 * Auth cookie ve demo sabitleri.
 * Üretimde JWT/session store ve ortam değişkenleri ile değiştirilmelidir.
 */

export const AUTH_COOKIE = "cd_session";

/** Demo giriş: herhangi bir e-posta + bu şifre ile oturum açılır (sadece mock). */
export const DEMO_PASSWORD = "demo123";

export const SESSION_MAX_AGE_SEC = 60 * 60 * 24 * 7;

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Oturum çerezinin adı, demo şifre sabiti ve süre ayarını tutar.
 * [TR] Neden gerekli: middleware, server actions ve session okuma tek yerden aynı anahtarı kullanır.
 * [TR] Sistem içinde: lib/auth/actions.ts, session.ts, middleware.ts.
 *
 * MODIFICATION NOTES (TR)
 * - İleride Google / SSO eklendiğinde: ayrı provider bayrakları veya cookie adları eklenebilir.
 * - İki faktör (2FA) sonrası: geçici “pending_2fa” çerezi veya kısa ömürlü token kullanımı.
 * - Admin rolleri: çerezde rol taşımak yerine sunucuda veritabanından okumak daha güvenli; bu mock yapı kolayca genişler.
 * - Etkilenen dosyalar: Bu dosya, actions, middleware, ileride Prisma User ile eşleme.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
