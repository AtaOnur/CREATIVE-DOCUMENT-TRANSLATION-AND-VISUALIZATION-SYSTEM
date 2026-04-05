/*
 * [TR] Bu dosya ne işe yarar: Workspace istemci davranışı — PDF render, sayfa/zoom kontrolü, dikdörtgen seçim overlay.
 * [TR] Neden gerekli: Details ekranını üretken bir çalışma alanına dönüştürür.
 * [TR] Kapsam: OCR bölge seçimi + AI panel tetikleme (çeviri/özet/rewrite/creative/visualize) mock akışı.
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu seçim için selection listesi ve çizim katmanı genişletilebilir.
 * - Tam sayfa OCR ve annotation araçları eklenebilir.
 * - Metin overlay (textLayer) ileride açılabilir.
 * - Genel resimden metin çıkarma özelliği bu sürümde bulunmamaktadır; future work olarak düşünülmüştür.
 * - Genel image-to-text işlemi bu modülün kapsamında değildir.
 * - Zorluk: Orta.
 */
import * as pdfjsLib from "/lib/pdfjs/pdf.min.mjs";

pdfjsLib.GlobalWorkerOptions.workerSrc = "/lib/pdfjs/pdf.worker.min.mjs";

const shell = document.querySelector(".workspace-shell");
if (!shell) {
  // Not a workspace page.
}

const pdfUrl = shell.dataset.pdfUrl || "";
const pagePreviewUrl = shell.dataset.pagePreviewUrl || "";
const extractUrl = shell.dataset.extractUrl || "";
const saveOcrUrl = shell.dataset.saveOcrUrl || "";
const aiProcessUrl = shell.dataset.aiProcessUrl || "";
const defaultAiModel = shell.dataset.defaultAiModel || "mock-gpt";
const defaultStyle = shell.dataset.defaultStyle || "Formal";
const documentId = shell.dataset.documentId || "";
let lastOcrResultId = shell.dataset.ocrResultId || "";

const antiForgeryInput = document.querySelector('input[name="__RequestVerificationToken"]');
const antiForgeryToken = antiForgeryInput ? antiForgeryInput.value : "";
const canvas = document.getElementById("pdf-canvas");
const overlay = document.getElementById("region-overlay");
const rect = document.getElementById("region-rect");
const empty = document.getElementById("workspace-viewer-empty");
const warning = document.getElementById("workspace-viewer-warning");
const nativePreview = document.getElementById("native-pdf-preview");
const btnOpenNativePreview = document.getElementById("btn-open-native-preview");

const pageInput = document.getElementById("input-page-number");
const pageTotal = document.getElementById("label-page-total");
const zoomLabel = document.getElementById("label-zoom");
const btnPrev = document.getElementById("btn-prev-page");
const btnNext = document.getElementById("btn-next-page");
const btnZoomIn = document.getElementById("btn-zoom-in");
const btnZoomOut = document.getElementById("btn-zoom-out");
const btnFit = document.getElementById("btn-fit-width");

const btnStartSelection = document.getElementById("btn-start-selection");
const btnConfirmSelection = document.getElementById("btn-confirm-selection");
const btnCancelSelection = document.getElementById("btn-cancel-selection");
const btnExtract = document.getElementById("btn-extract-text");
const btnSaveOcrText = document.getElementById("btn-save-ocr-text");
const ocrTextarea = document.getElementById("ocr-text-placeholder");
const ocrStatus = document.getElementById("ocr-status");
// [TR] OCR dil seçimi kaldırıldı; PaddleOCR varsayılan (en/Latin) modeli kullanır.

const aiOperation = document.getElementById("ai-operation");
const aiTranslateLangs = document.getElementById("ai-translate-langs");
const aiModel = document.getElementById("ai-model");
// [TR] Kaynak dil kaldırıldı; AI sadece hedef dil alır. Kaynak dil otomatik algılanır.
const aiTargetLanguage = document.getElementById("ai-target-language");
const aiStyle = document.getElementById("ai-style");
const aiInstruction = document.getElementById("ai-instruction");
const btnAiProcess = document.getElementById("btn-ai-process");
const aiStatus = document.getElementById("ai-status");
const aiPreview = document.getElementById("ai-preview");
const aiResultLink = document.getElementById("ai-result-link");

const elPage = document.getElementById("region-page");
const elX = document.getElementById("region-x");
const elY = document.getElementById("region-y");
const elW = document.getElementById("region-width");
const elH = document.getElementById("region-height");

let pdfDoc = null;
let currentPage = Number(shell.dataset.currentPage || "1");
let zoom = Number(shell.dataset.zoom || "100") / 100;

let selectionMode = false;
let drawing = false;
let startX = 0;
let startY = 0;
let currentRect = null;

function showFallbackWarning() {
  if (warning) warning.classList.remove("d-none");
}

function openNativePreview() {
  if (nativePreview) nativePreview.classList.remove("d-none");
}

function setRegionInfo(region) {
  if (!region) {
    elPage.textContent = "-";
    elX.textContent = "-";
    elY.textContent = "-";
    elW.textContent = "-";
    elH.textContent = "-";
    return;
  }
  elPage.textContent = String(region.pageNumber);
  elX.textContent = region.x.toFixed(1);
  elY.textContent = region.y.toFixed(1);
  elW.textContent = region.width.toFixed(1);
  elH.textContent = region.height.toFixed(1);
}

function setSelectionUi() {
  overlay.classList.toggle("workspace-region-overlay--active", selectionMode);
  btnStartSelection.textContent = selectionMode ? "Secim Aktif" : "Bolge Secimi Baslat";
  btnConfirmSelection.disabled = !currentRect;
  btnCancelSelection.disabled = !selectionMode && !currentRect;
  btnExtract.disabled = !currentRect;
  if (!currentRect && !lastOcrResultId) {
    btnSaveOcrText.disabled = true;
  }
}

function setOcrStatus(type, message) {
  if (!ocrStatus) return;
  ocrStatus.classList.remove("d-none", "text-success", "text-danger", "text-muted");
  if (type === "success") ocrStatus.classList.add("text-success");
  else if (type === "error") ocrStatus.classList.add("text-danger");
  else ocrStatus.classList.add("text-muted");
  ocrStatus.textContent = message;
}

function setAiStatus(type, message) {
  if (!aiStatus) return;
  aiStatus.classList.remove("d-none", "text-success", "text-danger", "text-muted");
  if (type === "success") aiStatus.classList.add("text-success");
  else if (type === "error") aiStatus.classList.add("text-danger");
  else aiStatus.classList.add("text-muted");
  aiStatus.textContent = message;
}

function clearDraftRect() {
  currentRect = null;
  rect.classList.add("d-none");
  rect.style.left = "";
  rect.style.top = "";
  rect.style.width = "";
  rect.style.height = "";
}

function drawDraftRect(x1, y1, x2, y2) {
  const left = Math.min(x1, x2);
  const top = Math.min(y1, y2);
  const width = Math.abs(x2 - x1);
  const height = Math.abs(y2 - y1);

  currentRect = { left, top, width, height, pageNumber: currentPage };
  rect.style.left = `${left}px`;
  rect.style.top = `${top}px`;
  rect.style.width = `${width}px`;
  rect.style.height = `${height}px`;
  rect.classList.remove("d-none");
}

async function renderPage(pageNumber) {
  const rendered = await renderPageFromServer(pageNumber);
  if (!rendered && pdfDoc) {
    const page = await pdfDoc.getPage(pageNumber);
    const viewport = page.getViewport({ scale: zoom });
    canvas.width = Math.floor(viewport.width);
    canvas.height = Math.floor(viewport.height);
    overlay.style.width = `${canvas.width}px`;
    overlay.style.height = `${canvas.height}px`;
    rect.style.maxWidth = `${canvas.width}px`;
    rect.style.maxHeight = `${canvas.height}px`;

    const ctx = canvas.getContext("2d");
    await page.render({ canvasContext: ctx, viewport }).promise;
  }

  pageInput.value = String(currentPage);
  pageTotal.textContent = `/ ${pdfDoc ? pdfDoc.numPages : 1}`;
  zoomLabel.textContent = `${Math.round(zoom * 100)}%`;
}

async function renderPageFromServer(pageNumber) {
  if (!pagePreviewUrl) return false;
  try {
    const safeZoom = Math.max(0.4, Math.min(2.5, zoom));
    const dpi = Math.round(170 * safeZoom);
    const url = `${pagePreviewUrl}${pagePreviewUrl.includes("?") ? "&" : "?"}page=${pageNumber}&dpi=${dpi}`;
    const res = await fetch(url, { credentials: "same-origin" });
    if (!res.ok) return false;

    const blob = await res.blob();
    const imgUrl = URL.createObjectURL(blob);
    const img = await new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = reject;
      image.src = imgUrl;
    });

    canvas.width = img.width;
    canvas.height = img.height;
    overlay.style.width = `${canvas.width}px`;
    overlay.style.height = `${canvas.height}px`;
    rect.style.maxWidth = `${canvas.width}px`;
    rect.style.maxHeight = `${canvas.height}px`;
    const ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0);
    URL.revokeObjectURL(imgUrl);
    return true;
  } catch {
    return false;
  }
}

function normalizeMousePosition(evt) {
  const b = overlay.getBoundingClientRect();
  return {
    x: Math.max(0, Math.min(evt.clientX - b.left, b.width)),
    y: Math.max(0, Math.min(evt.clientY - b.top, b.height)),
  };
}

overlay.addEventListener("mousedown", (evt) => {
  if (!selectionMode) return;
  drawing = true;
  const p = normalizeMousePosition(evt);
  startX = p.x;
  startY = p.y;
  drawDraftRect(startX, startY, startX, startY);
  setSelectionUi();
});

window.addEventListener("mousemove", (evt) => {
  if (!drawing || !selectionMode) return;
  const p = normalizeMousePosition(evt);
  drawDraftRect(startX, startY, p.x, p.y);
  setSelectionUi();
});

window.addEventListener("mouseup", () => {
  drawing = false;
});

btnStartSelection.addEventListener("click", () => {
  selectionMode = !selectionMode;
  setSelectionUi();
});

btnConfirmSelection.addEventListener("click", () => {
  if (!currentRect) return;
  selectionMode = false;
  setSelectionUi();
  setRegionInfo({
    pageNumber: currentRect.pageNumber,
    x: currentRect.left,
    y: currentRect.top,
    width: currentRect.width,
    height: currentRect.height,
  });
});

btnCancelSelection.addEventListener("click", () => {
  selectionMode = false;
  clearDraftRect();
  setSelectionUi();
  setRegionInfo(null);
});

btnExtract.addEventListener("click", () => {
  if (!currentRect || !extractUrl || !documentId) return;
  const ow = overlay.clientWidth || canvas.width || 1;
  const oh = overlay.clientHeight || canvas.height || 1;
  const payload = {
    documentId,
    // [TR] OCR dil seçimi kaldırıldı; PaddleOCR otomatik algılar.
    region: {
      pageNumber: currentRect.pageNumber,
      x: currentRect.left / ow,
      y: currentRect.top / oh,
      width: currentRect.width / ow,
      height: currentRect.height / oh,
    },
  };

  btnExtract.disabled = true;
  setOcrStatus("info", "OCR çalışıyor...");
  fetch(extractUrl, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: antiForgeryToken,
    },
    body: JSON.stringify(payload),
  })
    .then((r) => r.json())
    .then((res) => {
      if (!res.ok) {
        setOcrStatus("error", res.message || "OCR başarısız.");
        return;
      }
      if (ocrTextarea) ocrTextarea.value = res.text || "";
      lastOcrResultId = res.ocrResultId || "";
      if (btnSaveOcrText) btnSaveOcrText.disabled = !lastOcrResultId;
      setOcrStatus("success", res.message || "OCR tamamlandı.");
    })
    .catch(() => setOcrStatus("error", "OCR isteği sırasında hata oluştu."))
    .finally(() => {
      setSelectionUi();
    });
});

if (btnSaveOcrText) {
  btnSaveOcrText.disabled = !lastOcrResultId;
  btnSaveOcrText.addEventListener("click", () => {
    if (!saveOcrUrl || !lastOcrResultId || !ocrTextarea) return;
    btnSaveOcrText.disabled = true;
    setOcrStatus("info", "OCR metni kaydediliyor...");
    fetch(saveOcrUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken,
      },
      body: JSON.stringify({
        ocrResultId: lastOcrResultId,
        text: ocrTextarea.value || "",
      }),
    })
      .then((r) => r.json())
      .then((res) => {
        if (!res.ok) {
          setOcrStatus("error", res.message || "Kaydetme başarısız.");
          btnSaveOcrText.disabled = false;
          return;
        }
        setOcrStatus("success", res.message || "OCR metni kaydedildi.");
        btnSaveOcrText.disabled = false;
      })
      .catch(() => {
        setOcrStatus("error", "Kaydetme sırasında hata oluştu.");
        btnSaveOcrText.disabled = false;
      });
  });
}

if (btnAiProcess) {
  btnAiProcess.addEventListener("click", () => {
    if (!aiProcessUrl || !documentId || !ocrTextarea) return;
    const inputText = (ocrTextarea.value || "").trim();
    if (!inputText) {
      setAiStatus("error", "Önce OCR metni üretin veya metin kutusuna içerik girin.");
      return;
    }

    const payload = {
      documentId,
      operationType: aiOperation?.value || "Translate",
      modelName: aiModel?.value || "mock-gpt",
      // [TR] Kaynak dil kaldırıldı; AI prompt'unda kaynak dil "otomatik algıla" olarak geçer.
      targetLanguage: aiTargetLanguage?.value || "English",
      style: aiStyle?.value || "Formal",
      customInstruction: aiInstruction?.value || "",
      inputText,
      sourcePageNumber: currentRect?.pageNumber || currentPage,
    };

    btnAiProcess.disabled = true;
    if (aiPreview) aiPreview.classList.add("d-none");
    if (aiResultLink) aiResultLink.classList.add("d-none");
    setAiStatus("info", "AI işlemi çalışıyor...");

    fetch(aiProcessUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken,
      },
      body: JSON.stringify(payload),
    })
      .then((r) => r.json())
      .then((res) => {
        if (!res.ok) {
          setAiStatus("error", res.message || "AI işlemi başarısız.");
          return;
        }
        setAiStatus("success", res.message || "AI işlemi tamamlandı.");
        if (aiPreview) {
          const previewText = res.outputText || "";
          const imageHtml = res.outputImageUrl
            ? `<div class="mt-2"><img src="${res.outputImageUrl}" alt="AI visualize preview" class="img-fluid rounded border" /></div>`
            : "";
          aiPreview.innerHTML = `<strong>Onizleme</strong><br/>${previewText}${imageHtml}`;
          aiPreview.classList.remove("d-none");
        }
        if (aiResultLink && res.resultUrl) {
          aiResultLink.href = res.resultUrl;
          aiResultLink.classList.remove("d-none");
        }
      })
      .catch(() => setAiStatus("error", "AI isteği sırasında hata oluştu."))
      .finally(() => {
        btnAiProcess.disabled = false;
      });
  });
}

// [TR] Dil seçici satırını sadece "Translate" işlemi seçiliyken göster.
function syncTranslateLangsVisibility() {
  if (!aiTranslateLangs) return;
  const op = aiOperation ? aiOperation.value : "";
  aiTranslateLangs.style.display = op === "Translate" ? "" : "none";
}
if (aiOperation) {
  aiOperation.addEventListener("change", syncTranslateLangsVisibility);
}
// Sayfa ilk yüklendiğinde de hizala
syncTranslateLangsVisibility();

btnPrev.addEventListener("click", async () => {
  if (!pdfDoc || currentPage <= 1) return;
  currentPage -= 1;
  clearDraftRect();
  setSelectionUi();
  await renderPage(currentPage);
});

btnNext.addEventListener("click", async () => {
  if (!pdfDoc || currentPage >= pdfDoc.numPages) return;
  currentPage += 1;
  clearDraftRect();
  setSelectionUi();
  await renderPage(currentPage);
});

pageInput.addEventListener("change", async () => {
  if (!pdfDoc) return;
  let requested = Number(pageInput.value || "1");
  if (Number.isNaN(requested)) requested = 1;
  requested = Math.max(1, Math.min(pdfDoc.numPages, requested));
  currentPage = requested;
  clearDraftRect();
  setSelectionUi();
  await renderPage(currentPage);
});

btnZoomIn.addEventListener("click", async () => {
  zoom = Math.min(2.5, zoom + 0.1);
  clearDraftRect();
  setSelectionUi();
  await renderPage(currentPage);
});

btnZoomOut.addEventListener("click", async () => {
  zoom = Math.max(0.4, zoom - 0.1);
  clearDraftRect();
  setSelectionUi();
  await renderPage(currentPage);
});

btnFit.addEventListener("click", async () => {
  const frame = document.getElementById("workspace-viewer-frame");
  if (!pdfDoc || !frame) return;
  const page = await pdfDoc.getPage(currentPage);
  const unit = page.getViewport({ scale: 1.0 });
  zoom = Math.max(0.4, Math.min(2.5, (frame.clientWidth - 8) / unit.width));
  clearDraftRect();
  setSelectionUi();
  await renderPage(currentPage);
});

if (btnOpenNativePreview) {
  btnOpenNativePreview.addEventListener("click", () => {
    openNativePreview();
  });
}

async function init() {
  try {
    // [TR] Önce sayfa sayısı için pdf.js yüklenir; render için server-side PNG preview tercih edilir.
    if (pdfUrl) {
      try {
        pdfDoc = await pdfjsLib.getDocument({ url: pdfUrl, withCredentials: true }).promise;
      } catch {
        pdfDoc = null;
      }
    }
    currentPage = Math.max(1, Math.min(pdfDoc ? pdfDoc.numPages : 1, currentPage));
    await renderPage(currentPage);
    setSelectionUi();
    setRegionInfo(null);
    if (lastOcrResultId && btnSaveOcrText) btnSaveOcrText.disabled = false;
    if (aiModel) aiModel.value = defaultAiModel;
    if (aiStyle) aiStyle.value = defaultStyle;
  } catch (err) {
    console.error(err);
    empty.classList.remove("d-none");
    showFallbackWarning();
  }
}

init();

