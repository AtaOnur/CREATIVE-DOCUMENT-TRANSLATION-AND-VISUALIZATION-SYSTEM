import { clsx } from "clsx";
import type { ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Tailwind sınıf birleştirme yardımcısı (`cn`) sağlar; shadcn/ui ile aynı desen.
 *
 * [TR] Neden gerekli
 * Koşullu stillerde çakışan utility sınıflarını birleştirir.
 *
 * [TR] Sistem içinde nerede kullanılır
 * UI bileşenleri (`components/ui/*`).
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: tema token’ları ile entegre yardımcılar.
 * - Nasıl çözülür: İhtiyaç halinde ek fonksiyon; `cn` genelde aynı kalır.
 * - Etkilenen dosyalar: Bu dosya, bileşen stil kodu.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
