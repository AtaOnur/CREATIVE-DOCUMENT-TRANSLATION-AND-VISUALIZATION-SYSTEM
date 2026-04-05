export { runPdfRegionOcr } from "./ocr/pdf-region-ocr.placeholder";
export { runAiOperation } from "./ai/creative-operations.placeholder";

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Dış entegrasyon modüllerini tek yerden dışa verir.
 *
 * [TR] Neden gerekli
 * Uygulama katmanının doğrudan alt klasörlere yapışmasını azaltır.
 *
 * [TR] Sistem içinde nerede kullanılır
 * İş akışı servisleri ve ilerideki API route’lar bu barrel’ı import edebilir.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: e-posta, ödeme, dosya depolama (S3) adaptörleri.
 * - Nasıl çözülür: yeni dosya + export satırı.
 * - Etkilenen dosyalar: Bu dosya, ilgili adapter.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
