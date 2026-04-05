export * from "./enums";
export * from "./core";

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Çekirdek TypeScript tiplerinin barrel export’udur.
 *
 * [TR] Neden gerekli
 * `import { NormalizedRect } from "@/lib/types"` gibi kısa ve tutarlı import yolu sağlar.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Tüm katmanlar; özellikle sunum + uygulama arası veri sözleşmeleri.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: API yanıt tipleri (DTO) için ayrı dosya.
 * - Nasıl çözülür: `lib/types/api.ts` eklenir ve buradan export edilir.
 * - Etkilenen dosyalar: Bu dosya, yeni tip dosyası.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
