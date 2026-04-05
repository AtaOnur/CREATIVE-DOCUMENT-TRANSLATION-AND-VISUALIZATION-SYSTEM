import type { DocumentStatusValue } from "@/lib/types/enums";

export type MockDocument = {
  id: string;
  ownerEmail: string;
  title: string;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
  status: DocumentStatusValue;
  createdAt: string;
  updatedAt: string;
  storagePath: string;
};

export type ActivityEntry = {
  id: string;
  at: string;
  message: string;
};

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Mock belge ve aktivite kayıtları için paylaşılan TypeScript tipleri.
 * [TR] Neden gerekli: Panoda ve belge listesinde tek veri sözleşmesi.
 * [TR] Sistem içinde: mock-store, server actions, React bileşenleri.
 *
 * MODIFICATION NOTES (TR)
 * - Prisma Document ile hizalama: alan adları bilinçli olarak yakın tutuldu.
 * - Çalışma alanı PDF URL’si: şimdilik getWorkspacePdfUrl(doc); istenirse MockDocument’a publicPdfUrl alanı eklenebilir.
 * - Sayfa sayısı, özet metin: sonraki sprint alanları eklenebilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
