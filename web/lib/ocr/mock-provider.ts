import type { OcrProvider } from "./provider";
import type { OcrRegionRequest, OcrRegionResponse } from "./types";

const MOCK_DELAY_MS = 900;

function clamp01(n: number) {
  return Math.min(1, Math.max(0, n));
}

/**
 * [TR] Açıklama: Ağ gecikmesini taklit eder; metin, sayfa ve bölge boyutundan türetilmiş deterministik sahte çıktı üretir.
 * Gerçek OCR yoktur — yalnızca akış ve UI için.
 */
export class MockOcrProvider implements OcrProvider {
  async extractTextFromPdfRegion(req: OcrRegionRequest): Promise<OcrRegionResponse> {
    await new Promise((r) => setTimeout(r, MOCK_DELAY_MS));

    const { pageNumber, xNorm, yNorm, widthNorm, heightNorm } = req.region;
    const area = clamp01(widthNorm) * clamp01(heightNorm);
    const shortHint =
      area < 0.02
        ? "(Mock: seçim çok küçük; gerçek OCR’da okunabilirlik düşebilir.)"
        : "(Mock: bölge boyutu uygun görünüyor.)";

    const text =
      `[Mock OCR — PDF bölgesi, sayfa ${pageNumber}]\n\n` +
      `Normalize kutu: x=${xNorm.toFixed(3)}, y=${yNorm.toFixed(3)}, w=${widthNorm.toFixed(3)}, h=${heightNorm.toFixed(3)}.\n` +
      `${shortHint}\n\n` +
      `Bu metin düzenlenebilir; sonraki AI adımına göndermeden önce elle düzeltme yapmanız önerilir.`;

    return { text, confidence: 0.85 };
  }
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: OCR sağlayıcısının sahte uygulaması; yükleme ve gecikme simülasyonu.
 * [TR] Neden gerekli: Backend olmadan uçtan uca kullanıcı hikâyesi.
 * [TR] Sistem içinde: lib/ocr/index.ts üzerinden DocumentWorkspace.
 *
 * MODIFICATION NOTES (TR)
 * - Gerçek motor: Aynı OcrProvider sınıf yapısında TesseractWorker veya fetch('/api/ocr').
 * - PDF’den bitmap: pdf.js getOperatorList / render + canvas; sonra motor girişi.
 * - Güven değeri: Motor çıktısından confidence doldurulur; mock’ta sabit.
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Yüksek (gerçek OCR pipeline).
 * -----------------------------------------------------------------------------
 */
