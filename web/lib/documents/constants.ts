/** Yalnızca PDF; genel görüntü yükleme / görüntüden metin (OCR) kapsam dışı — ileride çalışma olarak not edilebilir. */
export const ALLOWED_UPLOAD_MIME = "application/pdf";
export const ALLOWED_UPLOAD_EXTENSION = ".pdf";
export const MAX_UPLOAD_BYTES = 20 * 1024 * 1024; // 20 MB

export const PDF_MAGIC = "%PDF";

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: PDF yükleme kuralları (MIME, uzantı, boyut üst sınır, sihir bayt).
 * [TR] Neden gerekli: İstemci ve sunucuda aynı sabitlerle doğrulama yapılır.
 * [TR] Sistem içinde: upload eylemi ve istemci validasyonu.
 *
 * MODIFICATION NOTES (TR)
 * - DOCX desteği: ayrı MIME listesi ve ayrı işleme hattı (dönüştürücü).
 * - Bulut depolama: boyut limiti sağlayıcıya göre artırılabilir; parçalı yükleme.
 * - Çoklu dosya: dizi doğrulama ve kuyruk.
 * - Genel görüntü OCR: bu projede yok; gelecek çalışma olarak dokümante edilir.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
