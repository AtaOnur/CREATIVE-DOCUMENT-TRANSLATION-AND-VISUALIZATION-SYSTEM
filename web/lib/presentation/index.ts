/**
 * Presentation layer: routes and UI live under `app/` and `components/`.
 * Use this folder only for view-specific helpers (for example formatting hooks)
 * to avoid mixing them with domain services in `lib/application`.
 */

export {};

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Sunum katmanının “görünür” kısmının nerede olduğunu kod içinde işaret eder.
 *
 * [TR] Neden gerekli
 * Savunmada klasörleri gösterirken `app/` + `components/` + bu dosya ile hikâye tutarlı kalır.
 *
 * [TR] Sistem içinde nerede kullanılır
 * İleride view-model veya sunum hook’ları buraya eklenebilir; şimdilik yönlendirme amaçlıdır.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: feature-based alt klasörler (`features/documents`).
 * - Nasıl çözülür: Yapı büyürse `components/documents/*` veya `app/(app)/documents/*` rehberi yazılır.
 * - Etkilenen dosyalar: Bu dosya, import yolları, tsconfig paths.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
