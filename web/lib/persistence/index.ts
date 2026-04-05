export { db } from "./db";

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Kalıcılık katmanının dışa açılan tek giriş noktasını basit tutar.
 *
 * [TR] Neden gerekli
 * İleride repository dosyaları eklendiğinde import yolları sade kalır.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Uygulama katmanı servisleri `lib/persistence` üzerinden erişebilir.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: repository modüllerinin barrel export’u.
 * - Nasıl çözülür: export satırları eklenir.
 * - Etkilenen dosyalar: Bu dosya, yeni repository dosyaları.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
