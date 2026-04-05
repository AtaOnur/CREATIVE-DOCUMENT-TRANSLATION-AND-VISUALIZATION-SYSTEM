/** @type {import('next').NextConfig} */
const nextConfig = {};

export default nextConfig;

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Next.js uygulamasının derleme ve çalışma zamanı ayarlarını tutar.
 *
 * [TR] Neden gerekli
 * Projeyi `next dev` / `next build` ile çalıştırırken framework bu dosyayı okur.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Kök dizinde; CI ve geliştirme ortamı her build’de dolaylı olarak kullanır.
 *
 * MODIFICATION NOTES (TR)
 * - HTTPS (geliştirme): `npm run dev` → `next dev --experimental-https` (package.json); adres https://localhost:3000, tarayıcı self-signed uyarısı verebilir.
 * - Olası değişiklik: görseller için remote pattern, i18n, environment değişkeni whitelist.
 * - Nasıl çözülür: next.config içinde images / env / experimental alanlarına ekleme yapılır.
 * - Etkilenen dosyalar: Bu dosya; gerekirse Dockerfile veya deploy betikleri.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
