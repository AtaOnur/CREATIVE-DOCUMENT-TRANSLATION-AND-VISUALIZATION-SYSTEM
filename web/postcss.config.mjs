/** @type {import('postcss-load-config').Config} */
const config = {
  plugins: {
    tailwindcss: {},
  },
};

export default config;

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * CSS işleme zincirinde Tailwind’i PostCSS üzerinden bağlar.
 *
 * [TR] Neden gerekli
 * Next.js, global stiller ve Tailwind sınıflarının üretilmesi için PostCSS kullanır.
 *
 * [TR] Sistem içinde nerede kullanılır
 * `app/globals.css` derlenirken; geliştirme ve production build sırasında.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: autoprefixer ekleme, CSS minify plugin’leri.
 * - Nasıl çözülür: plugins nesnesine ilgili paket eklenir.
 * - Etkilenen dosyalar: Bu dosya, package.json bağımlılıkları, globals.css çıktısı.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
