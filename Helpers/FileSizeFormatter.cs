namespace pdf_bitirme.Helpers;

/*
 * [TR] Bu dosya ne işe yarar: Bayt cinsinden dosya boyutunu okunur metne çevirir (B, KB, MB).
 * [TR] İlgili: Dashboard, Documents görünümleri
 *
 * MODIFICATION NOTES (TR)
 * - GB desteği büyük arşivler için.
 * - Yerelleştirilmiş birim dizesi (kultur).
 * - Zorluk: Kolay.
 */
public static class FileSizeFormatter
{
    public static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024):0.#} MB";
    }
}
