import type { OcrProvider } from "./provider";
import { MockOcrProvider } from "./mock-provider";

let _singleton: OcrProvider | null = null;

/** [TR] Şimdilik mock; ileride env ile Tesseract/API seçilebilir. */
export function getOcrProvider(): OcrProvider {
  if (!_singleton) _singleton = new MockOcrProvider();
  return _singleton;
}

export type { OcrProvider } from "./provider";
export type { NormalizedPdfRegion, OcrRegionRequest, OcrRegionResponse } from "./types";

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: OCR sağlayıcı fabrikası — tek nesne (mock) döner.
 * [TR] Neden gerekli: Bileşenler doğrudan sınıf örneklemez; değişim tek yerde kalır.
 * [TR] Sistem içinde: DocumentWorkspace.
 *
 * MODIFICATION NOTES (TR)
 * - process.env.NEXT_PUBLIC_OCR_DRIVER: "mock" | "api" gibi bayrak.
 * - Sunucu taraflı OCR: getOcrProvider yerine server action ile çağrı.
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
