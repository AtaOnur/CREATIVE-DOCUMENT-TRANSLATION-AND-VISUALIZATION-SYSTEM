/**
 * Foundation placeholder: orchestrates document + region + OCR + AI steps.
 * Real logic (PDF parsing, OCR, LLM calls) will plug in via persistence + external layers.
 */

export type DocumentWorkflowStage =
  | "uploaded"
  | "region_selected"
  | "ocr_ready"
  | "ai_ready";

export function describeWorkflowStage(stage: DocumentWorkflowStage): string {
  const labels: Record<DocumentWorkflowStage, string> = {
    uploaded: "PDF stored; awaiting region selection.",
    region_selected: "User boxed a region on a page (coordinates normalized).",
    ocr_ready: "Text extracted from PDF region only (not arbitrary images).",
    ai_ready: "Translation / summary / creative / visualize result recorded.",
  };
  return labels[stage];
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * İş akışı aşamalarını savunmada anlatım kolaylığı için metin olarak özetler (henüz gerçek iş mantığı yok).
 *
 * [TR] Neden gerekli
 * Sunum ve SDD için “pipeline” kavramını kodda görünür kılar; ileride gerçek servis buraya taşınır.
 *
 * [TR] Sistem içinde nerede kullanılır
 * İleride Server Actions veya API route’ları bu katmandaki fonksiyonları çağıracak.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: asenkron kuyruk, adım bazlı durum makinesi, hata geri alma.
 * - Nasıl çözülür: Bu dosya genişletilir veya `services/documentWorkflow.ts` gibi modüllere bölünür.
 * - Etkilenen dosyalar: Kalıcı modeller (DocumentStatus), dış entegrasyon adaptörleri.
 * - Zorluk: Orta–yüksek.
 * -----------------------------------------------------------------------------
 */
