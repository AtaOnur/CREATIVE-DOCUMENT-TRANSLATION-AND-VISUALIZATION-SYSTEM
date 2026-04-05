export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function formatDateTime(iso: string): string {
  try {
    return new Intl.DateTimeFormat("tr-TR", {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Dosya boyutu ve tarih gösterimi için küçük yardımcılar.
 * [TR] Neden gerekli: Belge tablosunda okunabilir çıktı.
 * [TR] Sistem içinde: belge listesi ve pano.
 *
 * MODIFICATION NOTES (TR)
 * - Uluslararası: locale parametresi veya kullanıcı tercihi.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
