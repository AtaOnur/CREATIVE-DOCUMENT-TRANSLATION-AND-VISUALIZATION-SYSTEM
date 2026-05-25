/*
 * [TR] AI sonuç detay sayfasında workspace ile aynı localStorage anahtarından sohbet geçmişini okur.
 * [TR] Anahtar pdf-workspace.mjs ile senkron tutulmalı: AI_CHAT_STORAGE_PREFIX + ":" + documentId
 *
 * MODIFICATION NOTES (TR)
 * - Sunucu tarafı sohbet kalıcılığı eklendiğinde bu modül genişletilebilir veya kaldırılabilir.
 */
const AI_CHAT_STORAGE_PREFIX = "pdf_bitirme_ai_chat_v1";

function storageKey(documentId) {
  return `${AI_CHAT_STORAGE_PREFIX}:${documentId}`;
}

function attachShowMoreHandler(container) {
  if (!container || container.dataset.showMoreBound === "1") return;
  container.dataset.showMoreBound = "1";
  container.addEventListener("click", (ev) => {
    const btn = ev.target?.closest?.('[data-role="chat-show-more"]');
    if (!btn) return;
    const bubble = btn.previousElementSibling;
    if (!bubble?.classList?.contains("ai-chat-bubble-text--collapsible")) return;
    const expanded = bubble.classList.toggle("is-expanded");
    btn.textContent = expanded ? "Show less" : "Show more";
  });
}

function initAiResultChatHistory() {
  const mount = document.getElementById("ai-result-chat-mount");
  const win = document.getElementById("ai-result-chat-window");
  const emptyEl = document.getElementById("ai-result-chat-empty");
  if (!mount || !win || !emptyEl) return;

  const documentId = mount.dataset.documentId?.trim();
  if (!documentId) {
    emptyEl.textContent =
      "Document ID was not found; chat history cannot be displayed.";
    emptyEl.classList.remove("d-none");
    return;
  }

  let raw = null;
  try {
    raw = localStorage.getItem(storageKey(documentId));
  } catch {
    emptyEl.textContent =
      "Browser local storage could not be accessed (private mode or policy). Chat history cannot be displayed.";
    emptyEl.classList.remove("d-none");
    return;
  }

  if (!raw) {
    emptyEl.innerHTML =
      'No AI chat is stored in this browser for this document. When you use <strong>Process with AI</strong> in the workspace, the chat will also appear here.';
    emptyEl.classList.remove("d-none");
    return;
  }

  let data = null;
  try {
    data = JSON.parse(raw);
  } catch {
    emptyEl.textContent = "Chat data could not be read.";
    emptyEl.classList.remove("d-none");
    return;
  }

  if (!data || typeof data.html !== "string" || !data.html.trim()) {
    emptyEl.textContent =
      "The saved chat is empty. You may have cleared the chat in the document workspace or no messages have been created yet.";
    return;
  }

  win.innerHTML = data.html;
  win.classList.remove("d-none");
  emptyEl.classList.add("d-none");
  attachShowMoreHandler(win);
  win.scrollTop = win.scrollHeight;
}

initAiResultChatHistory();
