export {
  describeWorkflowStage,
  type DocumentWorkflowStage,
} from "./document-workflow.placeholder";

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Uygulama katmanı (iş kuralları / use-case) için ortak dışa aktarım noktasıdır.
 *
 * [TR] Neden gerekli
 * Sunum ve kod gezintisinde “servisler nerede?” sorusuna tek import ile cevap verir.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Route handler’lar, Server Actions ve testler `lib/application` altından çağırır.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: `services/` altında dosya başına use-case ; buradan export.
 * - Nasıl çözülür: Yeni servis dosyası + export satırı; overly generic “manager” sınıflarından kaçının.
 * - Etkilenen dosyalar: Bu dosya, ilgili servis, kalıcılık adaptörleri.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
