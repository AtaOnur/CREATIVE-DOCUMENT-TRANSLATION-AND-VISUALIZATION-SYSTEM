"""
paddle_ocr.py — PaddleOCR Yardımcı Script (3.x uyumlu, dil seçimi yok)
=======================================================================
[TR] Bu script ne işe yarar:
     PaddleOCR kütüphanesini kullanarak verilen görüntü dosyasından metin çıkarır.
     C# tarafındaki PaddleOcrService bu scripti subprocess olarak çağırır ve
     stdout'tan düz metin alır.

[TR] Dil seçimi hakkında:
     PaddleOCR'ın dil modeli seçimi kaldırıldı. Script artık dil parametresi almaz.
     Varsayılan model (en/Latin) Latin alfabesi kullanan tüm dilleri (Türkçe dahil) okur.
     Otomatik algılama bu şekilde sağlanır; kullanıcıya seçim yaptırılmaz.

[TR] Kullanım:
     python paddle_ocr.py <resim_yolu>

[TR] Neden orientation-detection kapatıldı:
     PP-LCNet_x1_0_doc_ori modeli PaddleOCR 3.x'te her çalıştırmada indirmeye çalışır.
     Standart PDF sayfaları zaten dik (0°) olduğundan bu model gerekmez.
     Kapatmak hem hızlandırır hem model indirme hatasını engeller.

MODIFICATION NOTES (TR):
- Dil desteği geri eklenecekse sys.argv[2] eklenir ve LANG_MAP tablosu kullanılır.
- GPU desteği: use_gpu=True yapılabilir (CUDA gerekir).
- Döndürülmüş belgeler için orientation detection açılabilir; model indirme sorununu
  çözdükten sonra use_doc_orientation_classify=True eklenir.
- Zorluk: Kolay.
"""

import sys
import os
import warnings

# ── Ortam ayarları ─────────────────────────────────────────────────────────────
# [TR] Model önbelleğini boşluk/Türkçe karakter içermeyen güvenli bir yola yönlendir.
# Varsayılan (~\.paddlex) OneDrive tarafından senkronize edilirse WinError 4392 oluşur;
# C:\paddlex_models bu sorunu önler. Dizin yoksa oluşturulur.
_SAFE_CACHE = r"C:\paddlex_models"
os.makedirs(_SAFE_CACHE, exist_ok=True)
os.environ["PADDLE_PDX_CACHE_HOME"] = _SAFE_CACHE

# [TR] Model kaynaklarına bağlantı kontrolünü devre dışı bırak (yavaşlatma önleme)
os.environ["PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK"] = "True"
os.environ["PADDLE_PDX_DISABLE_TELEMETRY"] = "True"
# [TR] PaddleOCR/PaddleX log seviyesini bastır
os.environ["PADDLEX_LOG_LEVEL"] = "ERROR"
os.environ["FLAGS_call_stack_level"] = "0"

# [TR] Python uyarılarını bastır (deprecated parametre mesajları vb.)
warnings.filterwarnings("ignore")

MIN_CONFIDENCE = 0.4


def parse_result(result):
    """
    [TR] PaddleOCR 2.x ve 3.x sonuç formatlarını birlikte işler.
    """
    lines = []
    if not result:
        return lines

    for page in result:
        if not page:
            continue
        # 3.x nesne tabanlı format
        if hasattr(page, "rec_text"):
            score = float(getattr(page, "rec_score", 1.0))
            if score >= MIN_CONFIDENCE:
                lines.append(page.rec_text)
            continue
        if not isinstance(page, (list, tuple)):
            continue
        for item in page:
            if not item:
                continue
            if hasattr(item, "rec_text"):
                score = float(getattr(item, "rec_score", 1.0))
                if score >= MIN_CONFIDENCE:
                    lines.append(item.rec_text)
                continue
            # [bbox, [text, confidence]] formatı (2.x)
            if isinstance(item, (list, tuple)) and len(item) >= 2:
                text_part = item[1]
                if isinstance(text_part, (list, tuple)) and len(text_part) >= 2:
                    text, conf = text_part[0], text_part[1]
                    try:
                        conf = float(conf)
                    except (ValueError, TypeError):
                        conf = 1.0
                    if isinstance(text, str) and conf >= MIN_CONFIDENCE:
                        lines.append(text)
                elif isinstance(text_part, str):
                    lines.append(text_part)
    return lines


def try_init_ocr(configs):
    """[TR] Sırasıyla farklı PaddleOCR konfigürasyonlarını dener, ilk başarılıyı döner."""
    from paddleocr import PaddleOCR
    last_err = None
    for cfg in configs:
        try:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                ocr = PaddleOCR(**cfg)
            return ocr
        except Exception as e:
            last_err = e
    raise RuntimeError(f"PaddleOCR baslatma hatasi: {last_err}")


def try_run_ocr(ocr, image_path):
    """[TR] Farklı .ocr() çağrı stillerini dener (3.x ve 2.x uyumluluğu için)."""
    last_err = None
    for kw in [{}, {"cls": False}]:
        try:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                return ocr.ocr(image_path, **kw)
        except Exception as e:
            last_err = e
    raise RuntimeError(f"OCR calistirma hatasi: {last_err}")


def main():
    if len(sys.argv) < 2:
        print("Kullanim: python paddle_ocr.py <resim_yolu>", file=sys.stderr)
        sys.exit(1)

    image_path = sys.argv[1]

    if not os.path.isfile(image_path):
        print(f"Hata: Dosya bulunamadi: {image_path}", file=sys.stderr)
        sys.exit(1)

    try:
        from paddleocr import PaddleOCR  # noqa: F401
    except ImportError:
        print("paddleocr kurulu degil. Kurmak icin: pip install paddleocr paddlepaddle", file=sys.stderr)
        sys.exit(2)

    # [TR] PaddleOCR 3.x geçerli parametreleri inspect ile belirlendi.
    # Yalnızca det + rec modelleri çalışır; opsiyonel modeller kapatılır:
    #   use_doc_orientation_classify=False  → PP-LCNet_x1_0_doc_ori indirilmez
    #   use_doc_unwarping=False             → unwarping modeli indirilmez
    #   use_textline_orientation=False      → PP-LCNet_x1_0_textline_ori indirilmez
    # Konfigürasyonlar sırayla denenir; ilk başarılı olan kullanılır.
    MINIMAL = {
        "use_doc_orientation_classify": False,
        "use_doc_unwarping": False,
        "use_textline_orientation": False,
        "lang": "en",
    }
    init_configs = [
        MINIMAL,
        {"use_doc_orientation_classify": False, "use_doc_unwarping": False, "use_textline_orientation": False},
        {"lang": "en"},
        {},
    ]

    try:
        ocr = try_init_ocr(init_configs)
    except Exception as e:
        print(str(e), file=sys.stderr)
        sys.exit(1)

    try:
        result = try_run_ocr(ocr, image_path)
    except Exception as e:
        print(str(e), file=sys.stderr)
        sys.exit(1)

    lines = parse_result(result)
    print("\n".join(lines))


if __name__ == "__main__":
    main()
