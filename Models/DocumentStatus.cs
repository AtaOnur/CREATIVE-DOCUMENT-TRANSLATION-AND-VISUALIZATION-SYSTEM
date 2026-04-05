namespace pdf_bitirme.Models;

/*
 * [TR] Bu dosya ne işe yarar: Belge yaşam döngüsü durumları — pano ve filtrelerde ortak enum.
 * [TR] Neden gerekli: OCR/AI henüz yok; yükleme sonrası sıralı ilerleme savunmada anlatılabilir.
 * [TR] İlgili: Document varlığı, DocumentsController filtreleri
 *
 * MODIFICATION NOTES (TR)
 * - DOCX dönüştürme aşaması için ara durum eklenebilir.
 * - OCR yalnızca PDF seçili bölge içindir; genel görüntü OCR future work.
 * - Zorluk: Kolay.
 */
public enum DocumentStatus
{
    UPLOADED,
    PROCESSING,
    OCR_READY,
    AI_READY,
    COMPLETED,
    FAILED,
}
