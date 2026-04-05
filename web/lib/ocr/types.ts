/**
 * [TR] Açıklama: OCR katmanında kullanılan tutarlı veri şekilleri.
 * PDF üzerindeki dikdörtgen bölge, Prisma `RegionSelection` ile aynı mantıkta normalize (0–1).
 */
export type NormalizedPdfRegion = {
  pageNumber: number;
  xNorm: number;
  yNorm: number;
  widthNorm: number;
  heightNorm: number;
};

export type OcrRegionRequest = {
  /** Görüntülenebilir PDF URL’si (mock’ta `/sample.pdf`; üretimde imzalı URL olabilir). */
  pdfUrl: string;
  region: NormalizedPdfRegion;
};

export type OcrRegionResponse = {
  text: string;
  /** İleride güven skoru veya motor sürümü için genişletilebilir. */
  confidence?: number;
};

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: OCR isteği/cevabı ve normalize bölge tipi.
 * [TR] Neden gerekli: Sağlayıcı arayüzü ve UI’nin aynı sözleşmeyi kullanması.
 * [TR] Sistem içinde: provider arayüzü, mock sağlayıcı, belge çalışma alanı.
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu bölge: NormalizedPdfRegion[] ve birleşik metin politikası.
 * - Tam sayfa OCR: region tüm sayfayı kaplayacak şekilde üretilebilir (ayrı iş kuralı).
 * - Güven/kutu çizimi: confidence ile satır altı vurgulama (PDF üzerine overlay).
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Kolay–orta (tip genişletmeleri).
 * -----------------------------------------------------------------------------
 */
