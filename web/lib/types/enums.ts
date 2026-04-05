/**
 * İzin verilen string değerler — Zod / formlar için.
 * SQL Server şemasında Prisma `enum` kullanılmaz; aynı değerler DB’de `String` kolonlarda tutulur.
 */

export const documentStatuses = [
  "UPLOADED",
  "PROCESSING",
  "OCR_READY",
  "AI_READY",
  "COMPLETED",
  "FAILED",
] as const;
export type DocumentStatusValue = (typeof documentStatuses)[number];

export const operationTypes = [
  "TRANSLATE",
  "SUMMARIZE",
  "CREATIVE_WRITE",
  "REWRITE",
  "VISUALIZE",
] as const;
export type OperationTypeValue = (typeof operationTypes)[number];

export const styleTypes = ["NEUTRAL", "FORMAL", "CREATIVE", "ACADEMIC"] as const;
export type StyleTypeValue = (typeof styleTypes)[number];

export const roles = ["USER", "ADMIN"] as const;
export type RoleValue = (typeof roles)[number];

export const aiResultStatuses = ["PENDING", "COMPLETED", "FAILED"] as const;
export type AiResultStatusValue = (typeof aiResultStatuses)[number];

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Belge yaşam döngüsü dahil sabit listeler (PDF iş hattı durumları).
 * [TR] Neden gerekli: İstemci + sunucu + mock store aynı durum isimlerini kullanır.
 * [TR] Sistem içinde: belge listesi filtreleri, rozetler, Zod şemaları, ileride Prisma.
 *
 * MODIFICATION NOTES (TR)
 * - Arşiv / taslak durumu: listeye string eklenir; migrasyon ile DB değerleri güncellenir.
 * - Genel görüntü OCR: ayrı ürün özelliği olarak durum makinesi farklı olur; kapsam dışı notu korunur.
 * - Etkilenen dosyalar: prisma/schema.prisma yorumu, seed, mock-store örnekleri.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
