import type { OcrRegionRequest, OcrRegionResponse } from "./types";

/**
 * [TR] Açıklama: Gelecekte Tesseract, bulut API veya sunucu tarafı worker bağlamak için tek giriş noktası.
 * Şimdilik yalnızca PDF içinde kullanıcı seçimi bölgesi desteklenir.
 */
export interface OcrProvider {
  extractTextFromPdfRegion(req: OcrRegionRequest): Promise<OcrRegionResponse>;
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: OCR motorlarını değiştirilebilir kılan ince arayüz.
 * [TR] Neden gerekli: Mock’tan gerçek implementasyona geçişte çağrı kodunun sabit kalması.
 * [TR] Sistem içinde: mock-provider, DocumentWorkspace (istemci tarafı çağrı).
 *
 * MODIFICATION NOTES (TR)
 * - Yerel Tesseract: Worker + görüntü kırpma; bu arayüz sunucuya taşınabilir.
 * - API anahtarı: Sunucu eylemi üzerinden proxy; arayüz istemcide kalmaz.
 * - Streaming OCR: AsyncIterable veya WebSocket ile parça parça metin.
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Orta (gerçek motor entegrasyonu).
 * -----------------------------------------------------------------------------
 */
