"use client";

/**
 * [TR] Bu modül: PDF çalışma alanı — görüntüleyici, araç çubuğu, bölge seçimi, mock OCR, düzenlenebilir metin.
 * PDF.js worker yapılandırması react-pdf dokümantasyonuna uygun olarak bu dosyada yapılır (import sırası).
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Document, Page, pdfjs } from "react-pdf";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { getOcrProvider } from "@/lib/ocr";
import type { NormalizedPdfRegion } from "@/lib/ocr/types";
import { DocumentStatusBadge } from "@/components/documents/document-status-badge";
import type { DocumentStatusValue } from "@/lib/types/enums";

// [TR] Worker aynı origin’den: HTTPS dev + kurumsal ağda unpkg engeli yüzünden boş PDF tuvali olmasın.
pdfjs.GlobalWorkerOptions.workerSrc = "/pdf.worker.min.mjs";

const MIN_BOX_PX = 6;
const ZOOM_STEP = 1.15;
const SCALE_MIN = 0.35;
const SCALE_MAX = 3.5;

type PixelRect = { left: number; top: number; width: number; height: number };

function clamp01(v: number) {
  return Math.min(1, Math.max(0, v));
}

/** [TR] Seçim kutusunu örtü piksel boyutuna göre 0–1 aralığına çevirir (Prisma RegionSelection ile uyumlu). */
function pixelRectToNormalized(
  rect: PixelRect,
  overlayWidth: number,
  overlayHeight: number,
  pageNumber: number
): NormalizedPdfRegion {
  const w = Math.max(1, overlayWidth);
  const h = Math.max(1, overlayHeight);
  return {
    pageNumber,
    xNorm: clamp01(rect.left / w),
    yNorm: clamp01(rect.top / h),
    widthNorm: clamp01(rect.width / w),
    heightNorm: clamp01(rect.height / h),
  };
}

/** [TR] Tıklama sürükleme sırasında canlı kutu: başlangıç ve güncel imleç konumundan dikdörtgen üretir. */
function boxFromDrag(origin: { x: number; y: number }, cur: { x: number; y: number }): PixelRect {
  const left = Math.min(origin.x, cur.x);
  const top = Math.min(origin.y, cur.y);
  return {
    left,
    top,
    width: Math.abs(cur.x - origin.x),
    height: Math.abs(cur.y - origin.y),
  };
}

export type DocumentWorkspaceProps = {
  documentId: string;
  title: string;
  fileName: string;
  status: DocumentStatusValue;
  pdfUrl: string;
};

export function DocumentWorkspace({
  documentId,
  title,
  fileName,
  status,
  pdfUrl,
}: DocumentWorkspaceProps) {
  const [numPages, setNumPages] = useState<number>(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [pdfLoadError, setPdfLoadError] = useState<string | null>(null);
  const [scale, setScale] = useState(1);
  const [pagePtSize, setPagePtSize] = useState<{ width: number; height: number } | null>(null);

  const viewerRef = useRef<HTMLDivElement>(null);

  const [selectionMode, setSelectionMode] = useState(false);
  /** [TR] Sürükleme aktif mi; sadece seçim modunda anlamlı. */
  const [isDrawing, setIsDrawing] = useState(false);
  const dragOriginRef = useRef<{ x: number; y: number } | null>(null);
  /** [TR] Onay öncesi piksel kutusu (özet üzerinde de gösterilir). */
  const [draftRect, setDraftRect] = useState<PixelRect | null>(null);
  /** [TR] Taslağı yüzdeye çevirirken kullanılan örtü boyutu (özet paneli ref’siz çalışsın diye). */
  const [draftOverlaySize, setDraftOverlaySize] = useState<{ w: number; h: number } | null>(null);
  const [confirmedRegion, setConfirmedRegion] = useState<NormalizedPdfRegion | null>(null);
  /** [TR] Onaylı bölgenin çizildiği ölçekteki özet boyutu — önizleme oranını korumak için. */
  const [confirmedOverlaySize, setConfirmedOverlaySize] = useState<{ w: number; h: number } | null>(null);

  const [ocrText, setOcrText] = useState("");
  const [ocrLoading, setOcrLoading] = useState(false);

  const overlayRef = useRef<HTMLDivElement>(null);

  const onDocumentLoad = useCallback((pdf: { numPages: number }) => {
    setNumPages(pdf.numPages);
    setPdfLoadError(null);
  }, []);

  const onPageLoadSuccess = useCallback((page: { width: number; height: number }) => {
    setPagePtSize({ width: page.width, height: page.height });
  }, []);

  /** [TR] Sayfa değişince taslak/onaylı seçimi sıfırla; yanlış sayfada OCR önlenir. */
  useEffect(() => {
    setDraftRect(null);
    setDraftOverlaySize(null);
    setConfirmedRegion(null);
    setConfirmedOverlaySize(null);
    dragOriginRef.current = null;
    setIsDrawing(false);
  }, [pageNumber]);

  const fitWidth = useCallback(() => {
    const el = viewerRef.current;
    const pw = pagePtSize?.width;
    if (!el || !pw) return;
    const available = el.clientWidth - 32;
    if (available <= 0) return;
    const next = available / pw;
    setScale(Math.min(SCALE_MAX, Math.max(SCALE_MIN, next)));
  }, [pagePtSize]);

  const pageW = pagePtSize?.width;
  const pageH = pagePtSize?.height;

  /** [TR] Sayfa değişince genişliğe sığdır; ölçek sadece pageNumber veya sayfa pt boyutu değişince sıfırlanır (zoom’u bozmaz). */
  useEffect(() => {
    if (pageW == null || pageH == null) return;
    const el = viewerRef.current;
    if (!el) return;
    const available = el.clientWidth - 32;
    if (available <= 0) return;
    const next = available / pageW;
    setScale(Math.min(SCALE_MAX, Math.max(SCALE_MIN, next)));
  }, [pageNumber, pageW, pageH]);

  const zoomIn = () => setScale((s) => Math.min(SCALE_MAX, s * ZOOM_STEP));
  const zoomOut = () => setScale((s) => Math.max(SCALE_MIN, s / ZOOM_STEP));

  const startDraftFromEvent = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!selectionMode || ocrLoading) return;
    const el = e.currentTarget;
    setDraftOverlaySize({ w: el.clientWidth, h: el.clientHeight });
    const ox = e.nativeEvent.offsetX;
    const oy = e.nativeEvent.offsetY;
    dragOriginRef.current = { x: ox, y: oy };
    setIsDrawing(true);
    setDraftRect({ left: ox, top: oy, width: 0, height: 0 });
  };

  const updateDraftFromEvent = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!isDrawing || !dragOriginRef.current) return;
    const cur = { x: e.nativeEvent.offsetX, y: e.nativeEvent.offsetY };
    setDraftRect(boxFromDrag(dragOriginRef.current, cur));
  };

  const endDraft = () => {
    setIsDrawing(false);
    dragOriginRef.current = null;
  };

  const confirmRegion = () => {
    const el = overlayRef.current;
    if (!draftRect || !el) return;
    if (draftRect.width < MIN_BOX_PX || draftRect.height < MIN_BOX_PX) return;
    const norm = pixelRectToNormalized(draftRect, el.clientWidth, el.clientHeight, pageNumber);
    setConfirmedRegion(norm);
    setConfirmedOverlaySize({ w: el.clientWidth, h: el.clientHeight });
    setDraftRect(null);
    setDraftOverlaySize(null);
    endDraft();
    setSelectionMode(false);
  };

  const cancelRegion = () => {
    if (isDrawing || draftRect) {
      setDraftRect(null);
      setDraftOverlaySize(null);
      endDraft();
      return;
    }
    setConfirmedRegion(null);
    setConfirmedOverlaySize(null);
  };

  const runOcr = async () => {
    if (!confirmedRegion) return;
    setOcrLoading(true);
    try {
      const provider = getOcrProvider();
      const res = await provider.extractTextFromPdfRegion({
        pdfUrl,
        region: confirmedRegion,
      });
      setOcrText(res.text);
    } finally {
      setOcrLoading(false);
    }
  };

  const pageAspect = pagePtSize ? pagePtSize.width / pagePtSize.height : 4 / 3;

  const previewBoxStyle = useMemo((): React.CSSProperties | undefined => {
    if (!confirmedRegion || !confirmedOverlaySize) return undefined;
    return {
      left: `${confirmedRegion.xNorm * 100}%`,
      top: `${confirmedRegion.yNorm * 100}%`,
      width: `${confirmedRegion.widthNorm * 100}%`,
      height: `${confirmedRegion.heightNorm * 100}%`,
    };
  }, [confirmedRegion, confirmedOverlaySize]);

  const draftPreviewStyle = useMemo((): React.CSSProperties | undefined => {
    if (!draftRect || !draftOverlaySize) return undefined;
    const { w, h } = draftOverlaySize;
    if (w <= 0 || h <= 0) return undefined;
    return {
      left: `${(draftRect.left / w) * 100}%`,
      top: `${(draftRect.top / h) * 100}%`,
      width: `${(draftRect.width / w) * 100}%`,
      height: `${(draftRect.height / h) * 100}%`,
    };
  }, [draftRect, draftOverlaySize]);

  return (
    <div className="flex min-h-[520px] w-full flex-1 flex-col gap-0 border border-slate-200 bg-white shadow-sm lg:min-h-[calc(100dvh-5.5rem)] lg:flex-row">
      {/* [TR] Sol: PDF ve üst araç çubuğu */}
      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 bg-slate-50/90 px-3 py-2">
          <Button variant="outline" size="sm" asChild>
            <Link href="/app/documents">← Belgeler</Link>
          </Button>
          <div className="mx-2 hidden h-6 w-px bg-slate-200 sm:block" />
          <span className="truncate text-sm font-medium text-slate-800">{title}</span>
          <span className="hidden text-xs text-slate-500 sm:inline">({fileName})</span>
          <div className="ml-auto flex flex-wrap items-center gap-1">
            <Button
              variant={selectionMode ? "default" : "outline"}
              size="sm"
              type="button"
              onClick={() => {
                setSelectionMode((v) => !v);
                if (selectionMode) {
                  setDraftRect(null);
                  setDraftOverlaySize(null);
                  endDraft();
                }
              }}
              disabled={ocrLoading}
            >
              {selectionMode ? "Seçim modu: Açık" : "Seçim modu"}
            </Button>
            <Button variant="outline" size="sm" type="button" onClick={cancelRegion} disabled={ocrLoading}>
              İptal
            </Button>
            <Button
              size="sm"
              type="button"
              onClick={confirmRegion}
              disabled={
                !selectionMode ||
                !draftRect ||
                draftRect.width < MIN_BOX_PX ||
                draftRect.height < MIN_BOX_PX ||
                ocrLoading
              }
            >
              Bölgeyi onayla
            </Button>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 px-3 py-2 text-sm">
          <Button variant="outline" size="sm" type="button" onClick={() => setPageNumber((p) => Math.max(1, p - 1))} disabled={pageNumber <= 1}>
            Önceki
          </Button>
          <span className="text-slate-600">
            Sayfa{" "}
            <input
              type="number"
              min={1}
              max={Math.max(1, numPages)}
              className="w-14 rounded border border-slate-200 px-1 py-0.5 text-center text-slate-900"
              value={pageNumber}
              onChange={(e) => {
                const v = Number(e.target.value);
                if (!Number.isFinite(v)) return;
                setPageNumber(Math.min(Math.max(1, v), Math.max(1, numPages)));
              }}
            />{" "}
            / {numPages || "…"}
          </span>
          <Button
            variant="outline"
            size="sm"
            type="button"
            onClick={() => setPageNumber((p) => Math.min(Math.max(1, numPages), p + 1))}
            disabled={numPages === 0 || pageNumber >= numPages}
          >
            Sonraki
          </Button>
          <div className="mx-1 h-5 w-px bg-slate-200" />
          <Button variant="outline" size="sm" type="button" onClick={zoomOut} disabled={ocrLoading}>
            −
          </Button>
          <span className="w-14 text-center text-slate-600">{Math.round(scale * 100)}%</span>
          <Button variant="outline" size="sm" type="button" onClick={zoomIn} disabled={ocrLoading}>
            +
          </Button>
          <Button variant="outline" size="sm" type="button" onClick={fitWidth} disabled={!pagePtSize}>
            Sığdır (genişlik)
          </Button>
        </div>

        <div ref={viewerRef} className="min-h-0 flex-1 overflow-auto bg-slate-200/60 p-4">
          {pdfLoadError ? (
            <p className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">{pdfLoadError}</p>
          ) : (
            /* [TR] Metin/annotasyon katmanı kapalı: sadece görüntü + bizim seçim örtüsü. */
            <Document
              file={pdfUrl}
              onLoadSuccess={onDocumentLoad}
              onLoadError={(err) => setPdfLoadError(err.message || "PDF yüklenemedi.")}
              loading={<p className="text-sm text-slate-600">PDF yükleniyor…</p>}
              className="mx-auto flex justify-center"
            >
              <div className="relative inline-block shadow-md">
                <Page
                  pageNumber={pageNumber}
                  scale={scale}
                  onLoadSuccess={onPageLoadSuccess}
                  renderTextLayer={false}
                  renderAnnotationLayer={false}
                  loading={<div className="h-96 w-72 animate-pulse bg-slate-300/50" />}
                />
                {selectionMode ? (
                  <div
                    ref={overlayRef}
                    className="absolute inset-0 cursor-crosshair ring-2 ring-sky-400/80 ring-inset"
                    role="presentation"
                    onMouseDown={startDraftFromEvent}
                    onMouseMove={updateDraftFromEvent}
                    onMouseUp={endDraft}
                    onMouseLeave={endDraft}
                  >
                    {/* [TR] Sürüklerken canlı dikdörtgen */}
                    {draftRect && draftRect.width >= 1 && draftRect.height >= 1 ? (
                      <div
                        className="pointer-events-none absolute border-2 border-sky-500 bg-sky-400/25"
                        style={{
                          left: draftRect.left,
                          top: draftRect.top,
                          width: draftRect.width,
                          height: draftRect.height,
                        }}
                      />
                    ) : null}
                  </div>
                ) : null}
              </div>
            </Document>
          )}
        </div>
        <p className="border-t border-slate-100 px-3 py-2 text-xs text-slate-500">
          Belge kimliği: {documentId} · OCR yalnızca PDF içinde onaylanmış dikdörtgen için çalışır.
        </p>
      </div>

      {/* [TR] Sağ: önizleme, çıkar, düzenlenebilir metin */}
      <aside className="flex w-full shrink-0 flex-col border-t border-slate-200 bg-white lg:max-h-none lg:w-[340px] lg:min-h-0 lg:overflow-y-auto lg:border-l lg:border-t-0 lg:self-stretch">
        <div className="border-b border-slate-100 px-4 py-3">
          <div className="flex items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-slate-900">Araçlar ve metin</h2>
            <DocumentStatusBadge status={status} />
          </div>
          <p className="mt-1 text-xs text-slate-500">
            Seçim modunda sayfa üzerinde sürükleyin; &quot;Bölgeyi onayla&quot; ile koordinatları sabitleyin.
          </p>
        </div>

        <div className="border-b border-slate-100 px-4 py-3">
          <p className="text-xs font-medium text-slate-700">Bölge önizlemesi (oran)</p>
          <div
            className="relative mt-2 w-full overflow-hidden rounded-md border border-slate-200 bg-slate-100"
            style={{ aspectRatio: `${pageAspect}` }}
          >
            {confirmedRegion && previewBoxStyle ? (
              <div className="pointer-events-none absolute border-2 border-emerald-600 bg-emerald-400/30" style={previewBoxStyle} />
            ) : draftRect && draftPreviewStyle && !confirmedRegion ? (
              <div className="pointer-events-none absolute border-2 border-dashed border-sky-500 bg-sky-400/20" style={draftPreviewStyle} />
            ) : (
              <div className="flex h-full items-center justify-center text-xs text-slate-400">Henüz onaylı kutu yok</div>
            )}
          </div>
          {confirmedRegion ? (
            <dl className="mt-2 grid grid-cols-2 gap-x-2 gap-y-1 font-mono text-[10px] text-slate-600">
              <dt>Sayfa</dt>
              <dd>{confirmedRegion.pageNumber}</dd>
              <dt>x,y</dt>
              <dd>
                {confirmedRegion.xNorm.toFixed(3)}, {confirmedRegion.yNorm.toFixed(3)}
              </dd>
              <dt>w,h</dt>
              <dd>
                {confirmedRegion.widthNorm.toFixed(3)}, {confirmedRegion.heightNorm.toFixed(3)}
              </dd>
            </dl>
          ) : null}
        </div>

        <div className="flex gap-2 border-b border-slate-100 px-4 py-3">
          <Button className="flex-1" type="button" onClick={runOcr} disabled={!confirmedRegion || ocrLoading}>
            {ocrLoading ? "Çıkarılıyor…" : "Metni çıkar (OCR)"}
          </Button>
        </div>

        <div className="flex min-h-0 flex-1 flex-col px-4 py-3">
          <label htmlFor="ocr-text" className="text-xs font-medium text-slate-700">
            OCR sonucu (düzenlenebilir)
          </label>
          <textarea
            id="ocr-text"
            className="mt-1 min-h-[200px] flex-1 resize-y rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 shadow-inner focus:outline-none focus:ring-2 focus:ring-slate-400"
            placeholder={
              confirmedRegion
                ? "Metni çıkar veya doğrudan yazın. AI adımından önce düzeltmeler burada yapılabilir."
                : "Önce PDF üzerinde bir bölge seçip onaylayın."
            }
            value={ocrText}
            onChange={(e) => setOcrText(e.target.value)}
            disabled={ocrLoading}
          />
          {ocrLoading ? <p className="mt-2 text-xs text-sky-700">Mock OCR çalışıyor…</p> : null}
        </div>
      </aside>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app/documents/[id] istemci çalışma alanı — PDF + bölge + mock OCR + textarea.
 * [TR] Neden gerekli: react-pdf ve fare etkileşimi yalnızca tarayıcıda; sunucu bileşeni ince tutulur.
 * [TR] Sistem içinde: app/(protected)/app/documents/[id]/page.tsx (dynamic import, ssr: false).
 *
 * MODIFICATION NOTES (TR)
 * - PDF.js worker: `/public/pdf.worker.min.mjs` (react-pdf ile uyumlu sürüm); unpkg engellenirse yine çalışır.
 * - Çoklu kutu: draft/confirmed dizileri ve sıralı OCR.
 * - Tam sayfa: tek kutu tüm örtüyü kaplar veya ayrı “fit page” düğmesi.
 * - Yerel OCR: Worker + canvas kırpma; getOcrProvider içinde switch.
 * - PDF üzerinde metin highlight: text layer açılır ve koordinat dönüşümü yapılır.
 * - Genel resimden metin çıkarma özelliği bu sürümde yoktur; future work olarak düşünülmüştür.
 * - Zorluk: Yüksek (gerçek OCR ve performans).
 * -----------------------------------------------------------------------------
 */
