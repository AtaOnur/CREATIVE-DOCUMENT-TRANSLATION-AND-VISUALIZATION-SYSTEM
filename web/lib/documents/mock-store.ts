import type { MockDocument, ActivityEntry } from "./types";
import type { DocumentStatusValue } from "@/lib/types/enums";

/** Demo kullanıcıda savunma için örnek veri; diğer e-postalar boş başlar (yükleme ile dolar). */
const DEMO_EMAIL = "demo.student@university.edu";

const now = () => new Date().toISOString();

let documents: MockDocument[] = [
  {
    id: "seed-thesis",
    ownerEmail: DEMO_EMAIL,
    title: "Tez taslağı — Bölüm 2",
    fileName: "tez-bolum-2.pdf",
    mimeType: "application/pdf",
    sizeBytes: 1_048_576,
    status: "OCR_READY",
    createdAt: new Date(Date.now() - 86_400_000).toISOString(),
    updatedAt: new Date(Date.now() - 3_600_000).toISOString(),
    storagePath: "mock/uploads/tez-bolum-2.pdf",
  },
  {
    id: "seed-literature",
    ownerEmail: DEMO_EMAIL,
    title: "Literatür notları",
    fileName: "literatur.pdf",
    mimeType: "application/pdf",
    sizeBytes: 512_000,
    status: "PROCESSING",
    createdAt: new Date(Date.now() - 172_800_000).toISOString(),
    updatedAt: new Date(Date.now() - 86_400_000).toISOString(),
    storagePath: "mock/uploads/literatur.pdf",
  },
];

let activities: ActivityEntry[] = [
  {
    id: "act-1",
    at: new Date(Date.now() - 3_600_000).toISOString(),
    message: `"Tez taslağı — Bölüm 2" durumu OCR_READY olarak güncellendi (mock).`,
  },
  {
    id: "act-2",
    at: new Date(Date.now() - 86_400_000).toISOString(),
    message: `"Literatür notları" işleniyor (mock).`,
  },
];

const MAX_ACTIVITY = 30;

function pushActivity(message: string) {
  activities = [
    { id: crypto.randomUUID(), at: now(), message },
    ...activities.filter((_, i) => i < MAX_ACTIVITY - 1),
  ];
}

export function listDocumentsForUser(ownerEmail: string): MockDocument[] {
  return documents
    .filter((d) => d.ownerEmail === ownerEmail)
    .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());
}

export function filterDocuments(
  ownerEmail: string,
  opts: { query?: string; status?: DocumentStatusValue | "" }
): MockDocument[] {
  let list = listDocumentsForUser(ownerEmail);
  const q = opts.query?.trim().toLowerCase();
  if (q) {
    list = list.filter(
      (d) =>
        d.title.toLowerCase().includes(q) ||
        d.fileName.toLowerCase().includes(q) ||
        d.id.toLowerCase().includes(q)
    );
  }
  if (opts.status) {
    list = list.filter((d) => d.status === opts.status);
  }
  return list;
}

export function getDocumentById(ownerEmail: string, id: string): MockDocument | undefined {
  return documents.find((d) => d.ownerEmail === ownerEmail && d.id === id);
}

export function addDocument(doc: Omit<MockDocument, "createdAt" | "updatedAt">) {
  const t = now();
  const full: MockDocument = { ...doc, createdAt: t, updatedAt: t };
  documents = [full, ...documents];
  pushActivity(`"${full.title}" yüklendi (PDF, mock).`);
}

export function removeDocument(ownerEmail: string, id: string): boolean {
  const before = documents.length;
  const removed = documents.find((d) => d.ownerEmail === ownerEmail && d.id === id);
  documents = documents.filter((d) => !(d.ownerEmail === ownerEmail && d.id === id));
  if (removed && documents.length < before) {
    pushActivity(`"${removed.title}" silindi (mock).`);
    return true;
  }
  return false;
}

export function getRecentActivity(limit = 8): ActivityEntry[] {
  return activities.slice(0, limit);
}

export function dashboardStats(ownerEmail: string) {
  const list = listDocumentsForUser(ownerEmail);
  const count = (s: DocumentStatusValue) => list.filter((d) => d.status === s).length;
  return {
    total: list.length,
    uploaded: count("UPLOADED"),
    processing: count("PROCESSING"),
    ocrReady: count("OCR_READY"),
    aiReady: count("AI_READY"),
    completed: count("COMPLETED"),
    failed: count("FAILED"),
  };
}

export function continueWorking(ownerEmail: string, limit = 3): MockDocument[] {
  const open: DocumentStatusValue[] = ["UPLOADED", "PROCESSING", "OCR_READY", "AI_READY"];
  return listDocumentsForUser(ownerEmail)
    .filter((d) => open.includes(d.status))
    .slice(0, limit);
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Bellek içi mock belge listesi ve aktivite günlüğü; yükleme/silme burada tutulur.
 * [TR] Neden gerekli: Savunmada akışı uçtan uca göstermek için veritabanı olmadan da çalışan ince katman.
 * [TR] Sistem içinde: lib/documents/actions.ts ve sunucu bileşenleri doğrudan import eder.
 *
 * MODIFICATION NOTES (TR)
 * - Prisma: bu fonksiyonların yerini repository + DB sorguları alır; imzalar korunabilir.
 * - Sunucusuz (Vercel) uyarısı: process örneği başına bellek; üretimde kalıcı store şart.
 * - Çoklu kullanıcı: ownerEmail zaten ayrıştırıyor; ileride kurum/tenant id eklenebilir.
 * - Zorluk: Orta (gerçek depoya geçiş).
 * -----------------------------------------------------------------------------
 */
