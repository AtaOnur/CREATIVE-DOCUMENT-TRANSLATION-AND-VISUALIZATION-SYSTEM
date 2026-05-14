/*
 * [TR] Bu dosya ne işe yarar: Workspace istemci davranışı — PDF render, sayfa/zoom kontrolü, dikdörtgen seçim overlay.
 * [TR] Neden gerekli: Details ekranını üretken bir çalışma alanına dönüştürür.
 * [TR] Kapsam: OCR bölge seçimi + "Görsel Seç" (multimodal AI girdi) + AI panel + OCR metnini Gemini TTS ile seslendirme.
 *
 * SEÇİM AKIŞI (yeni):
 *   1. Kullanıcı PDF üzerinde sol fareye basılı tutup sürükler → dikdörtgen
 *      sınırları çizilir (otomatik OCR yapılmaz, sadece sınırlar gösterilir).
 *   2. "Metni Çıkar"  → mevcut OCR akışı (PaddleOCR servisi).
 *   3. "Görsel Seç"   → seçili bölge canvas'tan kırpılarak base64 PNG'ye çevrilir;
 *                       önizlemesi yan panelde gösterilir ve AI isteğine
 *                       inputImageBase64 olarak iliştirilir.
 *   4. "OCR Metnini Seslendir" → textarea’daki metin sunucuya gönderilir (OCR motorundan bağımsız), ses blob’u oynatılır.
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu seçim için selection listesi ve çizim katmanı genişletilebilir.
 * - Tam sayfa OCR ve annotation araçları eklenebilir.
 * - Metin overlay (textLayer) ileride açılabilir.
 * - Workspace durum satırları (#ocr-status, #ai-status): setOcrStatus / setAiStatus üçüncü argüman
 *   { loading: true } ile Bootstrap spinner-border-sm gösterir — "Metni Çıkar", seslendirme (istek +
 *   oynatma), NLP/AI işlemi beklerken kullanılır. Ses oynatımı bitince ended olayı clearOcrStatus() ile
 *   metni kaldırır (sabit "Ses oynatılıyor" kalıntısı olmaz).
 * - AI Sohbet (#ai-chat-window) sağ sidebar’ın üstünde; #region-page … #region-height üst toolbar’da (setRegionInfo).
 * - Seslendirme: <audio controls> veya duraklat/durdur UI ileride eklenebilir.
 * - Görsel sıkıştırma (JPEG kalite ayarı) büyük bölgeler için eklenebilir.
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
// data-narrate-speech-url → Documents/NarrateOcrSpeech (OCR kutusu metni JSON { text } ile gider).
const narrateSpeechUrl = shell.dataset.narrateSpeechUrl || "";
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

// [TR] Eski "Bölge Seçimi Başlat / Onayla" düğmeleri kaldırıldı.
//      Yalnızca "Seçimi Temizle" + yeni "Görsel Seç" butonları var.
const btnCancelSelection = document.getElementById("btn-cancel-selection");
const btnExtract = document.getElementById("btn-extract-text");
const btnSelectImage = document.getElementById("btn-select-image");
const btnSaveOcrText = document.getElementById("btn-save-ocr-text");
// OCR metnini Gemini TTS ile çalar (Details.cshtml id).
const btnNarrateOcrSpeech = document.getElementById("btn-narrate-ocr-speech");
const ocrTextarea = document.getElementById("ocr-text-placeholder");
const ocrStatus = document.getElementById("ocr-status");

// [TR] Görsel önizleme paneli ve "Kaldır" butonu (Görsel Seç ile yakalanan PNG).
const capturedImagePanel = document.getElementById("captured-image-panel");
const capturedImagePreview = document.getElementById("captured-image-preview");
const btnClearCapturedImage = document.getElementById("btn-clear-captured-image");

// [TR] OCR motoru seçici (Tesseract / PaddleOCR). Sunucuya request.Engine olarak gönderilir.
const ocrEngineSelect = document.getElementById("ocr-engine");
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
const aiResultLink = document.getElementById("ai-result-link");

// [TR] WhatsApp tarzı sohbet alanı — mesajlar burada görüntülenir.
// [TR] Sohbet listesi DOM’da #workspace-side-panel üstünde; ID’ler sabit.
const chatWindow = document.getElementById("ai-chat-window");
const chatEmpty = document.getElementById("ai-chat-empty");
const chatCount = document.getElementById("ai-chat-count");
const btnClearChat = document.getElementById("btn-clear-chat");
let chatMessageCount = 0;

// ─── OCR → son ses blob URL’ü (bellek sızıntısını önlemek için yeni istekte revoke) ───
// [TR] narrateAudioEl: Üzerinde ended ile durum satırını temizlemek ve üst üste oynatmayı kesmek için tutulur.
// MODIFICATION NOTES (TR): İleride tek Audio örneği + stop() ile güncellenebilir.
let narrateObjectUrl = null;
let narrateAudioEl = null;

function revokeNarrateObjectUrl() {
  if (narrateObjectUrl) {
    URL.revokeObjectURL(narrateObjectUrl);
    narrateObjectUrl = null;
  }
}

const elPage = document.getElementById("region-page");
const elX = document.getElementById("region-x");
const elY = document.getElementById("region-y");
const elW = document.getElementById("region-width");
const elH = document.getElementById("region-height");

let pdfDoc = null;
let currentPage = Number(shell.dataset.currentPage || "1");
let zoom = Number(shell.dataset.zoom || "100") / 100;

// [TR] Bölge seçimi her zaman aktif; ayrı bir "selection mode" toggle'ına ihtiyaç yok.
let drawing = false;
let startX = 0;
let startY = 0;
let currentRect = null;

// [TR] "Görsel Seç" ile yakalanmış bölge (base64 PNG). AI isteğinde gönderilir.
let capturedImage = null; // { base64: string (no data: prefix), mimeType: "image/png" }

function showFallbackWarning() {
  if (warning) warning.classList.remove("d-none");
}

function openNativePreview() {
  if (nativePreview) nativePreview.classList.remove("d-none");
}

function setRegionInfo(region) {
  if (!elPage || !elX || !elY || !elW || !elH) return;
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
  // [TR] Seçim her zaman aktif; ilgili düğmeler yalnızca seçim olup olmadığına göre aç/kapa.
  const hasSelection = !!currentRect;
  if (btnCancelSelection) btnCancelSelection.disabled = !hasSelection;
  if (btnExtract) btnExtract.disabled = !hasSelection;
  if (btnSelectImage) btnSelectImage.disabled = !hasSelection;
  if (!hasSelection && !lastOcrResultId && btnSaveOcrText) {
    btnSaveOcrText.disabled = true;
  }
}

/**
 * [TR] Durum satırı ve sohbet balonlarında innerHTML kullanıldığında metin kaçışı.
 */
function escapeHtml(s) {
  if (s == null) return "";
  return String(s)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

// ─── OCR / AI DURUM SATIRI (#ocr-status, #ai-status) ──────────────────────
// [TR] Ne işe yarar: Kısa bilgi, hata ve "yükleniyor" (spinner) göstergesi — kullanıcı uzun isteklerde boş ekran görmez.
// MODIFICATION NOTES (TR)
// - { loading: true } ile innerHTML + spinner (XSS için mesajlar escapeHtml ile kaçışlı).
// - clearOcrStatus: TTS bittiğinde veya satırın tamamen kaldırılması gerektiğinde (d-none + temizlik).

/**
 * [TR] #ocr-status içeriğini kaldırır ve gizler (ses oynatımı bittiğinde veya boş göstermek için).
 */
function clearOcrStatus() {
  if (!ocrStatus) return;
  ocrStatus.classList.add("d-none");
  ocrStatus.classList.remove("workspace-status-loading");
  ocrStatus.textContent = "";
  ocrStatus.innerHTML = "";
}

/**
 * [TR] #ocr-status: bilgi (info) / başarı / hata metni veya { loading: true } ile spinner + metin.
 * [TR] Ne işe yarar: OCR çıkarma, TTS, kaydetme sırasında görsel geri bildirim (site.css .workspace-status-loading).
 */
function setOcrStatus(type, message, options = {}) {
  const loading = options.loading === true;
  if (!ocrStatus) return;
  ocrStatus.classList.remove("d-none", "text-success", "text-danger", "text-muted", "workspace-status-loading");
  if (type === "success") ocrStatus.classList.add("text-success");
  else if (type === "error") ocrStatus.classList.add("text-danger");
  else ocrStatus.classList.add("text-muted");

  if (!message && !loading) {
    clearOcrStatus();
    return;
  }

  if (loading) {
    ocrStatus.classList.add("workspace-status-loading");
    ocrStatus.innerHTML =
      '<span class="spinner-border spinner-border-sm me-2 align-middle workspace-status-spinner" role="status" aria-live="polite" aria-busy="true"></span>' +
      "<span>" +
      escapeHtml(message) +
      "</span>";
    return;
  }

  ocrStatus.textContent = message;
}

/**
 * [TR] #ai-status: NLP/AI paneli kısa mesajı; setOcrStatus ile aynı loading sözleşmesi (spinner + escapeHtml).
 * MODIFICATION NOTES (TR): Başarı/hata sonrası kullanıcı özet mesajını görür; fetch sırasında spinner.
 */
function setAiStatus(type, message, options = {}) {
  const loading = options.loading === true;
  if (!aiStatus) return;
  aiStatus.classList.remove("d-none", "text-success", "text-danger", "text-muted", "workspace-status-loading");
  if (type === "success") aiStatus.classList.add("text-success");
  else if (type === "error") aiStatus.classList.add("text-danger");
  else aiStatus.classList.add("text-muted");

  if (loading) {
    aiStatus.classList.add("workspace-status-loading");
    aiStatus.innerHTML =
      '<span class="spinner-border spinner-border-sm me-2 align-middle workspace-status-spinner" role="status" aria-live="polite" aria-busy="true"></span>' +
      "<span>" +
      escapeHtml(message) +
      "</span>";
    return;
  }

  aiStatus.textContent = message;
}

// ─── SOHBET YARDIMCILARI (WhatsApp tarzı balonlar, #ai-chat-window sağ panelde) ───
// [TR] Ne işe yarar: Kullanıcı gönderisi + AI cevabı DOM’a eklenir; sohbet geçmişi sunucuda kalıcı tutulmaz (oturumdaki sayfa).
// [TR] Balon yapısı: kullanıcı mesajı — görsel + metin + isteğe bağlı prompt + reaction chip’ler (işlem/model/stil);
//      AI mesajı — metin, isteğe bağlı görsel veya hata.
// MODIFICATION NOTES (TR)
// - Uzun metin: kısaltma + "Devamını göster"; görseller base64 özetlenmez (büyük payload uyarısı).
// - COLLAPSE_* eşikleri aşağıda sabit.

function ensureChatVisible() {
  if (chatEmpty && !chatEmpty.classList.contains("d-none")) {
    chatEmpty.classList.add("d-none");
  }
}

function updateChatCount() {
  if (chatCount) chatCount.textContent = `${chatMessageCount} mesaj`;
}

function scrollChatToBottom() {
  if (chatWindow) chatWindow.scrollTop = chatWindow.scrollHeight;
}

// [TR] Uzun mesaj balonları için kısaltılmış metin + "Devamını göster" kutusu.
//      Eşik: ~500 karakter VEYA ~6 satır. Eşiği aşan metin
//      .ai-chat-bubble-text--collapsible sınıfıyla kısıtlanır ve kullanıcı
//      butona basınca .is-expanded ile tam metin açılır.
const COLLAPSE_CHAR_THRESHOLD = 500;
const COLLAPSE_LINE_THRESHOLD = 6;

function shouldCollapseText(text) {
  if (!text) return false;
  if (text.length > COLLAPSE_CHAR_THRESHOLD) return true;
  const lines = text.split("\n").length;
  return lines > COLLAPSE_LINE_THRESHOLD;
}

function renderCollapsibleText(text, { muted = false } = {}) {
  const safe = escapeHtml(text);
  if (!shouldCollapseText(text)) {
    const cls = muted ? "ai-chat-bubble-text text-muted" : "ai-chat-bubble-text";
    return `<p class="${cls}">${safe}</p>`;
  }
  const cls = muted
    ? "ai-chat-bubble-text ai-chat-bubble-text--collapsible text-muted"
    : "ai-chat-bubble-text ai-chat-bubble-text--collapsible";
  // [TR] Toggle mantığı DOM inşa edildikten sonra event delegation ile takılır
  //      (bkz. attachShowMoreHandler). İlk durumda collapsed, buton "Devamını göster"
  return (
    `<p class="${cls}">${safe}</p>` +
    `<button type="button" class="ai-chat-show-more" data-role="chat-show-more">Devamını göster</button>`
  );
}

// [TR] Tek bir event delegation: chat-window içindeki "Devamını göster" butonları
//      hep aynı davranır → bir kere takılır, yeni eklenen balonlarda da çalışır.
function attachShowMoreHandler() {
  if (!chatWindow || chatWindow.dataset.showMoreBound === "1") return;
  chatWindow.dataset.showMoreBound = "1";
  chatWindow.addEventListener("click", (ev) => {
    const btn = ev.target?.closest?.('[data-role="chat-show-more"]');
    if (!btn) return;
    const bubble = btn.previousElementSibling;
    if (!bubble?.classList?.contains("ai-chat-bubble-text--collapsible")) return;
    const expanded = bubble.classList.toggle("is-expanded");
    btn.textContent = expanded ? "Daha az göster" : "Devamını göster";
    // Expanded değişince scroll durumu bozulmasın — kullanıcı zaten balonun
    // başında değilse sohbeti olduğu yerde tut.
  });
}

// ─── AI SOHBET: BELGE BAŞINA YEREL SAKLAMA (localStorage) ────────────────────
// [TR] "Detaylı sonucu aç" ile başka sayfaya gidip geri gelindiğinde veya sayfa
//      yenilendiğinde sohbet sıfırlanmasın diye tamamlanmış mesaj balonları
//      tarayıcıda documentId anahtarıyla saklanır. Yalnızca "Temizle" ile silinir.
//      İhlali tamamlanmamış "yazıyor..." balonları kaydedilmez (filtre).
const AI_CHAT_STORAGE_PREFIX = "pdf_bitirme_ai_chat_v1";

function getAiChatStorageKey() {
  return `${AI_CHAT_STORAGE_PREFIX}:${documentId}`;
}

function persistAiChat() {
  if (!documentId || !chatWindow) return;
  const msgs = [...chatWindow.querySelectorAll(".ai-chat-message")].filter(
    (n) => !n.querySelector(".ai-chat-typing")
  );
  const html = msgs.map((n) => n.outerHTML).join("");
  try {
    const key = getAiChatStorageKey();
    if (!html.trim()) {
      localStorage.removeItem(key);
      return;
    }
    localStorage.setItem(key, JSON.stringify({ v: 1, html }));
  } catch (e) {
    console.warn("AI sohbet yerel depoya yazılamadı:", e);
  }
}

function restoreAiChat() {
  if (!documentId || !chatWindow) return;
  let raw = null;
  try {
    raw = localStorage.getItem(getAiChatStorageKey());
  } catch {
    return;
  }
  if (!raw) return;
  let data = null;
  try {
    data = JSON.parse(raw);
  } catch {
    return;
  }
  if (!data || typeof data.html !== "string" || !data.html.trim()) return;

  chatWindow.innerHTML = data.html;
  chatMessageCount = chatWindow.querySelectorAll(".ai-chat-message").length;
  updateChatCount();
  scrollChatToBottom();
}

function buildReactionChips(reactions) {
  if (!reactions || reactions.length === 0) return "";
  const items = reactions
    .filter((r) => r && r.label)
    .map((r) => {
      const cls = r.kind ? `ai-chat-reaction--${r.kind}` : "";
      return `<span class="ai-chat-reaction ${cls}">${escapeHtml(r.label)}</span>`;
    })
    .join("");
  return `<div class="ai-chat-reactions">${items}</div>`;
}

// [TR] Kullanıcı mesajı ekle. Görsel ve/veya metin + opsiyonel prompt + reactions.
function appendUserMessage({ imageDataUrl, contentText, prompt, reactions }) {
  if (!chatWindow) return null;
  ensureChatVisible();

  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--user";

  const inner = [];
  inner.push('<div class="ai-chat-bubble-wrap">');
  inner.push('<div class="ai-chat-bubble">');

  // [TR] Önce görsel (varsa).
  if (imageDataUrl) {
    inner.push(`<img src="${imageDataUrl}" alt="Gönderilen görsel" />`);
  }

  // [TR] Sonra metin (görsel/OCR/serbest metin) — uzunsa "Devamını göster" ile kısalt.
  if (contentText && contentText.trim().length > 0) {
    if (imageDataUrl) inner.push('<div style="height:0.45rem"></div>');
    inner.push(renderCollapsibleText(contentText));
  } else if (!imageDataUrl) {
    inner.push('<p class="ai-chat-bubble-text text-muted"><em>(içerik yok)</em></p>');
  }

  // [TR] Promptu kesik çizgiyle ayır ve "PROMPT:" etiketi ile göster.
  if (prompt && prompt.trim().length > 0) {
    inner.push('<hr class="ai-chat-divider" />');
    inner.push('<div class="ai-chat-prompt-label">Prompt</div>');
    inner.push(renderCollapsibleText(prompt));
  }

  inner.push("</div>"); // bubble
  inner.push(buildReactionChips(reactions));
  inner.push("</div>"); // bubble-wrap

  wrap.innerHTML = inner.join("");
  chatWindow.appendChild(wrap);
  chatMessageCount += 1;
  updateChatCount();
  attachShowMoreHandler();
  persistAiChat();
  scrollChatToBottom();
  return wrap;
}

// [TR] AI yazıyor... bekleme balonu — istek tamamlandığında kaldırılır/ değiştirilir.
function appendAiTypingPlaceholder() {
  if (!chatWindow) return null;
  ensureChatVisible();

  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--ai";
  wrap.innerHTML = `
    <div class="ai-chat-bubble-wrap">
      <div class="ai-chat-bubble">
        <span class="ai-chat-typing"><span></span><span></span><span></span></span>
      </div>
    </div>`;
  chatWindow.appendChild(wrap);
  scrollChatToBottom();
  return wrap;
}

// [TR] Bekleme balonunu gerçek AI cevabıyla değiştir veya yeni bir balon ekle.
function replaceWithAiMessage(typingNode, { text, imageUrl, isError, resultUrl }) {
  const wrap = typingNode || (() => {
    const el = document.createElement("div");
    el.className = "ai-chat-message ai-chat-message--ai";
    chatWindow?.appendChild(el);
    return el;
  })();

  const parts = [];
  parts.push('<div class="ai-chat-bubble-wrap">');
  parts.push(`<div class="ai-chat-bubble" ${isError ? 'style="background:#fee2e2;border-color:#fecaca;color:#7f1d1d"' : ""}>`);

  if (text && text.trim().length > 0) {
    parts.push(renderCollapsibleText(text));
  }
  if (imageUrl) {
    if (text) parts.push('<div style="height:0.45rem"></div>');
    parts.push(`<img src="${escapeHtml(imageUrl)}" alt="AI cevabı (görsel)" />`);
  }
  if (!text && !imageUrl) {
    parts.push('<p class="ai-chat-bubble-text text-muted"><em>(boş cevap)</em></p>');
  }

  parts.push("</div>"); // bubble

  if (resultUrl) {
    parts.push(`<div class="ai-chat-meta"><a class="link-primary" href="${escapeHtml(resultUrl)}">Detaylı sonucu aç →</a></div>`);
  }
  parts.push("</div>"); // wrap

  wrap.innerHTML = parts.join("");
  chatMessageCount += 1;
  updateChatCount();
  attachShowMoreHandler();
  persistAiChat();
  scrollChatToBottom();
  return wrap;
}

if (btnClearChat) {
  btnClearChat.addEventListener("click", () => {
    if (!chatWindow) return;
    try {
      localStorage.removeItem(getAiChatStorageKey());
    } catch {
      /* ignore */
    }
    chatWindow.innerHTML = "";
    chatMessageCount = 0;
    updateChatCount();
    if (chatEmpty) {
      const clone = chatEmpty.cloneNode(true);
      clone.classList.remove("d-none");
      chatWindow.appendChild(clone);
    }
  });
}

function clearDraftRect() {
  currentRect = null;
  rect.classList.add("d-none");
  rect.style.left = "";
  rect.style.top = "";
  rect.style.width = "";
  rect.style.height = "";
  // [TR] Bölge temizlenince koordinat panelini de sıfırla.
  setRegionInfo(null);
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

  // [TR] Çizim sırasında koordinatları canlı olarak göster;
  //      kullanıcı "Seçimi Onayla" basmadan da değerleri görebilir.
  setRegionInfo({ pageNumber: currentPage, x: left, y: top, width, height });
}

async function renderPage(pageNumber) {
  // [TR] Önce pdf.js (tarayıcı içi) ile render etmeyi dene — server tarafında
  //      pdftoppm.exe bağımlılığı yok. pdf.js herhangi bir sebeple başarısız
  //      olursa server tarafındaki PagePreview endpoint'ine düşeriz.
  //      Bu sıra kullanıcının pdftoppm kurmadığı makinelerde 500 hatası atmayı
  //      engeller (önceki davranış: önce server → her seferinde Win32Exception).
  let rendered = false;
  if (pdfDoc) {
    try {
      const page = await pdfDoc.getPage(pageNumber);
      const viewport = page.getViewport({ scale: zoom });
      canvas.width = Math.floor(viewport.width);
      canvas.height = Math.floor(viewport.height);
      // [TR] Overlay ve rect artık .workspace-viewer-canvas-wrap içinde
      //      absolute positioned; overlay CSS'te width/height: 100% → canvas
      //      boyutuyla otomatik senkronize. Rect'in JS'te maxWidth/maxHeight
      //      atamasına da gerek yok çünkü wrap canvas kadar geniş.
      rect.style.maxWidth = `${canvas.width}px`;
      rect.style.maxHeight = `${canvas.height}px`;

      const ctx = canvas.getContext("2d");
      await page.render({ canvasContext: ctx, viewport }).promise;
      rendered = true;
    } catch (err) {
      console.warn("pdf.js render hatası, server fallback denenecek:", err);
    }
  }

  if (!rendered) {
    await renderPageFromServer(pageNumber);
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
    // [TR] Overlay boyutu CSS tarafından %100 ile yönetiliyor (wrap → canvas).
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

// [TR] Sol fareye basıldığında bölge çizimi başlar — eski "Bölge Seçimi Başlat"
//      modu kaldırıldı; selection her zaman aktiftir.
overlay.addEventListener("mousedown", (evt) => {
  if (evt.button !== 0) return; // sadece sol tık
  drawing = true;
  const p = normalizeMousePosition(evt);
  startX = p.x;
  startY = p.y;
  drawDraftRect(startX, startY, startX, startY);
  setSelectionUi();
  // [TR] Sayfa içi metin/element seçimini engelle (sürükleme sırasında istenmeyen highlight).
  evt.preventDefault();
});

window.addEventListener("mousemove", (evt) => {
  if (!drawing) return;
  const p = normalizeMousePosition(evt);
  drawDraftRect(startX, startY, p.x, p.y);
  setSelectionUi();
});

window.addEventListener("mouseup", () => {
  if (!drawing) return;
  drawing = false;
  // [TR] Çizim bittiğinde minik (yanlışlıkla) seçimleri at.
  if (currentRect && (currentRect.width < 4 || currentRect.height < 4)) {
    clearDraftRect();
  }
  setSelectionUi();
});

// [TR] "Seçimi Temizle" — bölgeyi ve panel bilgisini sıfırlar.
//      Yakalanmış görsel ayrı yönetiliyor (bkz. btn-clear-captured-image).
if (btnCancelSelection) {
  btnCancelSelection.addEventListener("click", () => {
    clearDraftRect();
    setSelectionUi();
    setRegionInfo(null);
  });
}

// [TR] Klavye kısayolu: Esc ile aktif seçimi temizle.
window.addEventListener("keydown", (evt) => {
  if (evt.key === "Escape" && currentRect) {
    clearDraftRect();
    setSelectionUi();
    setRegionInfo(null);
  }
});

btnExtract.addEventListener("click", () => {
  if (!currentRect || !extractUrl || !documentId) return;
  const ow = overlay.clientWidth || canvas.width || 1;
  const oh = overlay.clientHeight || canvas.height || 1;
  const selectedEngine = ocrEngineSelect ? ocrEngineSelect.value : null;

  // [TR] Tarayıcı, render edilmiş canvas'tan seçili bölgeyi kırpıp PNG olarak gönderir.
  //      Böylece sunucu pdftoppm/Poppler kurulumuna ihtiyaç duymaz; OCR motoru
  //      görüntüyü doğrudan alır. captureSelectionAsImage() "Görsel Seç" ile
  //      aynı yardımcıyı kullanır (DPI ölçeği, sınır kontrolü).
  const cropped = captureSelectionAsImage();

  const payload = {
    documentId,
    region: {
      pageNumber: currentRect.pageNumber,
      x: currentRect.left / ow,
      y: currentRect.top / oh,
      width: currentRect.width / ow,
      height: currentRect.height / oh,
    },
    // [TR] Seçili OCR motoru: "Tesseract" veya "Paddle". Sunucu motora göre yönlendirir.
    engine: selectedEngine,
    // [TR] Önceden kırpılmış PNG; sunucu base64 prefix'ini de kabul eder.
    imageBase64: cropped ? cropped.base64 : null,
  };

  // [TR] Sunucu OCR (Tesseract/Paddle) uzun sürebilir — #ocr-status üzerinde spinner gösterilir.
  btnExtract.disabled = true;
  setOcrStatus("info", `OCR çalışıyor... (${selectedEngine || "varsayılan"})`, {
    loading: true,
  });
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

// ─── "GÖRSEL SEÇ" — SEÇİLİ BÖLGEYİ PNG OLARAK YAKALA ───────────────────────
// [TR] Seçili dikdörtgenin canvas üzerindeki piksellerini ayrı bir canvas'a
//      kırpıp base64 PNG'ye dönüştürür. Sonuç AI panelinde önizlenir ve
//      "AI ile İşle" tıklandığında inputImageBase64 olarak gönderilir.
//
// JÜRI MODİFİKASYON NOTLARI (TR)
// - Devicepixel oranı için ekstra ölçek uygulanmaz; canvas zaten render
//   edilmiş piksel boyutuna sahip (server-side preview veya pdf.js).
// - Çok büyük bölgelerde JPEG sıkıştırma + kalite parametresi eklenebilir.
function showCapturedImagePreview(dataUrl) {
  if (!capturedImagePanel || !capturedImagePreview) return;
  capturedImagePreview.src = dataUrl;
  capturedImagePanel.classList.remove("d-none");
}

function clearCapturedImage() {
  capturedImage = null;
  if (capturedImagePreview) capturedImagePreview.src = "";
  if (capturedImagePanel) capturedImagePanel.classList.add("d-none");
}

function captureSelectionAsImage() {
  if (!currentRect || !canvas) return null;

  // [TR] Overlay (DOM piksel) ile canvas (gerçek piksel) farklı olabilir;
  //      seçimi canvas piksellerine ölçeklemek için oranı hesapla.
  const ow = overlay.clientWidth || canvas.width || 1;
  const oh = overlay.clientHeight || canvas.height || 1;
  const scaleX = canvas.width / ow;
  const scaleY = canvas.height / oh;

  const sx = Math.max(0, Math.round(currentRect.left * scaleX));
  const sy = Math.max(0, Math.round(currentRect.top * scaleY));
  const sw = Math.min(canvas.width - sx, Math.max(1, Math.round(currentRect.width * scaleX)));
  const sh = Math.min(canvas.height - sy, Math.max(1, Math.round(currentRect.height * scaleY)));

  const off = document.createElement("canvas");
  off.width = sw;
  off.height = sh;
  const offCtx = off.getContext("2d");
  if (!offCtx) return null;
  offCtx.drawImage(canvas, sx, sy, sw, sh, 0, 0, sw, sh);

  const dataUrl = off.toDataURL("image/png");
  // [TR] data: prefix'ini ayır; sunucu sadece ham base64 + mimeType bekliyor.
  const commaIdx = dataUrl.indexOf(",");
  const base64 = commaIdx >= 0 ? dataUrl.slice(commaIdx + 1) : dataUrl;
  return { base64, mimeType: "image/png", dataUrl };
}

if (btnSelectImage) {
  btnSelectImage.addEventListener("click", () => {
    if (!currentRect) {
      setOcrStatus("error", "Önce PDF üzerinde bir bölge seçin.");
      return;
    }
    const captured = captureSelectionAsImage();
    if (!captured) {
      setOcrStatus("error", "Görsel yakalanamadı.");
      return;
    }
    capturedImage = { base64: captured.base64, mimeType: captured.mimeType };
    showCapturedImagePreview(captured.dataUrl);
    setOcrStatus("success", "Görsel yakalandı. AI ile İşle tıklayarak prompt ile gönderebilirsiniz.");
  });
}

if (btnClearCapturedImage) {
  btnClearCapturedImage.addEventListener("click", () => {
    clearCapturedImage();
  });
}

if (btnSaveOcrText) {
  btnSaveOcrText.disabled = !lastOcrResultId;
  btnSaveOcrText.addEventListener("click", () => {
    if (!saveOcrUrl || !lastOcrResultId || !ocrTextarea) return;
    btnSaveOcrText.disabled = true;
    setOcrStatus("info", "OCR metni kaydediliyor...", { loading: true });
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

// ─── OCR metni Gemini TTS seslendirme (sunucu proxy; Paddle/Tesseract OCR’dan ayrı) ───
// [TR] Textarea içeriği JSON { text } ile gider; sunucu Gemini’den ses alır — istemci blob ile çalar.
// [TR] Spinner: API + oynatma boyunca; audio "ended" → clearOcrStatus (mesaj kalıntısı yok).
// MODIFICATION NOTES (TR): Görünür oynatıcı veya indir bağlantısı ileride eklenebilir.
if (btnNarrateOcrSpeech && ocrTextarea) {
  btnNarrateOcrSpeech.addEventListener("click", async () => {
    const text = (ocrTextarea.value || "").trim();
    if (!narrateSpeechUrl) {
      setOcrStatus("error", "Seslendirme uç noktası yapılandırılmadı.");
      return;
    }
    if (!text) {
      setOcrStatus("error", "Önce OCR çıktısı oluşturun veya metin yazın.");
      return;
    }
    btnNarrateOcrSpeech.disabled = true;
    if (narrateAudioEl) {
      try {
        narrateAudioEl.pause();
      } catch {
        /* yoksay */
      }
      narrateAudioEl.onended = null;
      narrateAudioEl.removeAttribute("src");
      narrateAudioEl = null;
    }
    revokeNarrateObjectUrl();
    setOcrStatus("info", "Metin Gemini TTS ile seslendiriliyor...", { loading: true });
    try {
      const r = await fetch(narrateSpeechUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: antiForgeryToken,
        },
        body: JSON.stringify({ text }),
      });
      const ct = (r.headers.get("content-type") || "").toLowerCase();
      if (!r.ok) {
        let msg = "Seslendirme başarısız.";
        try {
          if (ct.includes("application/json")) {
            const j = await r.json();
            if (j?.message) msg = j.message;
          } else {
            const t = await r.text();
            if (t) msg = t.slice(0, 400);
          }
        } catch {
          /* yoksay */
        }
        setOcrStatus("error", msg);
        return;
      }
      if (!ct.includes("audio")) {
        setOcrStatus("error", "Beklenmeyen yanıt (ses dosyası değil).");
        return;
      }
      const blob = await r.blob();
      narrateObjectUrl = URL.createObjectURL(blob);
      const audioEl = new Audio(narrateObjectUrl);
      narrateAudioEl = audioEl;
      audioEl.onended = () => {
        clearOcrStatus();
        revokeNarrateObjectUrl();
        audioEl.onended = null;
        if (narrateAudioEl === audioEl) narrateAudioEl = null;
      };

      setOcrStatus("info", "Ses oynatılıyor...", { loading: true });

      try {
        await audioEl.play();
      } catch (playErr) {
        const pm =
          playErr && typeof playErr.message === "string"
            ? playErr.message
            : "Tarayıcı sesi çalamadı (ör. biçim desteklenmiyor).";
        setOcrStatus(
          "error",
          pm.length > 240 ? `${pm.slice(0, 240)}…` : pm
        );
        audioEl.onended = null;
        revokeNarrateObjectUrl();
        if (narrateAudioEl === audioEl) narrateAudioEl = null;
        return;
      }
    } catch (e) {
      const msg =
        e && typeof e.message === "string"
          ? e.message
          : "Seslendirme isteği sırasında hata oluştu.";
      setOcrStatus(
        "error",
        msg.length > 240 ? `${msg.slice(0, 240)}…` : msg
      );
    } finally {
      btnNarrateOcrSpeech.disabled = false;
    }
  });
}

if (btnAiProcess) {
  btnAiProcess.addEventListener("click", () => {
    if (!aiProcessUrl || !documentId || !ocrTextarea) return;
    const inputText = (ocrTextarea.value || "").trim();
    const customInstruction = (aiInstruction?.value || "").trim();
    const hasImage = !!capturedImage;

    // [TR] Eskiden sadece OCR metni zorunluydu. Artık üç kaynaktan biri yeterli:
    //      1) OCR/serbest metin, 2) "Görsel Seç" ile yakalanmış görsel, veya
    //      3) "Özel Yönerge" alanına yazılmış prompt.
    if (!inputText && !hasImage && !customInstruction) {
      setAiStatus(
        "error",
        "OCR metni, yakalanmış bir görsel veya özel yönerge gerekli."
      );
      return;
    }

    const operation = aiOperation?.value || "Translate";
    const model = aiModel?.value || "mock-gpt";
    const style = aiStyle?.value || "Formal";
    const targetLang = aiTargetLanguage?.value || "English";

    const payload = {
      documentId,
      operationType: operation,
      modelName: model,
      // [TR] Kaynak dil kaldırıldı; AI prompt'unda kaynak dil "otomatik algıla" olarak geçer.
      targetLanguage: targetLang,
      style,
      customInstruction,
      inputText,
      sourcePageNumber: currentRect?.pageNumber || currentPage,
      // [TR] "Görsel Seç" ile yakalanmış görsel varsa multimodal input olarak ekle.
      //      Sunucu (Gemini) bu alanı inlineData (mimeType + data) olarak Gemini API'ye iletir.
      inputImageBase64: hasImage ? capturedImage.base64 : null,
      inputImageMimeType: hasImage ? capturedImage.mimeType : null,
    };

    // ─── KULLANICI MESAJI: sohbet alanına ekle ─────────────────────────────
    // [TR] Reaction chip'ler: WhatsApp emoji reaksiyonları gibi balon altında
    //      görünür. Operation + Model + Style (+ Translate ise Hedef Dil) ekle.
    //      Görsel önizlemesi için Görsel Seç balonundaki dataUrl kullanılır;
    //      o yüzden aşağıda capturedImagePreview.src tercih edilir.
    const reactions = [
      { kind: "operation", label: operation },
      { kind: "model",     label: model },
      { kind: "style",     label: style },
    ];
    if (operation === "Translate") {
      reactions.push({ kind: "lang", label: `→ ${targetLang}` });
    }

    const userImageDataUrl = hasImage && capturedImagePreview ? capturedImagePreview.src : null;

    appendUserMessage({
      imageDataUrl: userImageDataUrl,
      contentText: inputText,
      prompt: customInstruction,
      reactions,
    });

    // ─── AI mesajı için "yazıyor..." balonu ekle ───────────────────────────
    const typingNode = appendAiTypingPlaceholder();

    // [TR] NLP görevi: ağ gecikmesi uzun olabildiği için #ai-status satırında spinner (+ "AI işlemi çalışıyor...").
    btnAiProcess.disabled = true;
    if (aiResultLink) aiResultLink.classList.add("d-none");
    setAiStatus("info", "AI işlemi çalışıyor...", { loading: true });

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
          replaceWithAiMessage(typingNode, {
            text: res.message || "AI işlemi başarısız.",
            isError: true,
          });
          return;
        }
        setAiStatus("success", res.message || "AI işlemi tamamlandı.");
        replaceWithAiMessage(typingNode, {
          text: res.outputText || "",
          imageUrl: res.outputImageUrl || null,
          resultUrl: res.resultUrl || null,
        });
        if (aiResultLink && res.resultUrl) {
          aiResultLink.href = res.resultUrl;
          aiResultLink.classList.remove("d-none");
        }
      })
      .catch(() => {
        setAiStatus("error", "AI isteği sırasında hata oluştu.");
        replaceWithAiMessage(typingNode, {
          text: "AI isteği sırasında hata oluştu.",
          isError: true,
        });
      })
      .finally(() => {
        btnAiProcess.disabled = false;
      });
  });
}

// ─── MODEL LİSTESİ DİNAMİK YÜKLEME ──────────────────────────────────────────
// [TR] Seçilen task'e göre /Ai/ModelsForTask?task={task} endpoint'i çağrılır.
//      Sadece o task'i destekleyen modeller dropdown'a eklenir.
//      Bu sayede örneğin "Summarize" seçildiğinde Gemini image modeli görünmez.
//
// MODIFICATION NOTES (TR)
// - Yeni task eklendiğinde bu fonksiyon otomatik çalışır; güncelleme gerekmez.
// - Offline durumda: fetch başarısız olursa mevcut seçenekler korunur (sessiz hata).

const defaultModelFromAttr = aiModel?.getAttribute("data-default-model") ?? "";

async function loadModelsForTask(task) {
  if (!aiModel) return;

  try {
    const resp = await fetch(`/Ai/ModelsForTask?task=${encodeURIComponent(task)}`, {
      credentials: "same-origin"
    });

    if (!resp.ok) return;

    const models = await resp.json(); // [{ id, label, provider }]

    // [TR] Mevcut seçenekleri temizle, yeni listeyi ekle.
    aiModel.innerHTML = "";

    if (!models || models.length === 0) {
      aiModel.innerHTML = '<option value="">Bu işlem için model bulunamadı</option>';
      return;
    }

    models.forEach(m => {
      const opt = document.createElement("option");
      opt.value = m.id;
      // [TR] Provider bilgisi etikette parantez içinde gösterilir (Gemini / HuggingFace).
      opt.textContent = m.label;
      // [TR] Kullanıcının varsayılan modeli bu listede varsa otomatik seçilir.
      if (m.id === defaultModelFromAttr) opt.selected = true;
      aiModel.appendChild(opt);
    });

    // [TR] Varsayılan model listede yoksa ilk modeli seç.
    if (!aiModel.value && aiModel.options.length > 0) {
      aiModel.selectedIndex = 0;
    }

  } catch {
    // [TR] Ağ hatasında sessizce başarısız ol; mevcut dropdown değişmez.
  }
}

// [TR] Dil seçici satırını sadece "Translate" işlemi seçiliyken göster.
function syncTranslateLangsVisibility() {
  if (!aiTranslateLangs) return;
  const op = aiOperation ? aiOperation.value : "";
  aiTranslateLangs.style.display = op === "Translate" ? "" : "none";
}

// [TR] Math işlemi seçiliyken OCR önizleme alanına matematik dostu tipografi (CSS).
function syncMathOcrPreviewStyle() {
  if (!ocrTextarea || !aiOperation) return;
  const on = aiOperation.value === "Math";
  ocrTextarea.classList.toggle("workspace-ocr-text-math", on);
}

if (aiOperation) {
  aiOperation.addEventListener("change", () => {
    // [TR] İşlem değiştiğinde: önce model listesi güncellenir, sonra dil seçici ayarlanır.
    loadModelsForTask(aiOperation.value);
    syncTranslateLangsVisibility();
    syncMathOcrPreviewStyle();
  });
}

// Sayfa ilk yüklendiğinde de hizala
syncTranslateLangsVisibility();
syncMathOcrPreviewStyle();

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
    // [TR] Sayfa açılışında varsayılan işlem (Translate) için model listesi yüklenir.
    //      Kullanıcının en çok kullandığı model (defaultModelFromAttr) otomatik seçilir.
    await loadModelsForTask(aiOperation ? aiOperation.value : "Translate");
    if (aiStyle) aiStyle.value = defaultStyle;
  } catch (err) {
    console.error(err);
    empty.classList.remove("d-none");
    showFallbackWarning();
  }
}

// [TR] Sekme kapanmadan önce / başka sayfaya gidilirken son durumu yaz (geri tuşu vb.).
window.addEventListener("pagehide", () => {
  persistAiChat();
});

attachShowMoreHandler();
restoreAiChat();

init();

