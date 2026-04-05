export type SessionUser = {
  email: string;
  name: string;
};

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Oturumda tutulan kullanıcı tipini tanımlar (mock JSON çerez ile uyumlu).
 * [TR] Neden gerekli: layout ve bileşenlerde tip güvenli props için.
 * [TR] Sistem içinde: session.ts, actions, app kabuğu bileşenleri.
 *
 * MODIFICATION NOTES (TR)
 * - Rol alanı (USER/ADMIN), avatar URL, locale: ileride eklenebilir.
 * - Gerçek backend: Prisma User modeli ile hizalanacak alanlar burada genişletilir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
