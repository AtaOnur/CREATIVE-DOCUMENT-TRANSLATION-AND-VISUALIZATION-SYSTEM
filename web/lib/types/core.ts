/**
 * Small, human-readable shapes shared across layers (not full DB rows).
 */

export type NormalizedRect = {
  xNorm: number;
  yNorm: number;
  widthNorm: number;
  heightNorm: number;
};

export type AuditMetadata = Record<string, unknown>;

export type VisualizationMeta = {
  diagramType?: string;
  notes?: string;
  [key: string]: unknown;
};

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Bölge seçimi dikdörtgeni ve denetim/görselleştirme metadata’sı için ortak tipler verir.
 *
 * [TR] Neden gerekli
 * Sunumda “normalize koordinat” ve JSON alanlarının anlamı tek yerde toplanır.
 *
 * [TR] Sistem içinde nerede kullanılır
 * OCR girişi, API DTO’ları, grafik üretim meta verisi.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: piksel bazlı rect + DPI bilgisi; çoklu bölge seçimi.
 * - Nasıl çözülür: Yeni alanlar eklenir; veritabanında `RegionSelection` veya ilişki tablosu.
 * - Etkilenen dosyalar: Bu dosya, prisma/schema.prisma, OCR adaptörü.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
