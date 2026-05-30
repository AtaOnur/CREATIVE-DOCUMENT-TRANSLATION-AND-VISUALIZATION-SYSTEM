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
 *   4. Chat balonundaki "Seslendir" → metin sunucuya gönderilir; ses çıktısı AI sohbet balonu olarak oynatılır.
 *
 * MODIFICATION NOTES (TR)
 * - Çoklu seçim için selection listesi ve çizim katmanı genişletilebilir.
 * - Tam sayfa OCR ve annotation araçları eklenebilir.
 * - Metin overlay (textLayer) ileride açılabilir.
 * - Workspace durum satırları (#ocr-status ve varsa #ai-status): setOcrStatus / setAiStatus üçüncü argüman
 *   { loading: true } ile Bootstrap spinner-border-sm gösterir. AI işlemleri artık chat balonu içinden yürür.
 * - AI Sohbet (#ai-chat-window) sağ sidebar’ın üstünde; #region-page … #region-height üst toolbar’da (setRegionInfo).
 * - Seslendirme: chat içi ses balonu gösterilir; sunucu aynı çıktıyı dosya + AiResult olarak notebook'a kaydeder.
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
// data-narrate-speech-url → Documents/NarrateOcrSpeech (OCR kutusu metni JSON { documentId, text } ile gider).
const narrateSpeechUrl = shell.dataset.narrateSpeechUrl || "";
const aiProcessUrl = shell.dataset.aiProcessUrl || "";
const chatSaveUrl = shell.dataset.chatSaveUrl || "";
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
// [TR] Geriye dönük uyumluluk: harici TTS butonu kaldırıldı; varsa aynı chat içi ses akışını tetikler.
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

// ─── CREATIVE: yüzen panel + fonksiyon rayı + bölge popup'ı ──────────────────
// [TR] Yeni düzen elemanları (Details.cshtml). Eski yan panel artık PDF üstünde
//      yüzen, sola/sağa genişletilebilir bir panele dönüştü.
const workspaceBody = document.getElementById("workspace-body");
const overlayPanel = document.getElementById("workspace-overlay-panel");
const panelResizer = document.getElementById("workspace-panel-resizer");
const fnRail = document.getElementById("workspace-fn-rail");
const fnFlyout = document.getElementById("workspace-fn-flyout");
const regionPopup = document.getElementById("region-popup");
const composeEmpty = document.getElementById("workspace-compose-empty");
const canvasWrap = document.getElementById("workspace-viewer-canvas-wrap");
const workspaceCompose = document.getElementById("workspace-compose");

// [TR] CREATIVE (4. tur): seçilebilir kaynak panelleri + region toast + kopyala.
const ocrTextPanel = document.getElementById("ocr-text-panel");
const btnClearOcrText = document.getElementById("btn-clear-ocr-text");
const btnCopyCapturedImage = document.getElementById("btn-copy-captured-image");
const regionToast = document.getElementById("region-toast");
const regionToastText = document.getElementById("region-toast-text");

// [TR] Compose alanındaki iki kaynak: OCR metni ve yakalanan görsel. Hangisi
//      "seçili" ise fonksiyonlar onun üzerinde çalışır.
let textSourceId = null;
let imageSourceId = null;
let currentComposeKind = null; // "text" | "image" | null

// [TR] Fonksiyon rayının üzerinde çalışacağı "aktif kaynak" (son OCR metni veya
//      yakalanan görsel). Bölge popup'ından Extract/Select ile güncellenir.
let workspaceActiveSourceId = null;
// [TR] Bölge popup'ında seçilen OCR motoru (varsayılan PaddleOCR).
let selectedOcrEngine = "Paddle";

// ─── OCR → chat içi Gemini TTS oynatıcı yardımcıları ───
const NARRATE_SEEK_SECONDS = 10;

function formatNarrateTime(seconds) {
  if (!Number.isFinite(seconds) || seconds < 0) seconds = 0;
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, "0")}`;
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

function clearAiStatus() {
  if (!aiStatus) return;
  aiStatus.classList.add("d-none");
  aiStatus.classList.remove("text-success", "text-danger", "text-muted", "workspace-status-loading");
  aiStatus.textContent = "";
  aiStatus.innerHTML = "";
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
  if (chatCount) chatCount.textContent = `${chatMessageCount} msj`;
}

function scrollChatToBottom() {
  if (chatWindow) chatWindow.scrollTop = chatWindow.scrollHeight;
}

function removeChatMessage(msg) {
  if (!msg || !chatWindow) return;
  msg.remove();
  chatMessageCount = chatWindow.querySelectorAll(".ai-chat-message").length;
  updateChatCount();
  persistAiChat();
  if (chatMessageCount === 0 && chatEmpty) {
    const clone = chatEmpty.cloneNode(true);
    clone.classList.remove("d-none");
    chatWindow.appendChild(clone);
  }
}

function upgradeLegacyChatErrors(root = chatWindow) {
  root?.querySelectorAll(".ai-chat-message--ai").forEach((msg) => {
    const bubble = msg.querySelector(".ai-chat-bubble");
    if (!bubble) return;
    const inlineBg = bubble.getAttribute("style") || "";
    const isError =
      msg.classList.contains("ai-chat-message--error") ||
      inlineBg.includes("#fee2e2") ||
      inlineBg.includes("fee2e2");
    if (!isError) return;
    msg.classList.add("ai-chat-message--error");
    bubble.classList.add("ai-chat-bubble--error");
    if (inlineBg.includes("fee2e2")) bubble.removeAttribute("style");
    const wrap = msg.querySelector(".ai-chat-bubble-wrap");
    if (wrap && !wrap.querySelector('[data-role="chat-error-dismiss"]')) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "ai-chat-error-dismiss";
      btn.dataset.role = "chat-error-dismiss";
      btn.title = "Delete error";
      btn.textContent = "🗑 Delete";
      wrap.appendChild(btn);
    }
  });
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
    `<button type="button" class="ai-chat-show-more" data-role="chat-show-more">Show more</button>`
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
    btn.textContent = expanded ? "Show less" : "Show more";
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
// [TR] Views/Ai/Result + wwwroot/js/ai-result-chat-history.mjs aynı prefix + documentId ile okur — değişirse orası da güncellenmeli.

const AI_CHAT_OPERATION_DEFS = [
  { op: "Translate", label: "Translate", icon: "🌐", needsText: true },
  { op: "Summarize", label: "Summarize", icon: "🧾", needsText: true },
  { op: "Rewrite", label: "Rewrite", icon: "✍️", needsText: true },
  { op: "CreativeWrite", label: "Creative", icon: "✨", needsText: true },
  { op: "Explanation", label: "Explain", icon: "💡", needsAny: true },
  { op: "Visualize", label: "Visualize", icon: "🖼️", needsAny: true },
  { op: "Math", label: "Math", icon: "📈", needsAny: true },
  { op: "Narrate", label: "Narrate", icon: "🔊", needsText: true },
];
let aiChatSourceSeq = 0;
const aiChatSources = new Map();
let lastOcrChatSourceId = null;
let lastImageChatSourceId = null;

function getAiChatStorageKey() {
  return `${AI_CHAT_STORAGE_PREFIX}:${documentId}`;
}

function persistAiChat() {
  if (!documentId || !chatWindow) return;
  const msgs = [...chatWindow.querySelectorAll(".ai-chat-message")].filter(
    (n) =>
      !n.querySelector(".ai-chat-typing") &&
      !n.classList.contains("ai-chat-message--op-setup")
  );
  const html = msgs
    .map((n) => {
      const clone = n.cloneNode(true);
      clone.querySelectorAll(".ai-chat-voice-msg").forEach((v) => {
        delete v.dataset.voiceBound;
        const url = (v.dataset.audioUrl || "").trim();
        if (url.startsWith("blob:")) v.dataset.audioUrl = "";
      });
      return clone.outerHTML;
    })
    .join("");
  const sources = serializeAiChatSources();
  try {
    const key = getAiChatStorageKey();
    if (!html.trim()) {
      localStorage.removeItem(key);
      return;
    }
    localStorage.setItem(key, JSON.stringify({ v: 2, html, sources }));
  } catch (e) {
    console.warn("AI chat could not be written to local storage:", e);
  }
}

function serializeAiChatSources() {
  const out = {};
  collectChatSourceIds().forEach((id) => {
    const s = aiChatSources.get(id);
    if (!s) return;
    out[id] = {
      text: s.text || "",
      imageDataUrl: s.imageDataUrl || null,
      imageBase64: s.imageBase64 || null,
      imageMimeType: s.imageMimeType || null,
    };
  });
  return out;
}

function collectChatSourceIds() {
  const ids = new Set();
  chatWindow?.querySelectorAll(".ai-chat-message[data-source-id]").forEach((el) => {
    if (el.dataset.sourceId) ids.add(el.dataset.sourceId);
  });
  if (textSourceId) ids.add(textSourceId);
  if (imageSourceId) ids.add(imageSourceId);
  return ids;
}

function parseDataUrl(dataUrl) {
  const m = /^data:([^;]+);base64,(.+)$/.exec(dataUrl || "");
  if (!m) return { mime: null, base64: null };
  return { mime: m[1], base64: m[2] };
}

function cssEscape(value) {
  if (typeof CSS !== "undefined" && typeof CSS.escape === "function") return CSS.escape(value);
  return String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function rebuildSourceFromDom(sourceId) {
  if (!sourceId || !chatWindow) return false;
  const msg = chatWindow.querySelector(
    `.ai-chat-message--user[data-source-id="${cssEscape(sourceId)}"]`
  );
  if (!msg) return false;

  const textHost = msg.querySelector('[data-role="chat-source-text"]');
  const text = textHost ? textHost.innerText.replace(/\s+\n/g, "\n").trim() : "";
  const img = msg.querySelector(".ai-chat-bubble img");
  let imageDataUrl = null;
  let imageBase64 = null;
  let imageMimeType = null;
  if (img?.src) {
    imageDataUrl = img.src;
    if (img.src.startsWith("data:")) {
      const parsed = parseDataUrl(img.src);
      imageBase64 = parsed.base64;
      imageMimeType = parsed.mime;
    }
  }
  if (!text && !imageDataUrl) return false;

  aiChatSources.set(sourceId, { text, imageDataUrl, imageBase64, imageMimeType });
  return true;
}

function restoreAiChatSources(storedSources) {
  if (!storedSources || typeof storedSources !== "object") return;
  Object.entries(storedSources).forEach(([id, s]) => {
    if (!id || !s) return;
    aiChatSources.set(id, {
      text: s.text || "",
      imageDataUrl: s.imageDataUrl || null,
      imageBase64: s.imageBase64 || null,
      imageMimeType: s.imageMimeType || null,
    });
  });
}

function ensureSourceInMemory(sourceId) {
  if (!sourceId) return false;
  if (aiChatSources.has(sourceId)) return true;
  return rebuildSourceFromDom(sourceId);
}

function dismissPendingChatOpSetups() {
  if (!chatWindow) return;
  let removed = false;
  chatWindow.querySelectorAll(".ai-chat-message--op-setup").forEach((el) => {
    el.remove();
    removed = true;
  });
  if (removed) persistAiChat();
}

function saveChatMessageToServer({ role, messageType, text, imageUrl, audioUrl, resultUrl }) {
  if (!chatSaveUrl || !documentId) return;
  fetch(chatSaveUrl, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: antiForgeryToken,
    },
    body: JSON.stringify({
      documentId,
      role,
      messageType,
      text: text || "",
      imageUrl: imageUrl || "",
      audioUrl: audioUrl || "",
      resultUrl: resultUrl || "",
    }),
  }).catch(() => {
    // [TR] Admin gorunurlugu icin arka plan kaydi; sohbet akisini bozmasin.
  });
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
  chatWindow.querySelectorAll(".ai-chat-voice-msg").forEach((v) => {
    delete v.dataset.voiceBound;
    v._voicePlayer = null;
    v._audioEl = null;
  });
  upgradeLegacyVoiceMessages(chatWindow);
  upgradeLegacyChatErrors(chatWindow);
  if (data.v >= 2 && data.sources) {
    restoreAiChatSources(data.sources);
  }
  chatWindow.querySelectorAll(".ai-chat-message[data-source-id]").forEach((el) => {
    if (el.dataset.sourceId) ensureSourceInMemory(el.dataset.sourceId);
  });
  chatMessageCount = chatWindow.querySelectorAll(".ai-chat-message").length;
  updateChatCount();
  bindChatAudioPlayers();
  persistAiChat();
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

function buildChatOperationButtons(sourceId, { hasText, hasImage }) {
  if (!sourceId) return "";
  const buttons = AI_CHAT_OPERATION_DEFS
    // [TR] Metin veya görsel varsa tüm işlemler kullanılabilir (görsel de gönderilebilir).
    .filter(() => hasText || hasImage)
    .map(
      (d) =>
        `<button type="button" class="ai-chat-op-button" data-role="chat-op" data-source-id="${escapeHtml(sourceId)}" data-operation="${escapeHtml(d.op)}">` +
        `<span aria-hidden="true">${d.icon}</span><span>${escapeHtml(d.label)}</span>` +
        "</button>"
    )
    .join("");
  if (!buttons) return "";
  // [TR] İşlemler balonun İÇİNDE ayrı bir bölmede: çizgiyle ayrılır, başlık +
  //      katlanabilir (collapsed) buton ızgarası. Böylece "yeniden yapılabilir
  //      işlemler" ile "yapılmış işlem reaksiyonları" karışmaz; çok buton varken
  //      sohbeti kaplamaz, kullanıcı genişletince tümünü görür.
  return (
    '<hr class="ai-chat-divider ai-chat-ops-divider" />' +
    '<div class="ai-chat-ops">' +
    '<button type="button" class="ai-chat-ops__toggle" data-role="ops-toggle" aria-expanded="false">' +
    "<span>↻ Run another operation on this</span>" +
    '<span class="ai-chat-ops__chevron" aria-hidden="true">▾</span>' +
    "</button>" +
    `<div class="ai-chat-op-row ai-chat-op-row--collapsed" aria-label="AI operations">${buttons}</div>` +
    "</div>"
  );
}

function createAiChatSource({ contentText, imageDataUrl, imageBase64, imageMimeType }) {
  const sourceId = `chat-src-${Date.now()}-${++aiChatSourceSeq}`;
  aiChatSources.set(sourceId, {
    text: contentText || "",
    imageDataUrl: imageDataUrl || null,
    imageBase64: imageBase64 || null,
    imageMimeType: imageMimeType || null,
  });
  return sourceId;
}

function updateAiChatSourceText(sourceId, text) {
  if (!sourceId || !aiChatSources.has(sourceId)) return false;
  const source = aiChatSources.get(sourceId);
  source.text = text || "";
  aiChatSources.set(sourceId, source);

  const node = chatWindow?.querySelector(`[data-source-id="${sourceId}"]`);
  const textHost = node?.querySelector?.('[data-role="chat-source-text"]');
  if (textHost) {
    textHost.innerHTML = (text || "").trim()
      ? renderCollapsibleText(text)
      : '<p class="ai-chat-bubble-text text-muted"><em>(text is empty)</em></p>';
  }
  attachShowMoreHandler();
  persistAiChat();
  return true;
}

// [TR] Kullanıcı mesajı ekle. Görsel/metin + opsiyonel prompt + reactions + chat içi operasyon kısayolları.
function appendUserMessage({ imageDataUrl, contentText, prompt, reactions, sourceId }) {
  if (!chatWindow) return null;
  ensureChatVisible();

  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--user";
  if (sourceId) wrap.dataset.sourceId = sourceId;

  const inner = [];
  inner.push('<div class="ai-chat-bubble-wrap">');
  inner.push('<div class="ai-chat-bubble">');

  // [TR] Önce görsel (varsa).
  if (imageDataUrl) {
    inner.push(`<img src="${imageDataUrl}" alt="Submitted image" />`);
  }

  // [TR] Sonra metin (görsel/OCR/serbest metin) — uzunsa "Devamını göster" ile kısalt.
  if (contentText && contentText.trim().length > 0) {
    if (imageDataUrl) inner.push('<div style="height:0.45rem"></div>');
    inner.push(`<div data-role="chat-source-text" data-source-id="${escapeHtml(sourceId || "")}">`);
    inner.push(renderCollapsibleText(contentText));
    inner.push("</div>");
  } else if (!imageDataUrl) {
    inner.push('<p class="ai-chat-bubble-text text-muted"><em>(no content)</em></p>');
  }

  // [TR] Promptu kesik çizgiyle ayır ve "PROMPT:" etiketi ile göster.
  if (prompt && prompt.trim().length > 0) {
    inner.push('<hr class="ai-chat-divider" />');
    inner.push('<div class="ai-chat-prompt-label">Prompt</div>');
    inner.push(renderCollapsibleText(prompt));
  }

  // [TR] İşlem menüsü artık balonun İÇİNDE (kapanıştan önce).
  inner.push(
    buildChatOperationButtons(sourceId, {
      hasText: !!(contentText && contentText.trim()),
      hasImage: !!imageDataUrl,
    })
  );

  inner.push("</div>"); // bubble
  // [TR] Reaksiyon çipleri balonun ALTINDA (yapılmış işlemler — reaksiyon emojisi gibi).
  inner.push(buildReactionChips(reactions));
  inner.push("</div>"); // bubble-wrap

  wrap.innerHTML = inner.join("");
  chatWindow.appendChild(wrap);
  chatMessageCount += 1;
  updateChatCount();
  attachShowMoreHandler();
  persistAiChat();
  saveChatMessageToServer({
    role: "user",
    messageType: imageDataUrl ? "image" : "text",
    text: `${contentText || (imageDataUrl ? "Image selected." : "")}${prompt ? `\n\nPrompt: ${prompt}` : ""}`,
  });
  scrollChatToBottom();
  return wrap;
}

function buildChatModelOptions() {
  if (!aiModel) return '<option value="mock-gpt">Default model</option>';
  return [...aiModel.options]
    .map((o) => {
      const selected = o.value === (aiModel.value || defaultAiModel) ? " selected" : "";
      return `<option value="${escapeHtml(o.value)}"${selected}>${escapeHtml(o.textContent || o.value)}</option>`;
    })
    .join("");
}

async function appendChatOperationSetup(sourceId, operation) {
  if (!chatWindow) return;
  dismissPendingChatOpSetups();
  if (!ensureSourceInMemory(sourceId)) {
    replaceWithAiMessage(null, {
      text: "This chat card came from an older session, so its operation data is no longer in memory. Please select the text or image again.",
      isError: true,
    });
    return;
  }
  const source = aiChatSources.get(sourceId);
  if (!source) {
    replaceWithAiMessage(null, {
      text: "This chat card came from an older session, so its operation data is no longer in memory. Please select the text or image again.",
      isError: true,
    });
    return;
  }
  if (aiOperation) aiOperation.value = operation;
  if (typeof loadModelsForTask === "function" && operation !== "Narrate") {
    await loadModelsForTask(operation);
  }

  ensureChatVisible();
  const opLabel = AI_CHAT_OPERATION_DEFS.find((d) => d.op === operation)?.label || operation;
  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--ai ai-chat-message--op-setup";
  wrap.innerHTML = `
    <div class="ai-chat-bubble-wrap ai-chat-bubble-wrap--wide">
      ${buildOpFormHtml(sourceId, operation, opLabel)}
    </div>`;
  chatWindow.appendChild(wrap);
  scrollChatToBottom();
}

// [TR] Operasyon ayar formu HTML'i — hem chat balonunda hem de soldaki fonksiyon
//      rayının flyout'unda kullanılır (DRY). data-role="chat-op-form" → submit
//      yakalayıcıları runChatOperation(form) çağırır.
function buildOpFormHtml(sourceId, operation, opLabel) {
  const setupId = `chat-setup-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const isNarrate = operation === "Narrate";
  const langLabel = isNarrate
    ? "Seslendirme dili"
    : operation === "Translate"
      ? "Hedef dil"
      : "Cevap dili";
  const styleField = isNarrate
    ? ""
    : `<label class="ai-chat-op-field">
            <span>Stil</span>
            <select class="form-select form-select-sm" data-field="style">
              <option value="Formal"${defaultStyle === "Formal" ? " selected" : ""}>Formal</option>
              <option value="Academic"${defaultStyle === "Academic" ? " selected" : ""}>Academic</option>
              <option value="Simplified"${defaultStyle === "Simplified" ? " selected" : ""}>Simplified</option>
            </select>
          </label>`;
  const modelField = isNarrate
    ? ""
    : `<label class="ai-chat-op-field">
            <span>Model</span>
            <select class="form-select form-select-sm" data-field="modelName">${buildChatModelOptions()}</select>
          </label>`;
  const customField = isNarrate
    ? ""
    : `<label class="ai-chat-op-field mt-2">
          <span>Custom prompt</span>
          <textarea class="form-control form-control-sm" rows="2" data-field="customInstruction" placeholder="Add an optional instruction"></textarea>
        </label>`;
  const narrateHint = isNarrate
    ? `<p class="small text-muted mb-2">If you pick a language other than the source text, it will be translated first, then narrated.</p>`
    : "";
  return `
      <form class="ai-chat-op-form" data-role="chat-op-form" data-source-id="${escapeHtml(sourceId)}" data-operation="${escapeHtml(operation)}" id="${escapeHtml(setupId)}">
        <div class="ai-chat-op-form__title">${escapeHtml(opLabel)} settings</div>
        ${narrateHint}
        <div class="ai-chat-op-form__grid">
          <label class="ai-chat-op-field">
            <span>${langLabel}</span>
            <select class="form-select form-select-sm" data-field="targetLanguage">
              <option value="Turkish">Turkish</option>
              <option value="English" selected>English</option>
              <option value="German">German</option>
              <option value="French">French</option>
              <option value="Spanish">Spanish</option>
              <option value="Italian">Italian</option>
              <option value="Portuguese">Portuguese</option>
              <option value="Russian">Russian</option>
              <option value="Arabic">Arabic</option>
              <option value="Chinese">Chinese</option>
              <option value="Japanese">Japanese</option>
              <option value="Korean">Korean</option>
            </select>
          </label>
          ${styleField}
          ${modelField}
        </div>
        ${customField}
        <div class="ai-chat-op-form__actions">
          <button type="submit" class="btn btn-sm btn-dark">${isNarrate ? "Narrate" : "Run"}</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" data-role="chat-op-cancel">Cancel</button>
        </div>
      </form>`;
}

async function runChatOperation(form) {
  dismissPendingChatOpSetups();
  const source = aiChatSources.get(form.dataset.sourceId || "");
  const operation = form.dataset.operation || "Explanation";
  if (!source || !aiProcessUrl || !documentId) return;

  const fd = (name) => form.querySelector(`[data-field="${name}"]`)?.value || "";
  const payload = {
    documentId,
    operationType: operation,
    modelName: fd("modelName") || aiModel?.value || defaultAiModel,
    targetLanguage: fd("targetLanguage") || aiTargetLanguage?.value || "English",
    style: fd("style") || aiStyle?.value || defaultStyle,
    customInstruction: fd("customInstruction"),
    inputText: source.text || "",
    sourcePageNumber: currentRect?.pageNumber || currentPage,
    inputImageBase64: source.imageBase64 || null,
    inputImageMimeType: source.imageMimeType || null,
  };

  const reactions = [
    { kind: "operation", label: operation },
    { kind: "model", label: payload.modelName },
    { kind: "style", label: payload.style },
  ];
  // [TR] Çeviri her zaman hedef dili gösterir; diğer işlemlerde kullanıcı belirli
  //      bir cevap dili seçtiyse (Auto değilse) onu da reaksiyon olarak gösterir.
  const tgtLang = payload.targetLanguage;
  if (operation === "Translate") {
    reactions.push({ kind: "lang", label: `→ ${tgtLang}` });
  } else if (tgtLang && tgtLang.toLowerCase() !== "auto") {
    reactions.push({ kind: "lang", label: `→ ${tgtLang}` });
  }

  // [TR] Her işlemde hangi metin/görsel üzerine uygulandığı ilk seferki gibi
  //      tam olarak gösterilir (sadece "AI operation" demek yerine kaynağı yazar).
  appendUserMessage({
    imageDataUrl: source.imageDataUrl || null,
    contentText: source.text || (source.imageDataUrl ? "" : `AI operation: ${operation}`),
    prompt: payload.customInstruction,
    reactions,
    sourceId: form.dataset.sourceId || null,
  });

  const controller = new AbortController();
  const typingNode = appendAiTypingPlaceholder(controller);
  form.closest(".ai-chat-message")?.remove();
  setAiStatus("info", "AI operation is running from chat...", { loading: true });

  try {
    const r = await fetch(aiProcessUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken,
      },
      body: JSON.stringify(payload),
      signal: controller.signal,
    });
    const res = await r.json();
    if (!res.ok) {
      clearAiStatus();
      replaceWithAiMessage(typingNode, { text: res.message || "AI operation failed.", isError: true });
      return;
    }
    setAiStatus("success", res.message || "AI operation completed.");
    replaceWithAiMessage(typingNode, {
      text: res.outputText || "",
      imageUrl: res.outputImageUrl || null,
      resultUrl: res.resultUrl || null,
    });
    if (aiResultLink && res.resultUrl) {
      aiResultLink.href = res.resultUrl;
      aiResultLink.classList.remove("d-none");
    }
  } catch (e) {
    // [TR] Kullanıcı Stop'a bastıysa hata değil, "iptal edildi" mesajı göster.
    if (e?.name === "AbortError") {
      clearAiStatus();
      replaceWithAiMessage(typingNode, { text: "⏹ Operation cancelled by user." });
      return;
    }
    clearAiStatus();
    replaceWithAiMessage(typingNode, { text: "An error occurred during the AI request.", isError: true });
  }
}

// [TR] AI yazıyor... bekleme balonu — istek tamamlandığında kaldırılır/ değiştirilir.
//      abortController verilirse "Stop" butonu eklenir; kullanıcı işlemi yarıda kesebilir.
function appendAiTypingPlaceholder(abortController = null) {
  if (!chatWindow) return null;
  ensureChatVisible();

  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--ai";
  const stopBtn = abortController
    ? '<button type="button" class="ai-chat-stop-btn" data-role="chat-stop" title="Stop">⏹ Stop</button>'
    : "";
  wrap.innerHTML = `
    <div class="ai-chat-bubble-wrap">
      <div class="ai-chat-bubble ai-chat-bubble--typing">
        <span class="ai-chat-typing"><span></span><span></span><span></span></span>
        ${stopBtn}
      </div>
    </div>`;
  if (abortController) wrap._abortController = abortController;
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

  if (isError) wrap.classList.add("ai-chat-message--error");

  const parts = [];
  parts.push('<div class="ai-chat-bubble-wrap">');
  parts.push(`<div class="ai-chat-bubble${isError ? " ai-chat-bubble--error" : ""}">`);

  if (text && text.trim().length > 0) {
    parts.push(renderCollapsibleText(text));
  }
  if (imageUrl) {
    if (text) parts.push('<div style="height:0.45rem"></div>');
    parts.push(`<img src="${escapeHtml(imageUrl)}" alt="AI response image" />`);
  }
  if (!text && !imageUrl) {
    parts.push('<p class="ai-chat-bubble-text text-muted"><em>(empty response)</em></p>');
  }

  parts.push("</div>"); // bubble

  if (isError) {
    parts.push(
      '<button type="button" class="ai-chat-error-dismiss" data-role="chat-error-dismiss" title="Delete error">🗑 Delete</button>'
    );
  }

  if (resultUrl) {
    parts.push(`<div class="ai-chat-meta"><a class="link-primary" href="${escapeHtml(resultUrl)}">Open detailed result →</a></div>`);
  }
  parts.push("</div>"); // wrap

  wrap.innerHTML = parts.join("");
  chatMessageCount += 1;
  updateChatCount();
  attachShowMoreHandler();
  persistAiChat();
  saveChatMessageToServer({
    role: "ai",
    messageType: isError ? "error" : imageUrl ? "image" : "text",
    text: text || (imageUrl ? "Visual AI output." : ""),
    imageUrl: imageUrl || "",
    resultUrl: resultUrl || "",
  });
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
    // [TR] Sohbet temizlenince yalnızca flyout'u kapat; compose kaynakları
    //      (extracted text / görsel) korunur ki kullanıcı onlar üzerinde işlemeye
    //      devam edebilsin.
    hideFnFlyout();
  });
}

if (chatWindow) {
  chatWindow.addEventListener("click", (ev) => {
    // [TR] Balon içi işlem bölmesini aç/kapat (genişlet).
    const opsToggle = ev.target?.closest?.('[data-role="ops-toggle"]');
    if (opsToggle) {
      const row = opsToggle.parentElement?.querySelector(".ai-chat-op-row");
      if (row) {
        const nowCollapsed = row.classList.toggle("ai-chat-op-row--collapsed");
        opsToggle.setAttribute("aria-expanded", String(!nowCollapsed));
        const chev = opsToggle.querySelector(".ai-chat-ops__chevron");
        if (chev) chev.textContent = nowCollapsed ? "▾" : "▴";
      }
      return;
    }

    const dismissBtn = ev.target?.closest?.('[data-role="chat-error-dismiss"]');
    if (dismissBtn) {
      removeChatMessage(dismissBtn.closest(".ai-chat-message"));
      return;
    }

    // [TR] Devam eden işlemi yarıda durdur (Stop).
    const stopBtn = ev.target?.closest?.('[data-role="chat-stop"]');
    if (stopBtn) {
      const msg = stopBtn.closest(".ai-chat-message");
      if (msg && msg._abortController) msg._abortController.abort();
      return;
    }

    const opBtn = ev.target?.closest?.('[data-role="chat-op"]');
    if (opBtn) {
      const sourceId = opBtn.dataset.sourceId || "";
      const operation = opBtn.dataset.operation || "";
      if (!ensureSourceInMemory(sourceId)) {
        replaceWithAiMessage(null, {
          text: "This chat card came from an older session, so its operation data is no longer in memory. Please select the text or image again.",
          isError: true,
        });
        return;
      }
      const source = aiChatSources.get(sourceId);
      if (operation === "Narrate") {
        if (!source?.text?.trim()) {
          setOcrStatus("error", "No text found for narration.");
          return;
        }
        appendChatOperationSetup(sourceId, operation);
        return;
      }
      appendChatOperationSetup(sourceId, operation);
      return;
    }

    const cancelBtn = ev.target?.closest?.('[data-role="chat-op-cancel"]');
    if (cancelBtn) {
      cancelBtn.closest(".ai-chat-message")?.remove();
      persistAiChat();
    }
  });

  chatWindow.addEventListener("submit", (ev) => {
    const form = ev.target?.closest?.('[data-role="chat-op-form"]');
    if (!form) return;
    ev.preventDefault();
    if (form.dataset.operation === "Narrate") {
      runNarrationFromForm(form);
      return;
    }
    runChatOperation(form);
  });
}

// ─── CREATIVE: AKTİF KAYNAK + COMPOSE ALANI ─────────────────────────────────
// [TR] Bölge seçilip "Extract text" veya "Select as image" denince üretilen
//      kaynak burada "aktif" olur; soldaki fonksiyon rayı bu kaynak üzerinde çalışır.
// [TR] Mor dalga animasyonu tercihi (localStorage). Kapalıysa yalnızca mor arka plan kalır.
const FN_WAVE_PREF_KEY = "pdf_bitirme_fn_wave_anim";
function isFnWaveEnabled() {
  try {
    return localStorage.getItem(FN_WAVE_PREF_KEY) !== "0";
  } catch {
    return true;
  }
}
function saveFnWavePref(enabled) {
  try {
    localStorage.setItem(FN_WAVE_PREF_KEY, enabled ? "1" : "0");
  } catch {
    /* yoksay */
  }
}

function updateFnWaveToggleLabel() {
  const waveToggle = document.getElementById("fn-wave-toggle");
  const label = waveToggle?.closest("label")?.querySelector("span");
  if (!waveToggle || !label) return;
  label.textContent = waveToggle.checked ? "Disable effect" : "Enable effect";
  waveToggle.setAttribute("aria-label", label.textContent);
}

function setActiveSource(sourceId, { pulse = true } = {}) {
  workspaceActiveSourceId = sourceId || null;
  updateFnRailEnabled();
  if (fnRail) {
    fnRail.classList.toggle("is-armed", !!sourceId);
    if (sourceId && pulse && isFnWaveEnabled()) {
      fnRail.classList.remove("is-pulsing");
      void fnRail.offsetWidth;
      fnRail.classList.add("is-pulsing");
      setTimeout(() => fnRail.classList.remove("is-pulsing"), 900);
    } else if (!sourceId) {
      fnRail.classList.remove("is-pulsing");
    }
  }
}

// ─── CREATIVE (4. tur): KAYNAK SEÇİMİ (metin / görsel) ──────────────────────
// [TR] Compose kartlarındaki radyo seçimi + aktif kaynak senkronizasyonu.
function setComposeSelection(kind, { pulse = true } = {}) {
  currentComposeKind = kind || null;
  document.querySelectorAll(".compose-source").forEach((p) => {
    p.classList.toggle("is-selected", !!kind && p.dataset.kind === kind);
  });
  const id = kind === "text" ? textSourceId : kind === "image" ? imageSourceId : null;
  setActiveSource(id, { pulse });
}

// [TR] Panellerin görünürlüğünü, boş durumu ve otomatik/zorunlu seçimi yeniler.
//      justAdded: yeni eklenen kaynak ("text"|"image") → iki kaynak da varsa o seçilir.
function refreshComposeState({ pulse = false, justAdded = null } = {}) {
  const hasText = !!textSourceId;
  const hasImage = !!imageSourceId;
  ocrTextPanel?.classList.toggle("d-none", !hasText);
  capturedImagePanel?.classList.toggle("d-none", !hasImage);
  if (composeEmpty) composeEmpty.classList.toggle("d-none", hasText || hasImage);
  workspaceCompose?.classList.toggle("workspace-compose--has-sources", hasText || hasImage);

  let kind = currentComposeKind;
  // [TR] Yeni eklenen kaynak (justAdded) varsa onu seç — kullanıcı az önce
  //      yakaladığı içeriği kullanmak ister; ayrıca hangisinin aktif olduğu netleşir.
  if (justAdded === "text" && hasText) kind = "text";
  else if (justAdded === "image" && hasImage) kind = "image";
  if (kind === "text" && !hasText) kind = null;
  if (kind === "image" && !hasImage) kind = null;
  if (!kind) {
    if (hasText && !hasImage) kind = "text";
    else if (hasImage && !hasText) kind = "image";
    // ikisi de varsa ve hiç seçim yoksa: kullanıcı kartlardan birini seçmeli (kilit).
  }
  setComposeSelection(kind, { pulse: pulse && !!kind });
}

// ─── CREATIVE: DİKEY FONKSİYON RAYI (görsel 3 tarzı) ─────────────────────────
// [TR] AI_CHAT_OPERATION_DEFS'ten ikon + etiketli dikey butonlar üretir.
function buildFnRail() {
  if (!fnRail) return;
  const header = '<div class="workspace-fn-rail__title" aria-hidden="true">Functions</div>';
  const list =
    '<div class="workspace-fn-rail__list">' +
    AI_CHAT_OPERATION_DEFS.map(
      (d) =>
        `<button type="button" class="workspace-fn-btn" data-operation="${escapeHtml(d.op)}">` +
        `<span class="workspace-fn-btn__icon" aria-hidden="true">${d.icon}</span>` +
        `<span class="workspace-fn-btn__label">${escapeHtml(d.label)}</span>` +
        "</button>"
    ).join("") +
    "</div>";
  const footer =
    '<div class="workspace-fn-rail__footer">' +
    '<label class="workspace-fn-rail__wave-toggle" title="Toggle purple wave animation">' +
    '<input type="checkbox" id="fn-wave-toggle" aria-label="Enable effect" />' +
    '<span>Enable effect</span></label></div>';
  fnRail.innerHTML = header + list + footer;
  const waveToggle = document.getElementById("fn-wave-toggle");
  if (waveToggle) {
    waveToggle.checked = isFnWaveEnabled();
    updateFnWaveToggleLabel();
    waveToggle.addEventListener("change", () => {
      saveFnWavePref(waveToggle.checked);
      updateFnWaveToggleLabel();
    });
  }
  positionResizerHint();
}

// [TR] Gönderilebilir aktif kaynak var mı? (metin veya görsel)
function getActiveSource() {
  if (!workspaceActiveSourceId) return null;
  return aiChatSources.get(workspaceActiveSourceId) || null;
}

function hasSendableSource() {
  const src = getActiveSource();
  return !!(src && (src.text?.trim() || src.imageBase64));
}

function warnNoSource() {
  if (textSourceId || imageSourceId) {
    setOcrStatus("error", "Gönderilecek kaynak seçilmedi. Yukarıdan metin veya görseli seçin.");
  } else {
    setOcrStatus("error", "Gönderilecek bir şey bulunamadı. Önce PDF üzerinde bölge seçin.");
  }
}

// [TR] Mor çubuk üzerindeki ↔ ipucunu Creative–Explain arasına hizala.
function positionResizerHint() {
  const hint = document.getElementById("workspace-panel-resizer-hint");
  if (!hint || !panelResizer || !fnRail) return;
  const creativeBtn = fnRail.querySelector('[data-operation="CreativeWrite"]');
  const explainBtn = fnRail.querySelector('[data-operation="Explanation"]');
  if (!creativeBtn || !explainBtn) return;
  const resizerRect = panelResizer.getBoundingClientRect();
  const creativeRect = creativeBtn.getBoundingClientRect();
  const explainRect = explainBtn.getBoundingClientRect();
  const midY = (creativeRect.bottom + explainRect.top) / 2 - resizerRect.top;
  hint.style.top = `${Math.max(8, midY)}px`;
}

function updateFnRailEnabled() {
  if (!fnRail) return;
  fnRail.querySelectorAll(".workspace-fn-btn").forEach((btn) => {
    const def = AI_CHAT_OPERATION_DEFS.find((d) => d.op === btn.dataset.operation);
    btn.title = def?.label || "";
    btn.classList.toggle(
      "is-active",
      btn.dataset.operation === fnFlyout?.dataset.operation && !fnFlyout?.classList.contains("d-none")
    );
  });
}

function hideFnFlyout() {
  if (!fnFlyout) return;
  fnFlyout.classList.add("d-none");
  fnFlyout.innerHTML = "";
  delete fnFlyout.dataset.operation;
  updateFnRailEnabled();
}

function positionFnFlyout(btn) {
  if (!fnFlyout || !overlayPanel || !fnRail) return;
  const panelRect = overlayPanel.getBoundingClientRect();
  const railRect = fnRail.getBoundingClientRect();
  const btnRect = btn.getBoundingClientRect();
  const flyH = fnFlyout.offsetHeight || 220;
  let top = btnRect.top - panelRect.top;
  top = Math.max(8, Math.min(top, panelRect.height - flyH - 8));
  fnFlyout.style.top = `${top}px`;
  fnFlyout.style.left = `${railRect.right - panelRect.left + 6}px`;
}

// [TR] Fonksiyon rayındaki butona basınca: ya seslendirmeyi başlatır (Narrate)
//      ya da rayın sağında alt fonksiyon penceresini (ayar formu) açar.
async function openFnFlyout(btn, operation) {
  dismissPendingChatOpSetups();
  if (!fnFlyout) return;
  if (!hasSendableSource()) {
    warnNoSource();
    return;
  }
  if (typeof loadModelsForTask === "function") {
    await loadModelsForTask(operation);
  }
  const opLabel = AI_CHAT_OPERATION_DEFS.find((d) => d.op === operation)?.label || operation;
  fnFlyout.innerHTML =
    `<div class="workspace-fn-flyout__head">${escapeHtml(opLabel)}</div>` +
    buildOpFormHtml(workspaceActiveSourceId, operation, opLabel);
  fnFlyout.dataset.operation = operation;
  fnFlyout.classList.remove("d-none");
  positionFnFlyout(btn);
  updateFnRailEnabled();
}

if (fnRail) {
  buildFnRail();
  fnRail.addEventListener("click", (ev) => {
    const btn = ev.target?.closest?.(".workspace-fn-btn");
    if (!btn) return;
    const operation = btn.dataset.operation || "";
    if (!hasSendableSource()) {
      warnNoSource();
      return;
    }
    if (operation === "Narrate") {
      const src = aiChatSources.get(workspaceActiveSourceId || "");
      if (!src?.text?.trim()) {
        setOcrStatus("error", "No text found for narration.");
        return;
      }
      if (!fnFlyout.classList.contains("d-none") && fnFlyout.dataset.operation === operation) {
        hideFnFlyout();
        return;
      }
      openFnFlyout(btn, operation);
      return;
    }
    // [TR] Aynı butona tekrar basınca aç/kapat (toggle).
    if (!fnFlyout.classList.contains("d-none") && fnFlyout.dataset.operation === operation) {
      hideFnFlyout();
      return;
    }
    openFnFlyout(btn, operation);
  });
}

if (fnFlyout) {
  fnFlyout.addEventListener("submit", (ev) => {
    const form = ev.target?.closest?.('[data-role="chat-op-form"]');
    if (!form) return;
    ev.preventDefault();
    hideFnFlyout();
    if (form.dataset.operation === "Narrate") {
      runNarrationFromForm(form);
      return;
    }
    runChatOperation(form);
  });
  fnFlyout.addEventListener("click", (ev) => {
    if (ev.target?.closest?.('[data-role="chat-op-cancel"]')) hideFnFlyout();
  });
}

// [TR] Flyout dışına / başka yere tıklanınca kapansın.
document.addEventListener("mousedown", (ev) => {
  if (!fnFlyout || fnFlyout.classList.contains("d-none")) return;
  if (fnFlyout.contains(ev.target)) return;
  if (ev.target?.closest?.(".workspace-fn-btn")) return; // ray click'i kendi yönetir
  hideFnFlyout();
});

// ─── CREATIVE: PANEL GENİŞLETME (mor tutamak) ───────────────────────────────
// [TR] Sol kenardaki mor çizgi sürüklenerek panel sola/sağa genişletilir.
//      Panel PDF üstünde absolute olduğundan, genişledikçe PDF'in daha çoğunu örter.
let panelResizing = false;
const panelResizerHint = document.getElementById("workspace-panel-resizer-hint");
function startPanelResize(ev) {
  if (ev.button !== 0) return;
  panelResizing = true;
  document.body.classList.add("workspace-resizing");
  ev.preventDefault();
}
if (panelResizer) {
  panelResizer.addEventListener("mousedown", startPanelResize);
  panelResizerHint?.addEventListener("mousedown", startPanelResize);
  window.addEventListener("mousemove", (ev) => {
    if (!panelResizing || !overlayPanel || !workspaceBody) return;
    const bodyRect = workspaceBody.getBoundingClientRect();
    let w = bodyRect.right - ev.clientX;
    const min = 320;
    const max = Math.max(min, bodyRect.width - 160);
    w = Math.max(min, Math.min(max, w));
    overlayPanel.style.width = `${w}px`;
    syncViewerPanRoom();
  });
  window.addEventListener("mouseup", () => {
    if (!panelResizing) return;
    panelResizing = false;
    document.body.classList.remove("workspace-resizing");
    hideFnFlyout();
    positionResizerHint();
  });
}

// ─── CREATIVE: PDF KAYDIRMA / PAN ALANI ─────────────────────────────────────
// [TR] Yüzen panel PDF'in sağını örttüğü için, canvas'a sağ/alt boşluk ekleriz;
//      böylece kullanıcı sayfanın panel altında kalan kısmını sol görünür alana
//      kaydırabilir. PDF sola yaslı render edilir (CSS margin:0).
const viewerFrame = document.getElementById("workspace-viewer-frame");
const viewerArea = document.getElementById("workspace-viewer-area");
// [TR] CREATIVE (3. tur): PDF görünür alanı yüzen panelin SOLUNDA kalsın; böylece
//      kaydırma çubukları (dikey: mor tutamağın hemen solunda, yatay: altta) her
//      zaman görünür ve panelin altına gizlenmez. Görünür alan genişliği panel
//      boyutuna göre anlık ayarlanır.
function syncViewerPanRoom() {
  if (!workspaceBody) return;
  const isOverlay = overlayPanel && getComputedStyle(overlayPanel).position === "absolute";
  if (viewerArea) {
    if (isOverlay) {
      const bodyW = workspaceBody.clientWidth;
      const panelW = overlayPanel.offsetWidth;
      viewerArea.style.width = `${Math.max(220, bodyW - panelW)}px`;
    } else {
      viewerArea.style.width = "";
    }
  }
  // [TR] Sola yaslı; ekstra margin gerekmiyor (alan zaten panelin solunda).
  if (canvasWrap) {
    canvasWrap.style.marginRight = "0px";
    canvasWrap.style.marginBottom = "0px";
  }
}

// [TR] PDF'i görünür alana (panelin soluna) genişlikçe sığdır (fit-to-width).
async function fitToWidth() {
  if (!pdfDoc || !viewerFrame) return;
  syncViewerPanRoom();
  const page = await pdfDoc.getPage(currentPage);
  const unit = page.getViewport({ scale: 1.0 });
  zoom = Math.max(0.4, Math.min(2.5, (viewerFrame.clientWidth - 14) / unit.width));
  await renderPage(currentPage);
}

// ─── CREATIVE (3. tur): ÇALIŞMA ALANI DURUMUNU KORU ─────────────────────────
// [TR] "Open detailed result" ile gidip geri dönünce sayfa/zoom/kaydırma ve
//      seçili bölge kaybolmasın diye localStorage'a yazıp geri yükleriz.
const WS_STATE_PREFIX = "pdf_bitirme_ws_state_v1";
function getWsStateKey() {
  return `${WS_STATE_PREFIX}:${documentId}`;
}
let wsStateSaveTimer = null;
function saveWsState() {
  if (!documentId) return;
  try {
    let region = null;
    if (currentRect && overlay) {
      const ow = overlay.clientWidth || 1;
      const oh = overlay.clientHeight || 1;
      region = {
        pageNumber: currentRect.pageNumber,
        nx: currentRect.left / ow,
        ny: currentRect.top / oh,
        nw: currentRect.width / ow,
        nh: currentRect.height / oh,
      };
    }
    // [TR] Kaynakları da kaydet → geri dönünce extracted text / görsel hazır gelir,
    //      fonksiyonlar kilitli olmaz (yeniden bölge seçmeye gerek kalmaz).
    const textSrc = textSourceId ? aiChatSources.get(textSourceId) : null;
    const imgSrc = imageSourceId ? aiChatSources.get(imageSourceId) : null;
    const state = {
      page: currentPage,
      zoom,
      scrollLeft: viewerFrame ? viewerFrame.scrollLeft : 0,
      scrollTop: viewerFrame ? viewerFrame.scrollTop : 0,
      region,
      composeKind: currentComposeKind,
      ocrText: textSrc ? textSrc.text || "" : null,
      ocrResultId: lastOcrResultId || "",
      image: imgSrc
        ? {
            dataUrl: imgSrc.imageDataUrl || (capturedImagePreview ? capturedImagePreview.src : ""),
            base64: imgSrc.imageBase64 || null,
            mime: imgSrc.imageMimeType || "image/png",
          }
        : null,
    };
    localStorage.setItem(getWsStateKey(), JSON.stringify(state));
  } catch {
    /* yoksay */
  }
}
function saveWsStateDebounced() {
  if (wsStateSaveTimer) clearTimeout(wsStateSaveTimer);
  wsStateSaveTimer = setTimeout(saveWsState, 250);
}
function loadWsState() {
  if (!documentId) return null;
  try {
    const raw = localStorage.getItem(getWsStateKey());
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

// [TR] Orta tuş veya Space + sol tık ile sürükleyerek PDF'i kaydır (pan).
let spaceDown = false;
let panning = false;
let panStart = null;
window.addEventListener("keydown", (evt) => {
  if (evt.code === "Space" && document.activeElement?.tagName !== "TEXTAREA" && document.activeElement?.tagName !== "INPUT") {
    spaceDown = true;
    if (overlay) overlay.style.cursor = "grab";
  }
});
window.addEventListener("keyup", (evt) => {
  if (evt.code === "Space") {
    spaceDown = false;
    if (overlay) overlay.style.cursor = "";
  }
});
function startPan(evt) {
  if (!viewerFrame) return false;
  panning = true;
  panStart = {
    x: evt.clientX,
    y: evt.clientY,
    left: viewerFrame.scrollLeft,
    top: viewerFrame.scrollTop,
  };
  if (overlay) overlay.style.cursor = "grabbing";
  evt.preventDefault();
  return true;
}
overlay.addEventListener("mousedown", (evt) => {
  // [TR] Orta tuş (1) veya Space basılıyken sol tık → pan; çizim/selection devre dışı.
  if (evt.button === 1 || (evt.button === 0 && spaceDown)) {
    startPan(evt);
  }
}, true); // capture: çizim handler'ından önce çalışsın
window.addEventListener("mousemove", (evt) => {
  if (!panning || !panStart || !viewerFrame) return;
  viewerFrame.scrollLeft = panStart.left - (evt.clientX - panStart.x);
  viewerFrame.scrollTop = panStart.top - (evt.clientY - panStart.y);
});
window.addEventListener("mouseup", () => {
  if (!panning) return;
  panning = false;
  panStart = null;
  if (overlay) overlay.style.cursor = spaceDown ? "grab" : "";
});

// ─── CREATIVE: BÖLGE POPUP'I (seçimin üstünde hızlı menü) ────────────────────
// [TR] Seçili dikdörtgenin hemen üstünde "Extract text / Select as image" +
//      OCR motoru seçimi (Paddle varsayılan). Görsel 2 & 4.
function hideRegionPopup() {
  regionPopup?.classList.add("d-none");
}

// [TR] Seçimin/popup'ın hemen üstünde beliren yeşil onay kutusu (✓ + işlem adı).
//      Yukarı doğru çıkar, 2sn durur, sonra geri inip kaybolur.
let regionToastTimer = null;
function showRegionToast(text) {
  if (!regionToast || !regionPopup) return;
  // [TR] Toast popup'ın çocuğudur; popup gizliyse (mouse uzaklaşmış olabilir)
  //      seçim hâlâ varsa popup'ı yeniden göster ki onay kutusu görünsün.
  if (regionPopup.classList.contains("d-none") && currentRect) showRegionPopup();
  if (regionToastText) regionToastText.textContent = text || "Done";
  if (regionToastTimer) clearTimeout(regionToastTimer);
  regionToast.classList.remove("d-none", "is-leaving");
  // reflow → giriş animasyonu yeniden tetiklensin
  void regionToast.offsetWidth;
  regionToast.classList.add("is-visible");
  regionToastTimer = setTimeout(() => {
    regionToast.classList.remove("is-visible");
    regionToast.classList.add("is-leaving");
    regionToastTimer = setTimeout(() => {
      regionToast.classList.add("d-none");
      regionToast.classList.remove("is-leaving");
    }, 280);
  }, 2000);
}

function showRegionPopup() {
  if (!regionPopup || !currentRect || !canvasWrap) return;
  regionPopup.classList.remove("d-none");
  // [TR] Önce görünür yap, sonra ölç (offsetHeight/Width d-none iken 0 olur).
  const ph = regionPopup.offsetHeight || 96;
  const pw = regionPopup.offsetWidth || 300;
  const gap = 8;

  const visLeft = viewerFrame ? viewerFrame.scrollLeft : 0;
  const visRight = visLeft + (viewerFrame ? viewerFrame.clientWidth : canvasWrap.clientWidth);
  const vTop = visTop();
  const vBottom = viewerFrame ? viewerFrame.scrollTop + viewerFrame.clientHeight : canvasWrap.clientHeight;

  const rect = currentRect;

  function fits(left, top) {
    return left >= visLeft && left + pw <= visRight && top >= vTop && top + ph <= vBottom;
  }

  function clamp(left, top) {
    return {
      left: Math.max(visLeft, Math.min(left, Math.max(visLeft, visRight - pw))),
      top: Math.max(vTop, Math.min(top, Math.max(vTop, vBottom - ph))),
    };
  }

  // [TR] Öncelik: SAĞ → SOL → ÜST → ALT. Sağda boşluk varken (panel sola
  //      itilmemişken) menü seçimin yanına açılır; yalnızca başka yer kalmazsa alta iner.
  const candidates = [
    { left: rect.left + rect.width + gap, top: rect.top },
    { left: rect.left - pw - gap, top: rect.top },
    { left: rect.left, top: rect.top - ph - gap },
    { left: rect.left, top: rect.top + rect.height + gap },
  ];

  let pos = null;
  for (const c of candidates) {
    if (fits(c.left, c.top)) {
      pos = c;
      break;
    }
  }
  if (!pos) {
    for (const c of candidates) {
      const clamped = clamp(c.left, c.top);
      if (fits(clamped.left, clamped.top)) {
        pos = clamped;
        break;
      }
    }
  }
  if (!pos) pos = clamp(candidates[0].left, candidates[0].top);

  regionPopup.style.top = `${pos.top}px`;
  regionPopup.style.left = `${pos.left}px`;
}

// [TR] Görünür alanın üst sınırı (frame scrollTop) — popup üstte sığar mı kontrolü.
function visTop() {
  return viewerFrame ? viewerFrame.scrollTop : 0;
}

if (regionPopup) {
  // [TR] Varsayılan motor görsel olarak işaretli (Paddle).
  regionPopup.addEventListener("click", (ev) => {
    const eng = ev.target?.closest?.(".region-engine-opt");
    if (eng) {
      selectedOcrEngine = eng.dataset.engine || "Paddle";
      if (ocrEngineSelect) ocrEngineSelect.value = selectedOcrEngine;
      regionPopup.querySelectorAll(".region-engine-opt").forEach((b) =>
        b.classList.toggle("is-active", b === eng)
      );
      return;
    }
    const act = ev.target?.closest?.("[data-region-action]");
    if (!act) return;
    const action = act.dataset.regionAction;
    if (action === "extract") {
      if (ocrEngineSelect) ocrEngineSelect.value = selectedOcrEngine;
      btnExtract?.click();
    } else if (action === "image") {
      btnSelectImage?.click();
    } else if (action === "copy") {
      copyRegionImageToClipboard();
    }
    // [TR] İşlemden sonra popup'ı KAPATMA — kullanıcı fikir değiştirip diğer
    //      seçeneği uygulayabilsin (yeniden bölge seçmesi gerekmesin).
  });

  regionPopup.addEventListener("mousemove", syncRegionPopupHover);
  regionPopup.addEventListener("mouseleave", syncRegionPopupHover);
}

// ─── CREATIVE: bölge popup'ı hover ile yeniden görünür ──────────────────────
// [TR] Bölge seçiliyken mouse seçim/popup üzerindeyken menü anında görünür;
//      ayrılınca gecikmesiz gizlenir.
function pointInRegionPopup(p) {
  if (!regionPopup || regionPopup.classList.contains("d-none")) return false;
  const left = parseFloat(regionPopup.style.left) || 0;
  const top = parseFloat(regionPopup.style.top) || 0;
  const w = regionPopup.offsetWidth;
  const h = regionPopup.offsetHeight;
  const pad = 6;
  return (
    p.x >= left - pad &&
    p.x <= left + w + pad &&
    p.y >= top - pad &&
    p.y <= top + h + pad
  );
}
function syncRegionPopupHover(evt) {
  if (drawing || panning || !currentRect) return;
  const p = normalizeMousePosition(evt);
  if (pointInCurrentRect(p) || pointInRegionPopup(p)) {
    if (regionPopup?.classList.contains("d-none")) showRegionPopup();
  } else {
    hideRegionPopup();
  }
}
function pointInCurrentRect(p) {
  if (!currentRect) return false;
  const pad = 6;
  return (
    p.x >= currentRect.left - pad &&
    p.x <= currentRect.left + currentRect.width + pad &&
    p.y >= currentRect.top - pad &&
    p.y <= currentRect.top + currentRect.height + pad
  );
}
overlay.addEventListener("mousemove", (evt) => {
  syncRegionPopupHover(evt);
});

function clearDraftRect() {
  currentRect = null;
  rect.classList.add("d-none");
  rect.style.left = "";
  rect.style.top = "";
  rect.style.width = "";
  rect.style.height = "";
  // [TR] Bölge temizlenince koordinat panelini ve hızlı menüyü de sıfırla.
  setRegionInfo(null);
  hideRegionPopup();
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

// [TR] Bölgeyi normalize (0..1) yakala/uygula — zoom değişince seçim doğru yerde kalsın.
function captureRegionNormalized() {
  if (!currentRect || !overlay) return null;
  const ow = overlay.clientWidth || 1;
  const oh = overlay.clientHeight || 1;
  return {
    pageNumber: currentRect.pageNumber,
    nx: currentRect.left / ow,
    ny: currentRect.top / oh,
    nw: currentRect.width / ow,
    nh: currentRect.height / oh,
  };
}
function applyRegionNormalized(norm) {
  if (!norm || !overlay || !rect) return;
  const ow = overlay.clientWidth || 1;
  const oh = overlay.clientHeight || 1;
  const left = norm.nx * ow;
  const top = norm.ny * oh;
  const width = norm.nw * ow;
  const height = norm.nh * oh;
  currentRect = { left, top, width, height, pageNumber: norm.pageNumber };
  rect.style.left = `${left}px`;
  rect.style.top = `${top}px`;
  rect.style.width = `${width}px`;
  rect.style.height = `${height}px`;
  rect.classList.remove("d-none");
  setRegionInfo({ pageNumber: norm.pageNumber, x: left, y: top, width, height });
}

async function renderPage(pageNumber) {
  // [TR] Zoom/render boyunca seçili bölge oranını koru (px değil oran).
  const keepRegion =
    currentRect && currentRect.pageNumber === pageNumber ? captureRegionNormalized() : null;
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
      console.warn("pdf.js render error; server fallback will be tried:", err);
    }
  }

  if (!rendered) {
    await renderPageFromServer(pageNumber);
  }

  pageInput.value = String(currentPage);
  pageTotal.textContent = `/ ${pdfDoc ? pdfDoc.numPages : 1}`;
  zoomLabel.textContent = `${Math.round(zoom * 100)}%`;
  // [TR] Zoom/sayfa değişince görünür alanı yenile, sonra bölgeyi yeni ölçeğe uygula.
  syncViewerPanRoom();
  if (keepRegion) applyRegionNormalized(keepRegion);
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
  // [TR] Koordinatları canvas üzerinden ölç — overlay ile hizalama kayması olmasın.
  const el = canvas || canvasWrap || overlay;
  if (!el) return { x: 0, y: 0 };
  const b = el.getBoundingClientRect();
  return {
    x: Math.max(0, Math.min(evt.clientX - b.left, b.width)),
    y: Math.max(0, Math.min(evt.clientY - b.top, b.height)),
  };
}

// [TR] Sol fareye basıldığında bölge çizimi başlar — eski "Bölge Seçimi Başlat"
//      modu kaldırıldı; selection her zaman aktiftir.
overlay.addEventListener("mousedown", (evt) => {
  if (evt.button !== 0) return; // sadece sol tık
  if (spaceDown || panning) return; // [TR] Space/orta tuş ile pan yapılıyorsa çizim yapma
  drawing = true;
  // [TR] Yeni seçim başlarken eski hızlı menüyü ve flyout'u kapat.
  hideRegionPopup();
  hideFnFlyout();
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
  } else if (currentRect) {
    // [TR] Geçerli seçim → seçimin üstünde hızlı işlem menüsünü göster.
    showRegionPopup();
  }
  setSelectionUi();
  saveWsState();
});

// [TR] "Seçimi Temizle" — bölgeyi ve panel bilgisini sıfırlar.
//      Yakalanmış görsel ayrı yönetiliyor (bkz. btn-clear-captured-image).
if (btnCancelSelection) {
  btnCancelSelection.addEventListener("click", () => {
    clearDraftRect();
    setSelectionUi();
    setRegionInfo(null);
    saveWsState();
  });
}

// [TR] Klavye kısayolu: Esc ile aktif seçimi temizle.
window.addEventListener("keydown", (evt) => {
  if (evt.key === "Escape" && currentRect) {
    clearDraftRect();
    setSelectionUi();
    setRegionInfo(null);
    saveWsState();
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
  const ocrController = new AbortController();
  setOcrStatus("info", `OCR is running... (${selectedEngine || "default"})`, {
    loading: true,
  });
  // [TR] Durum satırına Stop butonu ekle → kullanıcı OCR'ı yarıda kesebilir.
  if (ocrStatus) {
    const stop = document.createElement("button");
    stop.type = "button";
    stop.className = "btn btn-sm btn-outline-danger ms-2 py-0";
    stop.textContent = "Stop";
    stop.addEventListener("click", () => ocrController.abort());
    ocrStatus.appendChild(stop);
  }
  fetch(extractUrl, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: antiForgeryToken,
    },
    body: JSON.stringify(payload),
    signal: ocrController.signal,
  })
    .then((r) => r.json())
    .then((res) => {
      if (!res.ok) {
        setOcrStatus("error", res.message || "OCR failed.");
        return;
      }
      if (ocrTextarea) ocrTextarea.value = res.text || "";
      lastOcrResultId = res.ocrResultId || "";
      if (btnSaveOcrText) btnSaveOcrText.disabled = !lastOcrResultId;
      setOcrStatus("success", res.message || "OCR completed.");
      if ((res.text || "").trim()) {
        // [TR] Metni compose alanına düşür (chate YAZMA — işlem ancak fonksiyon
        //      seçilip çalıştırılınca chate gider). Kaynak "metin" olarak ayarlanır.
        const sourceId = createAiChatSource({ contentText: res.text || "" });
        textSourceId = sourceId;
        lastOcrChatSourceId = sourceId;
        refreshComposeState({ pulse: true, justAdded: "text" });
        // [TR] Seçimin üstünde yeşil onay kutusu (OCR başarılı).
        showRegionToast("OCR done");
      }
      saveWsStateDebounced();
    })
    .catch((e) => {
      if (e?.name === "AbortError") {
        setOcrStatus("info", "OCR cancelled.");
        return;
      }
      setOcrStatus("error", "An error occurred during the OCR request.");
    })
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
      setOcrStatus("error", "Select a region on the PDF first.");
      return;
    }
    const captured = captureSelectionAsImage();
    if (!captured) {
      setOcrStatus("error", "The image could not be captured.");
      return;
    }
    capturedImage = { base64: captured.base64, mimeType: captured.mimeType };
    showCapturedImagePreview(captured.dataUrl);
    // [TR] Görseli compose alanına düşür (chate YAZMA). Kaynak "görsel" olur.
    const sourceId = createAiChatSource({
      imageDataUrl: captured.dataUrl,
      imageBase64: captured.base64,
      imageMimeType: captured.mimeType,
    });
    imageSourceId = sourceId;
    lastImageChatSourceId = sourceId;
    refreshComposeState({ pulse: true, justAdded: "image" });
    showRegionToast("Image selected");
    setOcrStatus("success", "Image captured. Pick a function on the left to send it with a prompt.");
    saveWsStateDebounced();
  });
}

if (btnClearCapturedImage) {
  btnClearCapturedImage.addEventListener("click", () => {
    clearCapturedImage();
    imageSourceId = null;
    if (currentComposeKind === "image") currentComposeKind = null;
    refreshComposeState();
    saveWsStateDebounced();
  });
}

// [TR] Extracted text'i sil (görseldeki Remove gibi) — Expand'in solunda.
if (btnClearOcrText) {
  btnClearOcrText.addEventListener("click", () => {
    setOcrTextExpanded(false);
    if (ocrTextarea) ocrTextarea.value = "";
    textSourceId = null;
    lastOcrResultId = "";
    if (btnSaveOcrText) btnSaveOcrText.disabled = true;
    if (currentComposeKind === "text") currentComposeKind = null;
    refreshComposeState();
    saveWsStateDebounced();
  });
}

// [TR] Compose kartına tıklayınca o kaynağı seç (metin/görsel).
if (workspaceCompose) {
  workspaceCompose.addEventListener("click", (ev) => {
    const sel = ev.target?.closest?.('[data-role="select-source"]');
    if (!sel) return;
    setComposeSelection(sel.dataset.kind, { pulse: true });
  });
}

// [TR] OCR metni düzenlenince aktif metin kaynağını canlı güncelle + kaydet.
if (ocrTextarea) {
  ocrTextarea.addEventListener("input", () => {
    if (textSourceId && aiChatSources.has(textSourceId)) {
      aiChatSources.get(textSourceId).text = ocrTextarea.value;
      updateFnRailEnabled();
    }
    saveWsStateDebounced();
  });
}

// [TR] Seçili bölgeyi / yakalanan görseli panoya kopyala (+ bildirim).
async function copyRegionImageToClipboard() {
  // [TR] Önce anlık seçimi (canvas kırpma), yoksa mevcut yakalanmış görseli kullan.
  const shot = captureSelectionAsImage();
  const src = shot || (capturedImagePreview?.src ? { dataUrl: capturedImagePreview.src } : null);
  if (!src) {
    setOcrStatus("error", "Select a region first to copy its image.");
    return false;
  }
  try {
    const blob = await (await fetch(src.dataUrl)).blob();
    if (navigator.clipboard && window.ClipboardItem) {
      await navigator.clipboard.write([new ClipboardItem({ [blob.type || "image/png"]: blob })]);
      setOcrStatus("success", "Image copied to clipboard.");
      showRegionToast("Copied to clipboard");
      return true;
    }
    throw new Error("Clipboard API unavailable");
  } catch (e) {
    setOcrStatus("error", "Clipboard copy is not supported in this browser.");
    return false;
  }
}

if (btnCopyCapturedImage) {
  btnCopyCapturedImage.addEventListener("click", () => copyRegionImageToClipboard());
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
          setOcrStatus("error", res.message || "Save failed.");
          btnSaveOcrText.disabled = false;
          return;
        }
        const savedText = ocrTextarea.value || "";
        // [TR] Kaydetme chate balon EKLEMEZ (işlem ancak fonksiyon çalıştırılınca
        //      chate gider). Sadece metin kaynağını ve varsa mevcut balonları günceller.
        if (textSourceId && aiChatSources.has(textSourceId)) {
          aiChatSources.get(textSourceId).text = savedText;
        }
        updateAiChatSourceText(lastOcrChatSourceId, savedText);
        updateAiChatSourceText(lastImageChatSourceId, savedText);
        updateFnRailEnabled();
        saveWsStateDebounced();
        setOcrStatus("success", res.message || "OCR metni kaydedildi.");
        btnSaveOcrText.disabled = false;
      })
      .catch(() => {
        setOcrStatus("error", "An error occurred while saving.");
        btnSaveOcrText.disabled = false;
      });
  });
}

// ─── OCR/Gemini TTS → WhatsApp tarzı ses mesajı ─────────────────────────────
let activeVoiceAudio = null;
const VOICE_WAVE_BAR_COUNT = 48;
const voiceWavePeaksCache = new Map();

function voiceWaveStorageKey(audioUrl) {
  return `pdf_bitirme_wave_v1:${audioUrl}`;
}

function parseWavePeaksAttr(raw) {
  if (!raw) return null;
  try {
    const peaks = JSON.parse(raw);
    return Array.isArray(peaks) && peaks.length > 0 ? peaks : null;
  } catch {
    return null;
  }
}

function renderVoiceWaveformBarsHtml(peaks) {
  return peaks
    .map((peak) => {
      const h = Math.max(12, Math.min(100, Math.round(peak * 100)));
      return `<span class="ai-chat-voice-msg__bar" style="height:${h}%"></span>`;
    })
    .join("");
}

function applyVoiceWaveformToWrap(wrap, peaks) {
  if (!wrap || !peaks?.length) return;
  const barsHtml = renderVoiceWaveformBarsHtml(peaks);
  wrap.dataset.wavePeaks = JSON.stringify(peaks);
  wrap.querySelectorAll('[data-role="audio-bars"], [data-role="audio-bars-played"]').forEach((el) => {
    el.innerHTML = barsHtml;
  });
}

function ensureVoiceWaveformPlayedLayer(wrap) {
  const inner = wrap?.querySelector('[data-role="audio-wave-inner"]');
  if (!inner) return;
  let bg = inner.querySelector('[data-role="audio-bars"]');
  let played = inner.querySelector('[data-role="audio-bars-played"]');
  if (bg && !played) {
    played = document.createElement("div");
    played.className = "ai-chat-voice-msg__bars ai-chat-voice-msg__bars--played";
    played.dataset.role = "audio-bars-played";
    played.innerHTML = bg.innerHTML;
    bg.classList.add("ai-chat-voice-msg__bars--bg");
    const playhead = inner.querySelector('[data-role="audio-playhead"]');
    inner.insertBefore(played, playhead || null);
  } else if (!bg && played) {
    bg = document.createElement("div");
    bg.className = "ai-chat-voice-msg__bars ai-chat-voice-msg__bars--bg";
    bg.dataset.role = "audio-bars";
    bg.innerHTML = played.innerHTML;
    inner.insertBefore(bg, played);
  }
  if (bg) {
    bg.dataset.role = "audio-bars";
    bg.classList.add("ai-chat-voice-msg__bars--bg");
  }
  if (played) played.classList.add("ai-chat-voice-msg__bars--played");
}

function syncVoiceWaveformProgress(wrap, pct) {
  const played = wrap.querySelector('[data-role="audio-bars-played"]');
  const playhead = wrap.querySelector('[data-role="audio-playhead"]');
  const clipRight = Math.max(0, Math.min(100, 100 - pct));
  if (played) played.style.clipPath = `inset(0 ${clipRight}% 0 0)`;
  if (playhead) playhead.style.left = `${pct}%`;
}

function consolidateVoiceWaveformLayers(wrap) {
  const track = wrap?.querySelector(".ai-chat-voice-msg__track");
  if (!track) return;

  let inner = track.querySelector('[data-role="audio-wave-inner"]');
  if (!inner) {
    inner = document.createElement("div");
    inner.className = "ai-chat-voice-msg__wave-inner";
    inner.dataset.role = "audio-wave-inner";
    const bars = track.querySelector(
      '[data-role="audio-bars"], .ai-chat-voice-msg__bars--all, .ai-chat-voice-msg__bars--played, [data-role="audio-bars-played"]'
    );
    const playhead = track.querySelector('[data-role="audio-playhead"]');
    const seek = track.querySelector('[data-role="audio-seek"]');
    track.textContent = "";
    track.appendChild(inner);
    if (bars) inner.appendChild(bars);
    if (playhead) inner.appendChild(playhead);
    if (seek) inner.appendChild(seek);
  }

  const played = inner.querySelector('[data-role="audio-bars-played"], .ai-chat-voice-msg__bars--played');
  let bars = inner.querySelector('[data-role="audio-bars"], .ai-chat-voice-msg__bars--all');
  if (!bars && played) {
    bars = played;
    bars.dataset.role = "audio-bars";
    bars.classList.remove("ai-chat-voice-msg__bars--played");
  }
  if (bars) {
    bars.dataset.role = "audio-bars";
    bars.classList.add("ai-chat-voice-msg__bars--bg");
    bars.classList.remove("ai-chat-voice-msg__bars--all");
  }
  ensureVoiceWaveformPlayedLayer(wrap);

  const times = wrap.querySelector(".ai-chat-voice-msg__times");
  if (times && !times.querySelector('[data-role="audio-time"]')) {
    const cur = times.querySelector('[data-role="audio-current"]')?.textContent?.trim() || "0:00";
    const dur = times.querySelector('[data-role="audio-duration"]')?.textContent?.trim() || "0:00";
    times.innerHTML = `<span data-role="audio-time">${cur} / ${dur}</span>`;
  }
}

const VOICE_PLAY_ICON =
  '<svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" aria-hidden="true"><path d="M8 5v14l11-7z"/></svg>';
const VOICE_PAUSE_ICON =
  '<svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" aria-hidden="true"><path d="M6 4h4v16H6V4zm8 0h4v16h-4V4z"/></svg>';

function voiceVolumeIconSvg(level) {
  if (level === "mute") {
    return '<svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" aria-hidden="true"><path d="M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3 3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4 9.91 6.09 12 8.18V4z"/></svg>';
  }
  if (level === "low") {
    return '<svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" aria-hidden="true"><path d="M18.5 12c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM5 9v6h4l5 5V4L9 9H5z"/></svg>';
  }
  return '<svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" aria-hidden="true"><path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"/></svg>';
}


async function analyzeVoiceWaveformPeaks(audioUrl) {
  if (!audioUrl || audioUrl.startsWith("blob:")) return null;
  if (voiceWavePeaksCache.has(audioUrl)) return voiceWavePeaksCache.get(audioUrl);

  try {
    const cached = localStorage.getItem(voiceWaveStorageKey(audioUrl));
    const fromStorage = parseWavePeaksAttr(cached);
    if (fromStorage) {
      voiceWavePeaksCache.set(audioUrl, fromStorage);
      return fromStorage;
    }
  } catch {
    /* yoksay */
  }

  try {
    const response = await fetch(audioUrl, { credentials: "same-origin" });
    if (!response.ok) return null;
    const arrayBuffer = await response.arrayBuffer();
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return null;
    const audioContext = new AudioCtx();
    try {
      const audioBuffer = await audioContext.decodeAudioData(arrayBuffer.slice(0));
      const channelData = audioBuffer.getChannelData(0);
      const barCount = VOICE_WAVE_BAR_COUNT;
      const samplesPerBar = Math.max(1, Math.floor(channelData.length / barCount));
      const peaks = [];
      for (let i = 0; i < barCount; i++) {
        const start = i * samplesPerBar;
        const end = Math.min(start + samplesPerBar, channelData.length);
        let sum = 0;
        for (let j = start; j < end; j++) sum += Math.abs(channelData[j]);
        peaks.push(sum / Math.max(1, end - start));
      }
      const maxPeak = Math.max(...peaks, 0.001);
      const normalized = peaks.map((p) => 0.14 + (p / maxPeak) * 0.86);
      voiceWavePeaksCache.set(audioUrl, normalized);
      try {
        localStorage.setItem(voiceWaveStorageKey(audioUrl), JSON.stringify(normalized));
      } catch {
        /* yoksay */
      }
      return normalized;
    } finally {
      await audioContext.close().catch(() => {});
    }
  } catch (e) {
    console.warn("Voice waveform analysis failed:", e);
    return null;
  }
}

async function ensureVoiceWaveform(wrap) {
  if (!wrap) return;
  const existing = parseWavePeaksAttr(wrap.dataset.wavePeaks);
  if (existing) {
    applyVoiceWaveformToWrap(wrap, existing);
    return;
  }
  const src = wrap.dataset.audioUrl || "";
  const peaks = await analyzeVoiceWaveformPeaks(src);
  if (peaks) {
    applyVoiceWaveformToWrap(wrap, peaks);
    persistAiChat();
  }
}

function wireVoiceMessagePlayer(wrap) {
  if (!wrap || wrap._voicePlayer) return;
  consolidateVoiceWaveformLayers(wrap);
  ensureVoiceWaveformPlayedLayer(wrap);

  const src = (wrap.dataset.audioUrl || "").trim();
  if (!src || src.startsWith("blob:")) {
    wrap.classList.add("ai-chat-voice-msg--broken");
    return;
  }

  const audioEl = new Audio(src);
  audioEl.preload = "metadata";
  wrap._audioEl = audioEl;

  const playBtn = wrap.querySelector('[data-role="audio-play"]');
  const playIcon = wrap.querySelector('[data-role="audio-play-icon"]');
  const timeEl = wrap.querySelector('[data-role="audio-time"]');
  const seekEl = wrap.querySelector('[data-role="audio-seek"]');
  const volumeBtn = wrap.querySelector('[data-role="audio-volume-btn"]');
  const volumeIcon = wrap.querySelector('[data-role="audio-volume-icon"]');
  const volumePop = wrap.querySelector('[data-role="audio-volume-pop"]');
  const volumeRange = wrap.querySelector('[data-role="audio-volume-range"]');

  if (playIcon && !playIcon.querySelector("svg")) playIcon.innerHTML = VOICE_PLAY_ICON;
  if (volumeIcon && !volumeIcon.querySelector("svg")) volumeIcon.innerHTML = voiceVolumeIconSvg("high");

  const storedVolume = Number(wrap.dataset.volume);
  audioEl.volume = Number.isFinite(storedVolume) ? Math.min(1, Math.max(0, storedVolume)) : 1;
  if (volumeRange) volumeRange.value = String(Math.round(audioEl.volume * 100));

  const updateVolumeIcon = () => {
    if (!volumeIcon) return;
    const level = audioEl.volume <= 0 ? "mute" : audioEl.volume < 0.45 ? "low" : "high";
    volumeIcon.innerHTML = voiceVolumeIconSvg(level);
  };
  updateVolumeIcon();

  const setPlayingUi = () => {
    const playing = !audioEl.paused && !audioEl.ended;
    if (playIcon) playIcon.innerHTML = playing ? VOICE_PAUSE_ICON : VOICE_PLAY_ICON;
    wrap.classList.toggle("is-playing", playing);
  };

  let progressRaf = null;

  const syncProgress = () => {
    const cur = audioEl.currentTime || 0;
    const dur = Number.isFinite(audioEl.duration) && audioEl.duration > 0 ? audioEl.duration : 0;
    if (timeEl) timeEl.textContent = `${formatNarrateTime(cur)} / ${formatNarrateTime(dur)}`;
    const pct = dur > 0 ? Math.min(100, (cur / dur) * 100) : 0;
    if (seekEl && !seekEl.matches(":active")) seekEl.value = String(Math.round(pct));
    syncVoiceWaveformProgress(wrap, pct);
  };

  const startProgressLoop = () => {
    if (progressRaf) return;
    const frame = () => {
      syncProgress();
      if (!audioEl.paused && !audioEl.ended) {
        progressRaf = requestAnimationFrame(frame);
      } else {
        progressRaf = null;
      }
    };
    progressRaf = requestAnimationFrame(frame);
  };

  const stopProgressLoop = () => {
    if (!progressRaf) return;
    cancelAnimationFrame(progressRaf);
    progressRaf = null;
  };

  audioEl.addEventListener("loadedmetadata", syncProgress);
  audioEl.addEventListener("durationchange", syncProgress);
  audioEl.addEventListener("ended", () => {
    stopProgressLoop();
    setPlayingUi();
    syncProgress();
  });
  audioEl.addEventListener("play", () => {
    if (activeVoiceAudio && activeVoiceAudio !== audioEl) activeVoiceAudio.pause();
    activeVoiceAudio = audioEl;
    setPlayingUi();
    clearOcrStatus();
    startProgressLoop();
  });
  audioEl.addEventListener("pause", () => {
    stopProgressLoop();
    setPlayingUi();
    syncProgress();
  });
  audioEl.addEventListener("error", () => {
    wrap.classList.add("ai-chat-voice-msg--broken");
    setOcrStatus("error", "Saved audio could not be loaded.");
  });

  playBtn?.addEventListener("click", async () => {
    try {
      if (audioEl.paused || audioEl.ended) {
        if (audioEl.ended) audioEl.currentTime = 0;
        await audioEl.play();
      } else {
        audioEl.pause();
      }
    } catch (e) {
      setOcrStatus("error", e?.message || "Audio could not be played.");
    }
  });

  wrap.querySelector('[data-role="audio-rewind"]')?.addEventListener("click", () => {
    audioEl.currentTime = Math.max(0, audioEl.currentTime - NARRATE_SEEK_SECONDS);
    syncProgress();
  });
  wrap.querySelector('[data-role="audio-forward"]')?.addEventListener("click", () => {
    const dur = audioEl.duration;
    const max = Number.isFinite(dur) && dur > 0 ? dur : audioEl.currentTime + NARRATE_SEEK_SECONDS;
    audioEl.currentTime = Math.min(max, audioEl.currentTime + NARRATE_SEEK_SECONDS);
    syncProgress();
  });

  seekEl?.addEventListener("input", () => {
    const dur = audioEl.duration;
    if (!Number.isFinite(dur) || dur <= 0) return;
    audioEl.currentTime = (Number(seekEl.value) / 100) * dur;
    syncProgress();
  });

  volumeBtn?.addEventListener("click", (ev) => {
    ev.stopPropagation();
    volumePop?.classList.toggle("d-none");
  });

  volumeRange?.addEventListener("input", () => {
    audioEl.volume = Math.min(1, Math.max(0, Number(volumeRange.value) / 100));
    wrap.dataset.volume = String(audioEl.volume);
    updateVolumeIcon();
    persistAiChat();
  });

  wrap._voicePlayer = {
    closeVolumePop: () => volumePop?.classList.add("d-none"),
    stopProgressLoop,
  };

  ensureVoiceWaveform(wrap);
  syncProgress();
}

function upgradeLegacyVoiceMessages(root = chatWindow) {
  root?.querySelectorAll(".ai-chat-voice-msg").forEach((wrap) => {
    const wave = wrap.querySelector(".ai-chat-voice-msg__wave");
    if (!wave) return;

    if (!wave.querySelector(".ai-chat-voice-msg__track")) {
      const allBars = wave.querySelector(".ai-chat-voice-msg__bars--all, [data-role='audio-bars']");
      const playedBars = wave.querySelector(".ai-chat-voice-msg__bars--played, [data-role='audio-bars-played']");
      const seek = wave.querySelector(".ai-chat-voice-msg__seek");
      if (allBars || playedBars) {
        const track = document.createElement("div");
        track.className = "ai-chat-voice-msg__track";
        const inner = document.createElement("div");
        inner.className = "ai-chat-voice-msg__wave-inner";
        inner.dataset.role = "audio-wave-inner";
        wave.appendChild(track);
        track.appendChild(inner);
        if (allBars) inner.appendChild(allBars);
        else if (playedBars) inner.appendChild(playedBars);
        if (!wrap.querySelector('[data-role="audio-playhead"]')) {
          const playhead = document.createElement("div");
          playhead.className = "ai-chat-voice-msg__playhead";
          playhead.dataset.role = "audio-playhead";
          inner.appendChild(playhead);
        }
        if (seek) inner.appendChild(seek);
      }
    }

    consolidateVoiceWaveformLayers(wrap);

    const tools = wrap.querySelector(".ai-chat-voice-msg__tools");
    if (tools && !tools.querySelector('[data-role="audio-volume-btn"]')) {
      const volumeWrap = document.createElement("div");
      volumeWrap.className = "ai-chat-voice-msg__volume-wrap";
      volumeWrap.innerHTML = `
        <button type="button" class="ai-chat-voice-msg__volume-btn" data-role="audio-volume-btn" title="Volume" aria-label="Volume">
          <span data-role="audio-volume-icon">${voiceVolumeIconSvg("high")}</span>
        </button>
        <div class="ai-chat-voice-msg__volume-pop d-none" data-role="audio-volume-pop">
          <input type="range" min="0" max="100" value="100" data-role="audio-volume-range" aria-label="Volume level" />
        </div>`;
      const download = tools.querySelector(".ai-chat-voice-msg__download");
      tools.insertBefore(volumeWrap, download || null);
    }
    tools?.querySelectorAll(".ai-chat-voice-msg__download, .ai-chat-voice-msg__notebook").forEach((a) => {
      a.classList.add("ai-chat-voice-msg__action");
      if (a.classList.contains("ai-chat-voice-msg__download") && !a.querySelector(".ai-chat-voice-msg__action-icon")) {
        a.innerHTML = `<span class="ai-chat-voice-msg__action-icon" aria-hidden="true">⬇</span> Download`;
      }
    });
    if (!wrap.dataset.volume) wrap.dataset.volume = "1";
  });
}

function bindChatAudioPlayers(root = chatWindow) {
  upgradeLegacyVoiceMessages(root);
  upgradeLegacyChatErrors(root);
  root?.querySelectorAll(".ai-chat-voice-msg").forEach((el) => {
    delete el.dataset.voiceBound;
    el._voicePlayer = null;
    el._audioEl = null;
    wireVoiceMessagePlayer(el);
  });
}

document.addEventListener("click", (ev) => {
  if (ev.target?.closest?.('[data-role="audio-volume-btn"], [data-role="audio-volume-pop"]')) return;
  chatWindow?.querySelectorAll(".ai-chat-voice-msg").forEach((wrap) => {
    wrap._voicePlayer?.closeVolumePop?.();
  });
});

function appendChatNarrationMessage(audioSource, sourceText, savedInfo = {}) {
  if (!chatWindow) return null;
  ensureChatVisible();

  const persistentUrl =
    savedInfo.audioUrl ||
    (typeof audioSource === "string" && !audioSource.startsWith("blob:") ? audioSource : "");
  const storeUrl = persistentUrl;
  const playbackUrl =
    storeUrl ||
    (typeof audioSource === "string" && !audioSource.startsWith("blob:")
      ? audioSource
      : typeof audioSource === "string"
        ? audioSource
        : URL.createObjectURL(audioSource));
  const notebookUrl = savedInfo.aiResultId
    ? `/Notebook/Details/${encodeURIComponent(savedInfo.aiResultId)}`
    : "";
  const downloadName = `narration-${savedInfo.aiResultId || Date.now()}.wav`;
  const langLabel = savedInfo.targetLanguage ? ` · → ${savedInfo.targetLanguage}` : "";
  const placeholderBars = renderVoiceWaveformBarsHtml(
    Array.from({ length: VOICE_WAVE_BAR_COUNT }, () => 0.18)
  );

  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--ai";
  wrap.innerHTML = `
    <div class="ai-chat-bubble-wrap">
      <div class="ai-chat-voice-msg" data-audio-url="${escapeHtml(storeUrl || playbackUrl)}" data-download-name="${escapeHtml(downloadName)}" data-volume="1">
        <div class="ai-chat-voice-msg__row">
          <button type="button" class="ai-chat-voice-msg__play" data-role="audio-play" title="Play" aria-label="Play">
            <span data-role="audio-play-icon">${VOICE_PLAY_ICON}</span>
          </button>
          <div class="ai-chat-voice-msg__body">
            <div class="ai-chat-voice-msg__wave" data-role="audio-wave">
              <div class="ai-chat-voice-msg__track">
                <div class="ai-chat-voice-msg__wave-inner" data-role="audio-wave-inner">
                  <div class="ai-chat-voice-msg__bars ai-chat-voice-msg__bars--bg" data-role="audio-bars">${placeholderBars}</div>
                  <div class="ai-chat-voice-msg__bars ai-chat-voice-msg__bars--played" data-role="audio-bars-played">${placeholderBars}</div>
                  <div class="ai-chat-voice-msg__playhead" data-role="audio-playhead"></div>
                  <input type="range" class="ai-chat-voice-msg__seek" data-role="audio-seek" min="0" max="100" value="0" aria-label="Seek" />
                </div>
              </div>
            </div>
            <div class="ai-chat-voice-msg__times">
              <span data-role="audio-time">0:00 / 0:00</span>
            </div>
          </div>
        </div>
        <div class="ai-chat-voice-msg__tools">
          <button type="button" class="ai-chat-voice-msg__skip" data-role="audio-rewind">−10s</button>
          <button type="button" class="ai-chat-voice-msg__skip" data-role="audio-forward">+10s</button>
          <div class="ai-chat-voice-msg__volume-wrap">
            <button type="button" class="ai-chat-voice-msg__volume-btn" data-role="audio-volume-btn" title="Volume" aria-label="Volume">
              <span data-role="audio-volume-icon">${voiceVolumeIconSvg("high")}</span>
            </button>
            <div class="ai-chat-voice-msg__volume-pop d-none" data-role="audio-volume-pop">
              <input type="range" min="0" max="100" value="100" data-role="audio-volume-range" aria-label="Volume level" />
            </div>
          </div>
          <a class="ai-chat-voice-msg__action ai-chat-voice-msg__download" data-role="audio-download" href="${escapeHtml(playbackUrl)}" download="${escapeHtml(downloadName)}">
            <span class="ai-chat-voice-msg__action-icon" aria-hidden="true">⬇</span> Download
          </a>
          ${notebookUrl ? `<a class="ai-chat-voice-msg__action ai-chat-voice-msg__notebook" href="${escapeHtml(notebookUrl)}">Notebook</a>` : ""}
        </div>
      </div>
      <div class="ai-chat-reactions">
        <span class="ai-chat-reaction ai-chat-reaction--operation">Narrate</span>
        ${savedInfo.targetLanguage ? `<span class="ai-chat-reaction ai-chat-reaction--lang">→ ${escapeHtml(savedInfo.targetLanguage)}</span>` : ""}
      </div>
      <div class="ai-chat-meta ai-chat-meta--voice">
        Gemini TTS${langLabel}${sourceText ? ` · ${escapeHtml(sourceText.slice(0, 72))}${sourceText.length > 72 ? "..." : ""}` : ""}
      </div>
    </div>`;

  chatWindow.appendChild(wrap);
  wireVoiceMessagePlayer(wrap.querySelector(".ai-chat-voice-msg"));
  chatMessageCount += 1;
  updateChatCount();
  persistAiChat();
  saveChatMessageToServer({
    role: "ai",
    messageType: "audio",
    text: `Voice message: ${(sourceText || "").slice(0, 240)}`,
    audioUrl: persistentUrl || playbackUrl,
    resultUrl: notebookUrl,
  });
  scrollChatToBottom();
  return wrap;
}

async function runNarrationFromForm(form) {
  const sourceId = form.dataset.sourceId || "";
  const source = aiChatSources.get(sourceId);
  if (!source?.text?.trim()) {
    setOcrStatus("error", "No text found for narration.");
    return;
  }
  const targetLanguage = form.querySelector('[data-field="targetLanguage"]')?.value || "English";
  form.closest(".ai-chat-message")?.remove();
  await createNarrationInChat(source.text, { targetLanguage, sourceId });
}

async function createNarrationInChat(text, options = {}) {
  const { targetLanguage = "English", triggerButton = null, sourceId = null } = options;
  dismissPendingChatOpSetups();
  const plain = (text || "").trim();
  if (!narrateSpeechUrl) {
    setOcrStatus("error", "Narration endpoint is not configured.");
    return;
  }
  if (!plain) {
    setOcrStatus("error", "No text found for narration.");
    return;
  }

  if (triggerButton) triggerButton.disabled = true;
  const controller = new AbortController();
  const typingNode = appendAiTypingPlaceholder(controller);
  setOcrStatus("info", "Preparing narration...", { loading: true });
  try {
    const r = await fetch(narrateSpeechUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken,
      },
      body: JSON.stringify({ documentId, text: plain, targetLanguage }),
      signal: controller.signal,
    });
    const ct = (r.headers.get("content-type") || "").toLowerCase();
    if (!r.ok) {
      let msg = "Narration failed.";
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
      replaceWithAiMessage(typingNode, { text: msg, isError: true });
      clearOcrStatus();
      return;
    }
    if (!ct.includes("audio")) {
      const msg = "Unexpected response (not an audio file).";
      replaceWithAiMessage(typingNode, { text: msg, isError: true });
      clearOcrStatus();
      return;
    }
    const aiResultId = r.headers.get("x-ai-result-id") || "";
    const audioUrl = r.headers.get("x-audio-url") || "";
    const blob = await r.blob();
    typingNode?.remove();
    appendChatNarrationMessage(audioUrl || blob, plain, { aiResultId, audioUrl, targetLanguage });
    clearOcrStatus();
  } catch (e) {
    if (e?.name === "AbortError") {
      replaceWithAiMessage(typingNode, { text: "⏹ Narration cancelled by user." });
      clearOcrStatus();
      return;
    }
    const msg = e?.message || "An error occurred during the narration request.";
    replaceWithAiMessage(typingNode, { text: msg, isError: true });
    clearOcrStatus();
  } finally {
    if (triggerButton) triggerButton.disabled = false;
  }
}

const btnExtractTextFromImage = document.getElementById("btn-extract-text-from-image");
async function extractTextFromCapturedImage() {
  if (!capturedImage?.base64) {
    setOcrStatus("error", "No captured image found.");
    return;
  }
  if (!aiProcessUrl || !documentId) {
    setOcrStatus("error", "AI endpoint is not configured.");
    return;
  }
  dismissPendingChatOpSetups();
  if (btnExtractTextFromImage) btnExtractTextFromImage.disabled = true;
  setOcrStatus("info", "Extracting text from image with AI...", { loading: true });
  try {
    const r = await fetch(aiProcessUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken,
      },
      body: JSON.stringify({
        documentId,
        operationType: "Rewrite",
        modelName: aiModel?.value || defaultAiModel,
        targetLanguage: "English",
        style: "Formal",
        customInstruction:
          "TRANSCRIPTION ONLY: Extract every visible character from the attached image exactly as printed. Preserve line breaks, columns, tabs, and reading order. Do NOT translate, summarize, rewrite, or add commentary. Output ONLY the raw extracted text.",
        inputText: "",
        inputImageBase64: capturedImage.base64,
        inputImageMimeType: capturedImage.mimeType || "image/png",
        sourcePageNumber: currentRect?.pageNumber || currentPage,
      }),
    });
    const res = await r.json();
    if (!res.ok) {
      setOcrStatus("error", res.message || "Text extraction failed.");
      return;
    }
    const text = (res.outputText || "").trim();
    if (!text) {
      setOcrStatus("error", "No text could be extracted from the image.");
      return;
    }
    if (ocrTextarea) ocrTextarea.value = text;
    textSourceId = createAiChatSource({ contentText: text });
    lastOcrChatSourceId = textSourceId;
    refreshComposeState({ pulse: true, justAdded: "text" });
    setOcrStatus("success", "Text extracted from image.");
    saveWsStateDebounced();
  } catch (e) {
    setOcrStatus("error", e?.message || "Text extraction failed.");
  } finally {
    if (btnExtractTextFromImage) btnExtractTextFromImage.disabled = false;
  }
}

if (btnExtractTextFromImage) {
  btnExtractTextFromImage.addEventListener("click", () => extractTextFromCapturedImage());
}

if (btnNarrateOcrSpeech && ocrTextarea) {
  btnNarrateOcrSpeech.addEventListener("click", () => {
    createNarrationInChat(ocrTextarea.value || "", { triggerButton: btnNarrateOcrSpeech });
  });
}

if (btnAiProcess) {
  btnAiProcess.addEventListener("click", () => {
    if (!aiProcessUrl || !documentId || !ocrTextarea) return;
    dismissPendingChatOpSetups();
    const inputText = (ocrTextarea.value || "").trim();
    const customInstruction = (aiInstruction?.value || "").trim();
    const hasImage = !!capturedImage;

    // [TR] Eskiden sadece OCR metni zorunluydu. Artık üç kaynaktan biri yeterli:
    //      1) OCR/serbest metin, 2) "Görsel Seç" ile yakalanmış görsel, veya
    //      3) "Özel Yönerge" alanına yazılmış prompt.
    if (!inputText && !hasImage && !customInstruction) {
      setAiStatus(
        "error",
        "OCR text, a captured image, or a custom instruction is required."
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
    const sourceId = createAiChatSource({
      contentText: inputText,
      imageDataUrl: userImageDataUrl,
      imageBase64: hasImage ? capturedImage.base64 : null,
      imageMimeType: hasImage ? capturedImage.mimeType : null,
    });

    appendUserMessage({
      imageDataUrl: userImageDataUrl,
      contentText: inputText,
      prompt: customInstruction,
      reactions,
      sourceId,
    });

    // ─── AI mesajı için "yazıyor..." balonu ekle ───────────────────────────
    const typingNode = appendAiTypingPlaceholder();

    // [TR] NLP görevi: ağ gecikmesi uzun olabildiği için #ai-status satırında spinner (+ "AI işlemi çalışıyor...").
    btnAiProcess.disabled = true;
    if (aiResultLink) aiResultLink.classList.add("d-none");
    setAiStatus("info", "AI operation is running...", { loading: true });

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
          clearAiStatus();
          replaceWithAiMessage(typingNode, {
            text: res.message || "AI operation failed.",
            isError: true,
          });
          return;
        }
        setAiStatus("success", res.message || "AI operation completed.");
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
        clearAiStatus();
        replaceWithAiMessage(typingNode, {
          text: "An error occurred during the AI request.",
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
      aiModel.innerHTML = '<option value="">No model found for this operation</option>';
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
  saveWsState();
});

btnNext.addEventListener("click", async () => {
  if (!pdfDoc || currentPage >= pdfDoc.numPages) return;
  currentPage += 1;
  clearDraftRect();
  setSelectionUi();
  await renderPage(currentPage);
  saveWsState();
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
  saveWsState();
});

btnZoomIn.addEventListener("click", async () => {
  zoom = Math.min(2.5, zoom + 0.1);
  setSelectionUi();
  await renderPage(currentPage);
  saveWsState();
});

btnZoomOut.addEventListener("click", async () => {
  zoom = Math.max(0.4, zoom - 0.1);
  setSelectionUi();
  await renderPage(currentPage);
  saveWsState();
});

btnFit.addEventListener("click", async () => {
  await fitToWidth();
  setSelectionUi();
  saveWsState();
});

// [TR] PDF kaydırıldıkça (scroll) son konumu kaydet.
if (viewerFrame) {
  viewerFrame.addEventListener("scroll", saveWsStateDebounced, { passive: true });
}

if (btnOpenNativePreview) {
  btnOpenNativePreview.addEventListener("click", () => {
    openNativePreview();
  });
}

// ─── CREATIVE (3. tur): GÖRSEL BÜYÜTME (LIGHTBOX) + OCR METNİ GENİŞLET ───────
const imageLightbox = document.getElementById("image-lightbox");
const imageLightboxImg = document.getElementById("image-lightbox-img");
const imageLightboxClose = document.getElementById("image-lightbox-close");
const btnExpandOcr = document.getElementById("btn-expand-ocr");

function openImageLightbox(src) {
  if (!imageLightbox || !imageLightboxImg || !src) return;
  imageLightboxImg.src = src;
  imageLightbox.classList.remove("d-none");
}
function closeImageLightbox() {
  imageLightbox?.classList.add("d-none");
  if (imageLightboxImg) imageLightboxImg.src = "";
}

// [TR] Yakalanan görsele tıklayınca ortada büyük pencerede aç.
if (capturedImagePreview) {
  capturedImagePreview.style.cursor = "zoom-in";
  capturedImagePreview.addEventListener("click", () => openImageLightbox(capturedImagePreview.src));
}
if (imageLightboxClose) imageLightboxClose.addEventListener("click", closeImageLightbox);
if (imageLightbox) {
  imageLightbox.addEventListener("click", (ev) => {
    if (ev.target === imageLightbox) closeImageLightbox(); // sadece arka plana tıklayınca kapat
  });
}
window.addEventListener("keydown", (ev) => {
  if (ev.key === "Escape" && imageLightbox && !imageLightbox.classList.contains("d-none")) {
    closeImageLightbox();
  }
});

// [TR] Sohbetteki görsellere tıklayınca da büyüt.
if (chatWindow) {
  chatWindow.addEventListener("click", (ev) => {
    const img = ev.target?.closest?.(".ai-chat-bubble img");
    if (img && img.src) openImageLightbox(img.src);
  });
}

// [TR] Extracted OCR text alanını yukarı doğru overlay olarak genişlet/daralt.
function setOcrTextExpanded(expanded) {
  if (!ocrTextPanel || !workspaceCompose || !btnExpandOcr) return;
  if (expanded && !ocrTextPanel.classList.contains("d-none")) {
    workspaceCompose.style.setProperty("--ocr-slot-height", `${ocrTextPanel.offsetHeight}px`);
    ocrTextPanel.classList.add("compose-source--expanded");
    btnExpandOcr.textContent = "⤡ Collapse";
    ocrTextarea?.focus();
  } else {
    ocrTextPanel.classList.remove("compose-source--expanded");
    workspaceCompose.style.removeProperty("--ocr-slot-height");
    btnExpandOcr.textContent = "⤢ Expand";
  }
}

if (btnExpandOcr && ocrTextPanel && workspaceCompose) {
  btnExpandOcr.addEventListener("click", () => {
    const willExpand = !ocrTextPanel.classList.contains("compose-source--expanded");
    setOcrTextExpanded(willExpand);
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
    // [TR] Önceki oturumdan kayıtlı durum (sayfa/zoom/kaydırma/bölge) varsa geri yükle.
    const saved = loadWsState();
    currentPage = Math.max(1, Math.min(pdfDoc ? pdfDoc.numPages : 1, saved?.page || currentPage));

    syncViewerPanRoom();
    if (saved && Number.isFinite(saved.zoom)) {
      // [TR] Kayıtlı zoom'u kullan.
      zoom = Math.max(0.4, Math.min(2.5, saved.zoom));
      await renderPage(currentPage);
    } else {
      // [TR] İlk açılış: PDF'i görünür alana sığdır (fit-to-width).
      await fitToWidth();
    }

    setSelectionUi();
    setRegionInfo(null);

    // [TR] Kayıtlı kaydırma konumunu ve seçili bölgeyi geri getir.
    if (saved) {
      if (viewerFrame) {
        viewerFrame.scrollLeft = saved.scrollLeft || 0;
        viewerFrame.scrollTop = saved.scrollTop || 0;
      }
      if (saved.region && saved.region.pageNumber === currentPage) {
        applyRegionNormalized(saved.region);
        setSelectionUi();
      }

      // [TR] Kayıtlı extracted text / görsel kaynaklarını geri yükle → kullanıcı
      //      kaldığı yerden devam eder, fonksiyonlar kilitli olmaz.
      currentComposeKind = saved.composeKind || null;
      if (typeof saved.ocrText === "string" && saved.ocrText.length) {
        if (ocrTextarea) ocrTextarea.value = saved.ocrText;
        textSourceId = createAiChatSource({ contentText: saved.ocrText });
        lastOcrChatSourceId = textSourceId;
        if (saved.ocrResultId) {
          lastOcrResultId = saved.ocrResultId;
          if (btnSaveOcrText) btnSaveOcrText.disabled = false;
        }
      }
      if (saved.image && saved.image.dataUrl) {
        capturedImage = { base64: saved.image.base64, mimeType: saved.image.mime || "image/png" };
        showCapturedImagePreview(saved.image.dataUrl);
        imageSourceId = createAiChatSource({
          imageDataUrl: saved.image.dataUrl,
          imageBase64: saved.image.base64,
          imageMimeType: saved.image.mime || "image/png",
        });
        lastImageChatSourceId = imageSourceId;
      }
    }

    // [TR] Sunucudan gelen başlangıç OCR metni varsa (kayıt yoksa) onu da kaynak yap.
    if (!textSourceId && ocrTextarea && ocrTextarea.value.trim()) {
      textSourceId = createAiChatSource({ contentText: ocrTextarea.value });
      lastOcrChatSourceId = textSourceId;
    }
    // [TR] Panelleri/seçimi tazele (pulse yok — sessiz geri yükleme). Tek kaynak
    //      varsa otomatik seçili → fonksiyonlar kilitli olmaz.
    refreshComposeState({ pulse: false });

    chatWindow?.querySelectorAll(".ai-chat-message[data-source-id]").forEach((el) => {
      if (el.dataset.sourceId) ensureSourceInMemory(el.dataset.sourceId);
    });
    persistAiChat();

    if (lastOcrResultId && btnSaveOcrText) btnSaveOcrText.disabled = false;
    // [TR] Sayfa açılışında varsayılan işlem (Translate) için model listesi yüklenir.
    //      Kullanıcının en çok kullandığı model (defaultModelFromAttr) otomatik seçilir.
    await loadModelsForTask(aiOperation ? aiOperation.value : "Translate");
    if (aiStyle) aiStyle.value = defaultStyle;
    empty?.classList.add("d-none");
    warning?.classList.add("d-none");
    positionResizerHint();
  } catch (err) {
    console.error(err);
    // [TR] Yalnızca PDF gerçekten yüklenemediyse uyarı göster; diğer hatalarda
    //      mavi/sarı banner'lar düzeni bozmasın.
    if (canvas && canvas.width === 0) {
      empty?.classList.remove("d-none");
    }
  }
}

// [TR] Sekme kapanmadan önce / başka sayfaya gidilirken son durumu yaz (geri tuşu vb.).
window.addEventListener("pagehide", () => {
  persistAiChat();
  saveWsState();
});

// [TR] Pencere yeniden boyutlanınca panel altı kaydırma boşluğunu yenile.
window.addEventListener("resize", () => {
  syncViewerPanRoom();
  positionResizerHint();
});

attachShowMoreHandler();
restoreAiChat();

init();

