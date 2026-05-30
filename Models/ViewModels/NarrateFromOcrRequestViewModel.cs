namespace pdf_bitirme.Models.ViewModels;

/*
 * [TR] Bu dosya ne işe yarar: OCR metninin Gemini TTS ile seslendirilebilmesi için JSON gövdesi modeli ([FromBody]).
 * [TR] Neden gerekli: "Metni çıkar" sonrası textarea’daki düz yazı gönderilir; OCR motorundan bağımsızdır.
 * [TR] İlgili: DocumentsController.NarrateOcrSpeech, pdf-workspace.mjs gövdesi { text }
 *
 * MODIFICATION NOTES (TR)
 * - İleride VoiceId seçimi kullanıcıya bırakılırsa ek alan (opsiyonel string) konabilir.
 * - DocumentId ile üretilen ses AiResult kaydı olarak notebook'a otomatik düşer.
 * - Zorluk: Kolay.
 */

/// <summary>OCR alanından okunan düz metinle TTS tetiklemek için istek gövdesi.</summary>
public class NarrateFromOcrRequestViewModel
{
    /// <summary>Ses kaydının bağlanacağı belge; notebook/history ilişkilendirmesi için kullanılır.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// OCR çıktısı veya kullanıcı düzeltmesi; sunucuda normalize edilerek Gemini TTS’ye iletilir.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Seslendirme dili. Kaynak metinden farklıysa önce bu dile çevrilir, sonra TTS uygulanır.
    /// </summary>
    public string TargetLanguage { get; set; } = "English";
}
