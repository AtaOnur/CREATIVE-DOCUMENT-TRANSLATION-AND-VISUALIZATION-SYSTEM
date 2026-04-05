import type { MockDocument } from "./types";

/**
 * [TR] Açıklama: Çalışma alanında PDF.js’e verilecek genel URL.
 * Mock depoda dosya gerçekten disk’e yazılmadığı için şimdilik ortak demo PDF kullanılır.
 */
export function getWorkspacePdfUrl(_doc: MockDocument): string {
  void _doc;
  return "/sample.pdf";
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Belge kaydından görüntüleyici URL’si üretir.
 * [TR] Neden gerekli: Yükleme stub’ı ile viewer’ın tek yerde birleşmesi.
 * [TR] Sistem içinde: /app/documents/[id] ve DocumentWorkspace.
 *
 * MODIFICATION NOTES (TR)
 * - S3/Blob: storagePath → kısa ömürlü imzalı GET URL.
 * - Yükleme sonrası: public klasör veya Object Storage’a yazım + kalıcı key.
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Orta (güvenli URL ve önbellek).
 * -----------------------------------------------------------------------------
 */
