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
    btn.textContent = expanded ? "Daha az göster" : "Devamını göster";
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
      "Belge kimliği bulunamadı; sohbet geçmişi gösterilemiyor.";
    emptyEl.classList.remove("d-none");
    return;
  }

  let raw = null;
  try {
    raw = localStorage.getItem(storageKey(documentId));
  } catch {
    emptyEl.textContent =
      "Tarayıcı yerel depoya erişilemedi (gizli mod / politika). Sohbet geçmişi gösterilemiyor.";
    emptyEl.classList.remove("d-none");
    return;
  }

  if (!raw) {
    emptyEl.innerHTML =
      'Bu belge için bu tarayıcıda kayıtlı AI sohbeti yok. Workspace’te <strong>AI ile İşle</strong> kullandığınızda sohbet burada da görünür.';
    emptyEl.classList.remove("d-none");
    return;
  }

  let data = null;
  try {
    data = JSON.parse(raw);
  } catch {
    emptyEl.textContent = "Sohbet verisi okunamadı.";
    emptyEl.classList.remove("d-none");
    return;
  }

  if (!data || typeof data.html !== "string" || !data.html.trim()) {
    emptyEl.textContent =
      "Kayıtlı sohbet boş. Belge çalışma alanında sohbeti silmiş veya henüz mesaj oluşmamış olabilirsiniz.";
    return;
  }

  win.innerHTML = data.html;
  win.classList.remove("d-none");
  emptyEl.classList.add("d-none");
  attachShowMoreHandler(win);
  win.scrollTop = win.scrollHeight;
}

initAiResultChatHistory();
