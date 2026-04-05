/**
 * External Integration — AI operations (placeholder).
 * OperationType values: TRANSLATE, SUMMARIZE, CREATIVE_WRITE, REWRITE, VISUALIZE
 */

import type { OperationTypeValue, StyleTypeValue } from "@/lib/types/enums";

export type AiOperationInput = {
  operation: OperationTypeValue;
  style?: StyleTypeValue;
  sourceText: string;
};

export type AiOperationOutput = {
  text?: string;
  meta?: Record<string, unknown>;
};

export async function runAiOperation(
  _input: AiOperationInput
): Promise<AiOperationOutput> {
  void _input;
  throw new Error(
    "AI integration not implemented in foundation step. Connect provider SDK here later."
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Çeviri, özet, yaratıcı yazım, yeniden yazma ve görselleştirme için AI çağrısı yer tutucusudur.
 *
 * [TR] Neden gerekli
 * Sunumda “dış entegrasyon katmanı” net görülür; gerçek API anahtarı ve SDK sonra eklenir.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Uygulama katmanı işlem tamamlandığında `AiResult` kaydı ile eşleştirilecek.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: OpenAI / Azure / yerel model; kota ve içerik filtreleri.
 * - Nasıl çözülür: `runAiOperation` implement edilir; hata durumları status alanında `"FAILED"` ile kaydedilir.
 * - Etkilenen dosyalar: Bu dosya, ortam değişkenleri, audit log yazımları.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
