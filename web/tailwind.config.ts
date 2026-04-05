import type { Config } from "tailwindcss";

const config: Config = {
  darkMode: ["class"],
  content: [
    "./pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
    "./lib/presentation/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {},
  },
  plugins: [require("tailwindcss-animate")],
};

export default config;

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Tailwind CSS’in taranacak dosya yollarını ve tema eklentilerini tanımlar.
 *
 * [TR] Neden gerekli
 * shadcn/ui ve utility sınıfları için standart Tailwind yapılandırması gerekir.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Tüm React bileşenleri; build sırasında kullanılan sınıflar buradan üretilir.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: renk paleti, font ailesi, container genişlikleri, dark mode stratejisi.
 * - Nasıl çözülür: theme.extend altına token’lar eklenir; shadcn tema ile uyum korunur.
 * - Etkilenen dosyalar: Bu dosya, globals.css (CSS değişkenleri), UI bileşenleri.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
