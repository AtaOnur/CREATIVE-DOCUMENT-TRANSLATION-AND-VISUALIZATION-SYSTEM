import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { AppProviders } from "@/components/providers/app-providers";
import "./globals.css";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "Creative Document Translation & Visualisation",
  description: "Graduation project — foundation step",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="tr">
      <body className={`${inter.className} min-h-screen antialiased`}>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Next.js App Router kök düzenini tanımlar; global stil ve sağlayıcıları bağlar.
 *
 * [TR] Neden gerekli
 * Tüm sayfalar ortak HTML gövdesi, font ve veri sağlayıcılarından geçer.
 *
 * [TR] Sistem içinde nerede kullanılır
 * `/app` altındaki her route bu layout’u kullanır.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: gerçek auth sağlayıcısı, tema toggle, locale alt segmentleri.
 * - Nasıl çözülür: İlgili Provider bileşenleri `AppProviders` içine eklenir.
 * - Etkilenen dosyalar: Bu dosya, components/providers/*, i18n yapılandırması.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
