import { PrismaClient } from "@prisma/client";

const prisma = new PrismaClient();

const DEMO_USER_EMAIL = "demo.student@university.edu";

async function main() {
  await prisma.auditLog.deleteMany();
  await prisma.aiResult.deleteMany();
  await prisma.ocrResult.deleteMany();
  await prisma.regionSelection.deleteMany();
  await prisma.notebookEntry.deleteMany();
  await prisma.document.deleteMany();
  await prisma.userPreference.deleteMany();
  await prisma.user.deleteMany();

  const demoUser = await prisma.user.create({
    data: {
      email: DEMO_USER_EMAIL,
      name: "Demo Graduate Student",
      role: "USER",
      preferences: {
        create: {
          defaultStyle: "ACADEMIC",
          preferredLanguage: "tr",
        },
      },
    },
    include: { preferences: true },
  });

  const doc1 = await prisma.document.create({
    data: {
      userId: demoUser.id,
      title: "Sample Research Chapter (PDF placeholder)",
      fileName: "sample-chapter.pdf",
      storagePath: "uploads/demo/sample-chapter.pdf",
      status: "AI_READY",
    },
  });

  const doc2 = await prisma.document.create({
    data: {
      userId: demoUser.id,
      title: "Literature Notes — Bilingual Draft",
      fileName: "literature-notes.pdf",
      storagePath: "uploads/demo/literature-notes.pdf",
      status: "PROCESSING",
    },
  });

  const doc3 = await prisma.document.create({
    data: {
      userId: demoUser.id,
      title: "Thesis Outline (uploaded)",
      fileName: "thesis-outline.pdf",
      storagePath: "uploads/demo/thesis-outline.pdf",
      status: "COMPLETED",
    },
  });

  const regionOnDoc1 = await prisma.regionSelection.create({
    data: {
      documentId: doc1.id,
      pageNumber: 1,
      xNorm: 0.1,
      yNorm: 0.2,
      widthNorm: 0.8,
      heightNorm: 0.15,
      label: "Abstract paragraph",
    },
  });

  const ocrForRegion = await prisma.ocrResult.create({
    data: {
      regionSelectionId: regionOnDoc1.id,
      extractedText:
        "This paragraph is sample OCR output from a user-drawn region inside the PDF. " +
        "It is not arbitrary image OCR.",
      confidence: 0.92,
      engineVersion: "placeholder-v0",
    },
  });

  await prisma.aiResult.createMany({
    data: [
      {
        documentId: doc1.id,
        ocrResultId: ocrForRegion.id,
        operationType: "TRANSLATE",
        styleType: "NEUTRAL",
        status: "COMPLETED",
        inputSnapshot: ocrForRegion.extractedText.slice(0, 200),
        outputText:
          "Bu paragraf, PDF içinde kullanıcı tarafından seçilen bir bölgeden örnek OCR çıktısıdır.",
        outputMeta: JSON.stringify({ targetLanguage: "tr" }),
      },
      {
        documentId: doc1.id,
        ocrResultId: ocrForRegion.id,
        operationType: "SUMMARIZE",
        styleType: "ACADEMIC",
        status: "COMPLETED",
        inputSnapshot: ocrForRegion.extractedText.slice(0, 200),
        outputText: "Özet: Metin, PDF bölgesi OCR kapsamında üretilmiş demo içeriktir.",
        outputMeta: null,
      },
      {
        documentId: doc1.id,
        ocrResultId: null,
        operationType: "VISUALIZE",
        styleType: "CREATIVE",
        status: "PENDING",
        inputSnapshot: "placeholder: visualization input",
        outputText: null,
        outputMeta: JSON.stringify({
          diagramType: "concept-map",
          note: "not implemented in foundation step",
        }),
      },
    ],
  });

  await prisma.notebookEntry.create({
    data: {
      userId: demoUser.id,
      documentId: doc1.id,
      title: "Defense talking points",
      body: "Explain four layers: presentation, application, persistence, external integration.",
    },
  });

  await prisma.auditLog.create({
    data: {
      userId: demoUser.id,
      action: "SEED_DEMO_DATA",
      entityType: "SYSTEM",
      metadata: JSON.stringify({ documents: [doc1.id, doc2.id, doc3.id] }),
    },
  });

  console.log("Seed complete:", { user: demoUser.email, documents: [doc1.id, doc2.id, doc3.id] });
}

main()
  .then(() => prisma.$disconnect())
  .catch((e) => {
    console.error(e);
    prisma.$disconnect();
    process.exit(1);
  });

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * Geliştirme ve savunma demosu için örnek kullanıcı, belge, OCR ve AI kayıtları yükler.
 *
 * [TR] Neden gerekli
 * Boş veritabanıyla anlatım zorlaşır; tutarlı demo verisi sunar.
 *
 * [TR] Sistem içinde nerede kullanılır
 * `npm run db:seed` — SQL Server şeması uygulandıktan sonra.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: çıktıMeta/audit metadata JSON şekli; enum değerleri (string) ile uyum.
 * - Nasıl çözülür: `JSON.stringify` ile NVarchar alanları doldur; silme sırası FK’ye göre.
 * - Etkilenen dosyalar: Bu dosya, prisma/schema.prisma.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
