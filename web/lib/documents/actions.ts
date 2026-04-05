"use server";

import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import {
  MAX_UPLOAD_BYTES,
  ALLOWED_UPLOAD_MIME,
  PDF_MAGIC,
} from "./constants";
import { addDocument, removeDocument } from "./mock-store";
import type { DocumentStatusValue } from "@/lib/types/enums";

export type UploadResult = { ok: true } | { ok: false; error: string };

function isPdfBuffer(buf: Buffer): boolean {
  const head = buf.subarray(0, Math.min(5, buf.length)).toString("utf8");
  return head.startsWith(PDF_MAGIC);
}

export async function uploadPdfAction(formData: FormData): Promise<UploadResult> {
  const session = getSession();
  if (!session) {
    return { ok: false, error: "Oturum bulunamadı. Lütfen tekrar giriş yapın." };
  }

  const titleRaw = String(formData.get("title") ?? "").trim();
  const file = formData.get("file");

  if (!(file instanceof File)) {
    return { ok: false, error: "Dosya seçilmedi." };
  }

  if (file.size > MAX_UPLOAD_BYTES) {
    return { ok: false, error: `Dosya çok büyük. En fazla ${MAX_UPLOAD_BYTES / (1024 * 1024)} MB (PDF).` };
  }

  if (file.type && file.type !== ALLOWED_UPLOAD_MIME) {
    return { ok: false, error: "Yalnızca PDF yükleyebilirsiniz." };
  }

  const lower = file.name.toLowerCase();
  if (!lower.endsWith(".pdf")) {
    return { ok: false, error: "Dosya uzantısı .pdf olmalıdır." };
  }

  const buf = Buffer.from(await file.arrayBuffer());
  if (!isPdfBuffer(buf)) {
    return { ok: false, error: "Geçerli bir PDF dosyası değil (başlık kontrolü)." };
  }

  const title = titleRaw || file.name.replace(/\.pdf$/i, "") || "Adsız belge";

  addDocument({
    id: crypto.randomUUID(),
    ownerEmail: session.email,
    title,
    fileName: file.name,
    mimeType: ALLOWED_UPLOAD_MIME,
    sizeBytes: file.size,
    status: "UPLOADED" as DocumentStatusValue,
    storagePath: `mock/uploads/${session.email}/${Date.now()}-${file.name}`,
  });

  return { ok: true };
}

export async function deleteDocumentAction(formData: FormData): Promise<void> {
  const session = getSession();
  const id = String(formData.get("id") ?? "");
  if (!session || !id) redirect("/app/documents");
  removeDocument(session.email, id);
  redirect("/app/documents");
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: PDF yükleme ve silme sunucu eylemleri; mock depoya yazar, diske yazmaz.
 * [TR] Neden gerekli: FormData ile güvenli sunucu tarafı doğrulama ve tekil başarı/hata cevabı.
 * [TR] Sistem içinde: upload formu ve belge sil düğmesi.
 *
 * MODIFICATION NOTES (TR)
 * - AWS S3 / Azure Blob: storagePath gerçek URL ve yükleme SDK çağrısı eklenir.
 * - DOCX: MIME ve magic kontrolü ayrı dal; dönüştürme servisi tetiklenir.
 * - Çoklu dosya: döngü ve kısmi başarı raporu.
 * - Görüntüden metin (genel OCR): kapsam dışı; ileride çalışma olarak iş paketi açılır.
 * - Zorluk: Orta–yüksek (üretim güvenliği ve I/O).
 * -----------------------------------------------------------------------------
 */
