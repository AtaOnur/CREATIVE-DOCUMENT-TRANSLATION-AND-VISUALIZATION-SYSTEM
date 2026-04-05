import { PrismaClient } from "@prisma/client";

const globalForPrisma = globalThis as unknown as { prisma: PrismaClient | undefined };

export const db = globalForPrisma.prisma ?? new PrismaClient();

if (process.env.NODE_ENV !== "production") globalForPrisma.prisma = db;

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Tek bir paylaşılan PrismaClient örneği oluşturur (geliştirmede bağlantı sızıntısını önlemek için).
 *
 * [TR] Neden gerekli
 * Kalıcılık katmanında tekrar tekrar `new PrismaClient()` açmak verimsiz ve hataya açıktır.
 *
 * [TR] Sistem içinde nerede kullanılır
 * Repository fonksiyonları, Server Actions ve route handler’lar `db` import ederek kullanır.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: log seviyesi, read replica, bağlantı havuzu ayarları.
 * - Nasıl çözülür: PrismaClient yapılandırması veya DATABASE_URL parametreleri güncellenir.
 * - Etkilenen dosyalar: Bu dosya, ortam değişkenleri, deploy dokümantasyonu.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
