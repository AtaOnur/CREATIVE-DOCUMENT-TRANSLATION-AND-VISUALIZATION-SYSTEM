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
    (n) => !n.querySelector(".ai-chat-typing") && !n.querySelector(".ai-chat-audio-player")
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
    console.warn("AI chat could not be written to local storage:", e);
  }
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

function buildChatOperationButtons(sourceId, { hasText, hasImage }) {
  if (!sourceId) return "";
  const buttons = AI_CHAT_OPERATION_DEFS
    .filter((d) => (d.needsText ? hasText : d.needsAny ? hasText || hasImage : true))
    .map(
      (d) =>
        `<button type="button" class="ai-chat-op-button" data-role="chat-op" data-source-id="${escapeHtml(sourceId)}" data-operation="${escapeHtml(d.op)}">` +
        `<span aria-hidden="true">${d.icon}</span><span>${escapeHtml(d.label)}</span>` +
        "</button>"
    )
    .join("");
  return `<div class="ai-chat-op-row" aria-label="AI operations">${buttons}</div>`;
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

  inner.push("</div>"); // bubble
  inner.push(buildReactionChips(reactions));
  inner.push(
    buildChatOperationButtons(sourceId, {
      hasText: !!(contentText && contentText.trim()),
      hasImage: !!imageDataUrl,
    })
  );
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
  const setupId = `chat-setup-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const opLabel = AI_CHAT_OPERATION_DEFS.find((d) => d.op === operation)?.label || operation;
  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--ai";
  wrap.innerHTML = `
    <div class="ai-chat-bubble-wrap ai-chat-bubble-wrap--wide">
      <form class="ai-chat-op-form" data-role="chat-op-form" data-source-id="${escapeHtml(sourceId)}" data-operation="${escapeHtml(operation)}" id="${escapeHtml(setupId)}">
        <div class="ai-chat-op-form__title">${escapeHtml(opLabel)} settings</div>
        <div class="ai-chat-op-form__grid">
          <label class="ai-chat-op-field ${operation === "Translate" ? "" : "d-none"}">
            <span>Hedef dil</span>
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
          <label class="ai-chat-op-field">
            <span>Stil</span>
            <select class="form-select form-select-sm" data-field="style">
              <option value="Formal"${defaultStyle === "Formal" ? " selected" : ""}>Formal</option>
              <option value="Academic"${defaultStyle === "Academic" ? " selected" : ""}>Academic</option>
              <option value="Simplified"${defaultStyle === "Simplified" ? " selected" : ""}>Simplified</option>
            </select>
          </label>
          <label class="ai-chat-op-field">
            <span>Model</span>
            <select class="form-select form-select-sm" data-field="modelName">${buildChatModelOptions()}</select>
          </label>
        </div>
        <label class="ai-chat-op-field mt-2">
          <span>Custom prompt</span>
          <textarea class="form-control form-control-sm" rows="2" data-field="customInstruction" placeholder="Add an optional instruction"></textarea>
        </label>
        <div class="ai-chat-op-form__actions">
          <button type="submit" class="btn btn-sm btn-dark">Run</button>
          <button type="button" class="btn btn-sm btn-outline-secondary" data-role="chat-op-cancel">Cancel</button>
        </div>
      </form>
    </div>`;
  chatWindow.appendChild(wrap);
  scrollChatToBottom();
}

async function runChatOperation(form) {
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
  if (operation === "Translate") reactions.push({ kind: "lang", label: `→ ${payload.targetLanguage}` });

  appendUserMessage({
    imageDataUrl: null,
    contentText: `AI operation: ${operation}`,
    prompt: payload.customInstruction,
    reactions,
  });

  const typingNode = appendAiTypingPlaceholder();
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
    });
    const res = await r.json();
    if (!res.ok) {
      setAiStatus("error", res.message || "AI operation failed.");
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
  } catch {
    setAiStatus("error", "An error occurred during the AI request.");
    replaceWithAiMessage(typingNode, { text: "An error occurred during the AI request.", isError: true });
  }
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
    parts.push(`<img src="${escapeHtml(imageUrl)}" alt="AI response image" />`);
  }
  if (!text && !imageUrl) {
    parts.push('<p class="ai-chat-bubble-text text-muted"><em>(empty response)</em></p>');
  }

  parts.push("</div>"); // bubble

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
  });
}

if (chatWindow) {
  chatWindow.addEventListener("click", (ev) => {
    const opBtn = ev.target?.closest?.('[data-role="chat-op"]');
    if (opBtn) {
      const sourceId = opBtn.dataset.sourceId || "";
      const operation = opBtn.dataset.operation || "";
      const source = aiChatSources.get(sourceId);
      if (operation === "Narrate") {
        if (!source?.text?.trim()) {
          setOcrStatus("error", "No text found for narration.");
          return;
        }
        createNarrationInChat(source.text, opBtn);
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
    runChatOperation(form);
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
      console.warn("pdf.js render error; server fallback will be tried:", err);
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
  setOcrStatus("info", `OCR is running... (${selectedEngine || "default"})`, {
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
        setOcrStatus("error", res.message || "OCR failed.");
        return;
      }
      if (ocrTextarea) ocrTextarea.value = res.text || "";
      lastOcrResultId = res.ocrResultId || "";
      if (btnSaveOcrText) btnSaveOcrText.disabled = !lastOcrResultId;
      setOcrStatus("success", res.message || "OCR completed.");
      if ((res.text || "").trim()) {
        const sourceId = createAiChatSource({ contentText: res.text || "" });
        lastOcrChatSourceId = sourceId;
        appendUserMessage({
          contentText: res.text || "",
          reactions: [
            { kind: "operation", label: "OCR" },
            { kind: "model", label: selectedEngine || "Default" },
          ],
          sourceId,
        });
      }
    })
    .catch(() => setOcrStatus("error", "An error occurred during the OCR request."))
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
    const sourceId = createAiChatSource({
      imageDataUrl: captured.dataUrl,
      imageBase64: captured.base64,
      imageMimeType: captured.mimeType,
      contentText: ocrTextarea?.value || "",
    });
    lastImageChatSourceId = sourceId;
    appendUserMessage({
      imageDataUrl: captured.dataUrl,
      contentText: ocrTextarea?.value || "",
      reactions: [{ kind: "operation", label: "Select Image" }],
      sourceId,
    });
    setOcrStatus("success", "Image captured. Use the chat operation buttons to send it with a prompt.");
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
          setOcrStatus("error", res.message || "Save failed.");
          btnSaveOcrText.disabled = false;
          return;
        }
        const savedText = ocrTextarea.value || "";
        const updatedOcr = updateAiChatSourceText(lastOcrChatSourceId, savedText);
        const updatedImage = updateAiChatSourceText(lastImageChatSourceId, savedText);
        if (!updatedOcr && savedText.trim()) {
          const sourceId = createAiChatSource({ contentText: savedText });
          lastOcrChatSourceId = sourceId;
          appendUserMessage({
            contentText: savedText,
            reactions: [
              { kind: "operation", label: "OCR Updated" },
              { kind: "style", label: "Saved text" },
            ],
            sourceId,
          });
        } else if (updatedOcr || updatedImage) {
          persistAiChat();
        }
        setOcrStatus("success", res.message || "OCR metni kaydedildi.");
        btnSaveOcrText.disabled = false;
      })
      .catch(() => {
        setOcrStatus("error", "An error occurred while saving.");
        btnSaveOcrText.disabled = false;
      });
  });
}

// ─── OCR/Gemini TTS → AI sohbet içinde ses çıktısı ──────────────────────────
// [TR] Ses artık ayrı panelde değil; chat içinde AI balonu olarak üretilir.
//      Sunucu kaydı dönerse player kalıcı /ai-audio URL'sini kullanır ve notebook linki gösterir.
function appendChatNarrationMessage(audioSource, sourceText, savedInfo = {}) {
  if (!chatWindow) return null;
  ensureChatVisible();

  const objectUrl = typeof audioSource === "string" ? audioSource : URL.createObjectURL(audioSource);
  const audioEl = new Audio(objectUrl);
  const notebookUrl = savedInfo.aiResultId ? `/Notebook/Details/${encodeURIComponent(savedInfo.aiResultId)}` : "";
  const wrap = document.createElement("div");
  wrap.className = "ai-chat-message ai-chat-message--ai";
  wrap.innerHTML = `
    <div class="ai-chat-bubble-wrap ai-chat-bubble-wrap--wide">
      <div class="ai-chat-bubble">
        <div class="ai-chat-audio-player">
          <div class="ai-chat-audio-player__title">Audio output</div>
          <div class="ai-chat-audio-player__controls">
            <button type="button" class="ai-chat-audio-player__btn" data-role="audio-rewind" title="10 saniye geri" aria-label="10 saniye geri">⏮</button>
            <button type="button" class="ai-chat-audio-player__btn ai-chat-audio-player__btn--main" data-role="audio-play" title="Play" aria-label="Play">
              <span data-role="audio-play-icon">▶</span>
            </button>
            <button type="button" class="ai-chat-audio-player__btn" data-role="audio-stop" title="Stop and rewind" aria-label="Stop and rewind">■</button>
            <button type="button" class="ai-chat-audio-player__btn" data-role="audio-replay" title="Replay" aria-label="Replay">↻</button>
            <button type="button" class="ai-chat-audio-player__btn" data-role="audio-forward" title="10 saniye ileri" aria-label="10 saniye ileri">⏭</button>
            <span class="ai-chat-audio-player__time" data-role="audio-time">0:00</span>
          </div>
        </div>
      </div>
      <div class="ai-chat-meta">
        Gemini TTS · ${escapeHtml((sourceText || "").slice(0, 72))}${sourceText && sourceText.length > 72 ? "..." : ""}
        ${notebookUrl ? ` · <a href="${notebookUrl}">Open in Notebook</a>` : ""}
      </div>
    </div>`;

  const playBtn = wrap.querySelector('[data-role="audio-play"]');
  const playIcon = wrap.querySelector('[data-role="audio-play-icon"]');
  const timeEl = wrap.querySelector('[data-role="audio-time"]');
  const setTime = () => {
    if (timeEl) timeEl.textContent = formatNarrateTime(audioEl.currentTime);
  };
  const setPlaying = () => {
    if (!playIcon || !playBtn) return;
    const playing = !audioEl.paused && !audioEl.ended;
    playIcon.textContent = playing ? "⏸" : "▶";
    const label = playing ? "Pause" : "Play";
    playBtn.title = label;
    playBtn.setAttribute("aria-label", label);
  };

  wrap.querySelector('[data-role="audio-rewind"]')?.addEventListener("click", () => {
    audioEl.currentTime = Math.max(0, audioEl.currentTime - NARRATE_SEEK_SECONDS);
    setTime();
  });
  wrap.querySelector('[data-role="audio-forward"]')?.addEventListener("click", () => {
    const dur = audioEl.duration;
    const max = Number.isFinite(dur) && dur > 0 ? dur : audioEl.currentTime + NARRATE_SEEK_SECONDS;
    audioEl.currentTime = Math.min(max, audioEl.currentTime + NARRATE_SEEK_SECONDS);
    setTime();
  });
  wrap.querySelector('[data-role="audio-stop"]')?.addEventListener("click", () => {
    audioEl.pause();
    audioEl.currentTime = 0;
    setTime();
    setPlaying();
  });
  wrap.querySelector('[data-role="audio-replay"]')?.addEventListener("click", async () => {
    audioEl.currentTime = 0;
    setTime();
    try {
      await audioEl.play();
    } catch (e) {
      setOcrStatus("error", e?.message || "Audio could not be replayed.");
    }
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

  audioEl.onplay = () => {
    setPlaying();
    clearOcrStatus();
  };
  audioEl.onpause = () => {
    setTime();
    setPlaying();
  };
  audioEl.onended = () => {
    setTime();
    setPlaying();
  };
  audioEl.ontimeupdate = setTime;

  chatWindow.appendChild(wrap);
  chatMessageCount += 1;
  updateChatCount();
  saveChatMessageToServer({
    role: "ai",
    messageType: "audio",
    text: `Audio output: ${(sourceText || "").slice(0, 240)}`,
    audioUrl: savedInfo.audioUrl || (typeof audioSource === "string" ? audioSource : ""),
    resultUrl: notebookUrl,
  });
  scrollChatToBottom();

  audioEl.play().catch(() => {
    setPlaying();
  });
  return wrap;
}

async function createNarrationInChat(text, triggerButton = null) {
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
  const typingNode = appendAiTypingPlaceholder();
  setOcrStatus("info", "Metin Gemini TTS ile seslendiriliyor...", { loading: true });
  try {
    const r = await fetch(narrateSpeechUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken,
      },
      body: JSON.stringify({ documentId, text: plain }),
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
      setOcrStatus("error", msg);
      replaceWithAiMessage(typingNode, { text: msg, isError: true });
      return;
    }
    if (!ct.includes("audio")) {
      const msg = "Unexpected response (not an audio file).";
      setOcrStatus("error", msg);
      replaceWithAiMessage(typingNode, { text: msg, isError: true });
      return;
    }
    const aiResultId = r.headers.get("x-ai-result-id") || "";
    const audioUrl = r.headers.get("x-audio-url") || "";
    const blob = await r.blob();
    typingNode?.remove();
    appendChatNarrationMessage(audioUrl || blob, plain, { aiResultId, audioUrl });
    clearOcrStatus();
  } catch (e) {
    const msg = e?.message || "An error occurred during the narration request.";
    setOcrStatus("error", msg.length > 240 ? `${msg.slice(0, 240)}...` : msg);
    replaceWithAiMessage(typingNode, { text: msg, isError: true });
  } finally {
    if (triggerButton) triggerButton.disabled = false;
  }
}

if (btnNarrateOcrSpeech && ocrTextarea) {
  btnNarrateOcrSpeech.addEventListener("click", () => {
    createNarrationInChat(ocrTextarea.value || "", btnNarrateOcrSpeech);
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
          setAiStatus("error", res.message || "AI operation failed.");
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
        setAiStatus("error", "An error occurred during the AI request.");
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

