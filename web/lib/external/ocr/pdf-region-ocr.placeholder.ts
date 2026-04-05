/**
 * External Integration — PDF region OCR (placeholder).
 *
 * Scope: extract text only from a user-selected rectangle on a PDF page.
 * Out of scope: general image-to-text / arbitrary image OCR (future work).
 */

export type PdfRegionOcrInput = {
  documentStoragePath: string;
  pageNumber: number;
  rectangle: { xNorm: number; yNorm: number; widthNorm: number; heightNorm: number };
};

export type PdfRegionOcrOutput = {
  text: string;
  confidence?: number;
  engineVersion: string;
};

export async function runPdfRegionOcr(
  _input: PdfRegionOcrInput
): Promise<PdfRegionOcrOutput> {
  void _input;
  throw new Error(
    "PDF region OCR not implemented in foundation step. Wire Tesseract/cloud OCR here later."
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * PDF sayfasındaki seçili bölge için OCR arayüzünü (imza ve tipler) yer tutucu olarak tanımlar.
 *
 * [TR] Neden gerekli
 * Dış sistem entegrasyonu tek yerde toplanır; genel görüntü OCR kapsam dışı tutulur.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Uygulama katmanı OCR ihtiyacında bu modülü çağıracak; sonuç `OcrResult` olarak kaydedilir.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: Tesseract, AWS Textract, Google Vision (sadece PDF çıkarımı pipeline’ı ile).
 * - Nasıl çözülür: `runPdfRegionOcr` içi doldurulur; gerekli SDK ve env değişkenleri eklenir.
 * - Etkilenen dosyalar: Bu dosya, `lib/persistence` yazımları, iş akışı servisi.
 * - Zorluk: Orta–yüksek (PDF render + koordinat dönüşümü).
 * -----------------------------------------------------------------------------
 */
