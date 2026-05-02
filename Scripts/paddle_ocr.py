"""
paddle_ocr.py — PaddleOCR Yardımcı Script (2.x + 3.x uyumlu)
=======================================================================
[TR] Bu script ne işe yarar:
     PaddleOCR kütüphanesini kullanarak verilen görüntü dosyasından metin çıkarır.
     C# tarafındaki PaddleOcrService bu scripti subprocess olarak çağırır ve
     stdout'tan düz metin alır.

[TR] Neden PP-OCRv5_mobile_det + enable_mkldnn=False?
     PaddleOCR 3.x varsayılanı PP-OCRv5_server_det (ağır) + oneDNN/MKLDNN'dir.
     Windows'ta Paddle'ın yeni PIR executor'ı bu modeldeki bazı attribute'ları
     (pir::ArrayAttribute<pir::DoubleAttribute>) oneDNN backend'e dönüştüremez ve
     şu hatayı verir:
        NotImplementedError: (Unimplemented) ConvertPirAttribute2RuntimeAttribute
        not support [pir::ArrayAttribute<pir::DoubleAttribute>]
     Mobile det modeli bu attribute'u kullanmaz → sorun yaşanmaz.
     Ayrıca güvenlik için oneDNN tamamen kapatılır (FLAGS_use_mkldnn=0 +
     enable_mkldnn=False kwarg'ı). Performans kaybı ihmal edilebilir; Mobile
     det zaten çok hızlı.

[TR] PaddleOCR 3.x API değişikliği:
     - 2.x:  ocr.ocr(img_path, cls=True) → [[bbox, (text, conf)], ...]
     - 3.x:  ocr.predict(img_path)       → [OCRResult(...)]  (dict-benzeri)
             OCRResult.rec_texts   : List[str]
             OCRResult.rec_scores  : List[float]
     3.x'te .ocr() bir shim'dir; içeride .predict() çağırır ve 'cls' gibi
     eski kwarg'ları kabul etmez → "unexpected keyword argument 'cls'" hatası.

[TR] Kullanım:
     python paddle_ocr.py <resim_yolu>

[TR] Dil:
     Varsayılan model (en/Latin) Latin alfabesini kullanan tüm dilleri
     (Türkçe dahil) okur. Dil parametresi alınmaz.

MODIFICATION NOTES (TR):
- Server det modelini zorla deneyeceksen init_configs listesine server_det
  varyantını geri ekleyebilirsin ama Windows PIR bug'ı tekrar tetiklenir.
- GPU desteği için use_gpu=True (CUDA gerekir).
- Döndürülmüş belgeler için use_doc_orientation_classify=True açılır.
- Zorluk: Orta (Paddle 3.x iç API'si değişken).
"""

import sys
import os
import warnings
import traceback

# ── Ortam ayarları (paddleocr import'undan ÖNCE set edilmeli) ──────────────────
# [TR] Model önbelleğini boşluk/Türkçe karakter içermeyen güvenli bir yola yönlendir.
_SAFE_CACHE = r"C:\paddlex_models"
os.makedirs(_SAFE_CACHE, exist_ok=True)
os.environ["PADDLE_PDX_CACHE_HOME"] = _SAFE_CACHE

# [TR] Model kaynaklarına bağlantı kontrolünü devre dışı bırak (yavaşlatma önleme)
os.environ["PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK"] = "True"
os.environ["PADDLE_PDX_DISABLE_TELEMETRY"] = "True"

# [TR] PaddleOCR/PaddleX log seviyesini bastır
os.environ["PADDLEX_LOG_LEVEL"] = "ERROR"
os.environ["FLAGS_call_stack_level"] = "0"

# [TR] ───── KRITIK: oneDNN/MKLDNN'i kapat ────────────────────────────────────
# Windows'ta PIR executor + oneDNN kombinasyonu PP-OCRv5_server_det'i çalıştıramıyor:
#   "NotImplementedError: ConvertPirAttribute2RuntimeAttribute not support
#    [pir::ArrayAttribute<pir::DoubleAttribute>]"
# Çözüm: MKLDNN'i global olarak kapat.
os.environ["FLAGS_use_mkldnn"] = "0"
# [TR] Eski executor'a düşmek için PIR'yi de kapatabiliriz (fallback; 3.x'te default
# her zaman PIR olmayabilir ama zararı yok).
os.environ.setdefault("FLAGS_enable_pir_in_executor", "0")

# [TR] Python uyarılarını bastır (deprecated parametre mesajları vb.)
warnings.filterwarnings("ignore")

MIN_CONFIDENCE = 0.4


# [TR] Kutuyu (x_min, y_min, x_max, y_max, y_center, height) 6-demet olarak normalize eder.
#      Paddle 3.x çeşitli formatlarda bbox döndürebilir:
#        - polygon: [[x1,y1],[x2,y2],[x3,y3],[x4,y4]]
#        - axis-aligned: [x_min, y_min, x_max, y_max]
#        - tuple of floats aynı şekilde
#      Hepsi aynı 6-demete indirgenir ki kümeleme/sıralama ortak kodla yapılabilsin.
def _normalize_box(box):
    try:
        pts = list(box)
    except TypeError:
        return None
    if not pts:
        return None

    # Polygon: 4 (x,y) noktası
    if isinstance(pts[0], (list, tuple)) and len(pts[0]) >= 2:
        xs = [float(p[0]) for p in pts if len(p) >= 2]
        ys = [float(p[1]) for p in pts if len(p) >= 2]
        if not xs or not ys:
            return None
        x_min, x_max = min(xs), max(xs)
        y_min, y_max = min(ys), max(ys)
    elif len(pts) >= 4:
        # Axis-aligned: [x_min, y_min, x_max, y_max]
        try:
            x_min = float(pts[0]); y_min = float(pts[1])
            x_max = float(pts[2]); y_max = float(pts[3])
        except (ValueError, TypeError):
            return None
    else:
        return None

    if x_max < x_min: x_min, x_max = x_max, x_min
    if y_max < y_min: y_min, y_max = y_max, y_min
    y_center = (y_min + y_max) / 2.0
    height = max(1.0, y_max - y_min)
    return (x_min, y_min, x_max, y_max, y_center, height)


# [TR] Paddle 3.x OCRResult objesinden (text, confidence, normalized_box) üçlülerini derler.
#      Bbox yoksa None döndürülür ve downstream kod metni sıraya dizer (fallback).
def _extract_triplets_from_object(obj):
    def _get(key):
        if hasattr(obj, key):
            return getattr(obj, key)
        try:
            return obj[key]
        except (KeyError, TypeError, IndexError):
            return None

    texts = _get("rec_texts")
    scores = _get("rec_scores")
    # Paddle 3.x: rec_boxes (axis-aligned), rec_polys (poligon). İkisi de işe yarar.
    boxes = _get("rec_boxes")
    if boxes is None:
        boxes = _get("rec_polys")
    if boxes is None:
        boxes = _get("dt_polys")  # detection polygons — aynı işi görür

    if texts is None:
        # Tekil alan (rec_text) — genelde bbox yok; sadece metin döner.
        t = _get("rec_text")
        if isinstance(t, str) and t.strip():
            s = _get("rec_score")
            try:
                conf = float(s) if s is not None else 1.0
            except (ValueError, TypeError):
                conf = 1.0
            return [(t, conf, None)]
        return []

    try:
        t_list = list(texts)
    except TypeError:
        t_list = []
    try:
        s_list = list(scores) if scores is not None else []
    except TypeError:
        s_list = []
    try:
        b_list = list(boxes) if boxes is not None else []
    except TypeError:
        b_list = []

    triplets = []
    for i, t in enumerate(t_list):
        if not isinstance(t, str) or not t.strip():
            continue
        conf = 1.0
        if i < len(s_list):
            try:
                conf = float(s_list[i])
            except (ValueError, TypeError):
                conf = 1.0
        if conf < MIN_CONFIDENCE:
            continue
        nb = _normalize_box(b_list[i]) if i < len(b_list) else None
        triplets.append((t, conf, nb))
    return triplets


# [TR] (text, conf, box) üçlülerinden satır/sütun farkındalı çıktı üretir.
#      1) Bbox'ı olan girdiler y-merkezine göre satırlara kümelenir (aynı satır
#         tespiti: y-center farkı satır yüksekliğinin yarısından az ise aynı satır).
#      2) Her satır içinde x-min'e göre soldan sağa sıralanır.
#      3) Ardışık hücreler arasındaki yatay boşluk satır yüksekliğinin ~1.5 katını
#         aşarsa ayırıcı olarak "\t" (tab), aksi halde " " (boşluk) kullanılır.
#         → Tablolar tab-ayrılı, paragraflar boşluklu çıkar.
#      4) Bbox'ı olmayan girdiler sona paragraf olarak eklenir.
def _layout_to_text(triplets):
    with_box = [t for t in triplets if t[2] is not None]
    without_box = [t for t in triplets if t[2] is None]

    # Fallback: hiç bbox yoksa sadece metinleri alt alta yaz.
    if not with_box:
        return [t for t, _, _ in without_box]

    # 1) Satır kümeleme
    # Önce y-center'a göre sırala.
    sorted_by_y = sorted(with_box, key=lambda it: it[2][4])
    rows = []  # her eleman: {"y": float, "h": float, "items": [(text, conf, box), ...]}
    for item in sorted_by_y:
        _, _, box = item
        y_center = box[4]
        height = box[5]
        if not rows:
            rows.append({"y": y_center, "h": height, "items": [item]})
            continue
        last = rows[-1]
        # [TR] Satır yüksekliğinin yarısı + küçük sabit tolerans → aynı satır testi.
        threshold = max(last["h"], height) * 0.6
        if abs(y_center - last["y"]) <= threshold:
            last["items"].append(item)
            # Satır y ve h değerlerini güncelle (ortalama)
            n = len(last["items"])
            last["y"] = (last["y"] * (n - 1) + y_center) / n
            last["h"] = max(last["h"], height)
        else:
            rows.append({"y": y_center, "h": height, "items": [item]})

    # 2) + 3) Her satır içinde x'e göre sırala ve gap'e göre ayırıcı seç.
    out_lines = []
    for row in rows:
        items = sorted(row["items"], key=lambda it: it[2][0])  # x_min
        row_h = row["h"]
        gap_threshold = row_h * 1.5  # bu eşikten geniş boşluk → tab
        parts = []
        prev_x_max = None
        for (text, _conf, box) in items:
            x_min = box[0]
            if prev_x_max is None:
                parts.append(text)
            else:
                gap = x_min - prev_x_max
                if gap > gap_threshold:
                    parts.append("\t" + text)
                else:
                    # Küçük gap → zaten boşluk var sayılır; kelime birleşmesin diye space.
                    parts.append(" " + text)
            prev_x_max = box[2]  # x_max
        out_lines.append("".join(parts))

    # 4) Bbox'ı olmayanları sona ekle
    out_lines.extend(t for t, _, _ in without_box)
    return out_lines


def parse_result(result):
    """
    [TR] 2.x + 3.x sonuç formatlarını birlikte işler VE bbox'a göre satır/tablo
         yapısını korur. Çıktı:
           - Prose/paragraf → her satır tek parça metin
           - Tablo → aynı satırdaki hücreler '\t' ile ayrılır
         Textarea'da monospace font ile hizalı görünür; AI'ya gönderildiğinde de
         sütun yapısı TSV gibi okunur.
    """
    if not result:
        return []

    all_triplets = []

    for page in result:
        if page is None:
            continue

        # Önce 3.x OCRResult/dict formatı
        triplets = _extract_triplets_from_object(page)
        if triplets:
            all_triplets.extend(triplets)
            continue

        # 2.x / liste-iç-liste formatı
        if isinstance(page, (list, tuple)):
            for item in page:
                if not item:
                    continue
                # Bazı 3.x çıktılarında iç eleman da OCRResult olabilir
                sub = _extract_triplets_from_object(item)
                if sub:
                    all_triplets.extend(sub)
                    continue
                # 2.x: [bbox_polygon, (text, conf)]
                if isinstance(item, (list, tuple)) and len(item) >= 2:
                    bbox_raw = item[0]
                    text_part = item[1]
                    nb = _normalize_box(bbox_raw)
                    if isinstance(text_part, (list, tuple)) and len(text_part) >= 2:
                        text, conf = text_part[0], text_part[1]
                        try:
                            conf = float(conf)
                        except (ValueError, TypeError):
                            conf = 1.0
                        if isinstance(text, str) and conf >= MIN_CONFIDENCE:
                            all_triplets.append((text, conf, nb))
                    elif isinstance(text_part, str):
                        all_triplets.append((text_part, 1.0, nb))

    return _layout_to_text(all_triplets)


def try_init_ocr(configs):
    """
    [TR] Sırasıyla farklı PaddleOCR konfigürasyonlarını dener, ilk başarılıyı döner.
         Bazı kwarg'lar (text_detection_model_name, enable_mkldnn) Paddle sürümüne
         göre desteklenmeyebilir; o yüzden listede sadeleştirilmiş fallback'ler var.
    """
    from paddleocr import PaddleOCR
    last_err = None
    for cfg in configs:
        try:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                ocr = PaddleOCR(**cfg)
            return ocr, cfg
        except TypeError as e:
            # Bilinmeyen kwarg — bir sonraki config'e geç
            last_err = e
        except Exception as e:  # noqa: BLE001
            last_err = e
    raise RuntimeError(f"PaddleOCR baslatma hatasi: {last_err}")


def try_run_ocr(ocr, image_path):
    """
    [TR] Farklı OCR çağrı stillerini sırayla dener:
         1) ocr.predict(image_path)           → PaddleOCR 3.x native API
         2) ocr.ocr(image_path)               → Hem 2.x hem 3.x (kwarg'sız)
         3) ocr.ocr(image_path, cls=True)     → Yalnız 2.x
    """
    errors = []

    if hasattr(ocr, "predict"):
        try:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                return ocr.predict(image_path)
        except TypeError as e:
            errors.append(("predict(img)", e))
            try:
                with warnings.catch_warnings():
                    warnings.simplefilter("ignore")
                return ocr.predict(input=image_path)
            except Exception as e2:  # noqa: BLE001
                errors.append(("predict(input=img)", e2))
        except Exception as e:  # noqa: BLE001
            errors.append(("predict(img)", e))

    if hasattr(ocr, "ocr"):
        try:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                return ocr.ocr(image_path)
        except Exception as e:  # noqa: BLE001
            errors.append(("ocr(img)", e))

        try:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                return ocr.ocr(image_path, cls=True)
        except Exception as e:  # noqa: BLE001
            errors.append(("ocr(img, cls=True)", e))

    detail = "; ".join(f"{name}: {type(err).__name__}: {err}" for name, err in errors)
    raise RuntimeError(f"OCR calistirma hatasi: {detail}")


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

    # [TR] PaddleOCR 3.x: MOBILE det modelini zorla seç, oneDNN'i kapat.
    # Windows'taki ConvertPirAttribute2RuntimeAttribute bug'ını önlemek için:
    #   1) text_detection_model_name="PP-OCRv5_mobile_det"  (server değil)
    #   2) enable_mkldnn=False                              (oneDNN kapalı)
    # Bu iki parametreden herhangi biri desteklenmezse listedeki daha basit
    # config'lere otomatik düşeriz.
    MOBILE_FULL = {
        "text_detection_model_name": "PP-OCRv5_mobile_det",
        "text_recognition_model_name": "en_PP-OCRv5_mobile_rec",
        "use_doc_orientation_classify": False,
        "use_doc_unwarping": False,
        "use_textline_orientation": False,
        "enable_mkldnn": False,
        "lang": "en",
    }
    MOBILE_NO_MKLDNN_KW = {
        "text_detection_model_name": "PP-OCRv5_mobile_det",
        "text_recognition_model_name": "en_PP-OCRv5_mobile_rec",
        "use_doc_orientation_classify": False,
        "use_doc_unwarping": False,
        "use_textline_orientation": False,
        "lang": "en",
    }
    MOBILE_MIN = {
        "text_detection_model_name": "PP-OCRv5_mobile_det",
        "use_doc_orientation_classify": False,
        "use_doc_unwarping": False,
        "use_textline_orientation": False,
        "lang": "en",
    }
    MOBILE_BARE = {
        "text_detection_model_name": "PP-OCRv5_mobile_det",
        "lang": "en",
    }
    # Son çare: mobile det kwarg'ı desteklenmiyorsa default'a düş (oneDNN kapalı
    # global FLAGS ile server_det'i de çalıştırmayı deneriz).
    GENERIC_MIN = {
        "use_doc_orientation_classify": False,
        "use_doc_unwarping": False,
        "use_textline_orientation": False,
        "enable_mkldnn": False,
        "lang": "en",
    }
    GENERIC_BARE = {
        "use_doc_orientation_classify": False,
        "use_doc_unwarping": False,
        "use_textline_orientation": False,
        "lang": "en",
    }
    # 2.x fallback (çok eski kurulumlar için)
    LEGACY_2X = {"use_angle_cls": False, "lang": "en"}

    init_configs = [
        MOBILE_FULL,
        MOBILE_NO_MKLDNN_KW,
        MOBILE_MIN,
        MOBILE_BARE,
        GENERIC_MIN,
        GENERIC_BARE,
        LEGACY_2X,
        {},
    ]

    try:
        ocr, used_cfg = try_init_ocr(init_configs)
        # stderr'e debug: hangi config çalıştı?
        print(f"[paddle_ocr] init OK with keys: {sorted(used_cfg.keys())}", file=sys.stderr)
    except Exception as e:  # noqa: BLE001
        print(str(e), file=sys.stderr)
        sys.exit(1)

    try:
        result = try_run_ocr(ocr, image_path)
    except Exception as e:  # noqa: BLE001
        print(str(e), file=sys.stderr)
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)

    lines = parse_result(result)
    print("\n".join(lines))


if __name__ == "__main__":
    main()
