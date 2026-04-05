# Creative Document Translation and Visualisation System (Web)

## Project purpose

This web application is the foundation for a **final-year graduation project**: users upload **PDF documents**, select a **region on a page**, run **OCR on that region only**, then run **creative / translation / summarization / visualization** style operations on the extracted text. The goal is a **modular, easy-to-defend** codebase aligned with a future **Software Design Document (SDD)**.

## Graduation project note

The implementation prioritizes **clarity over cleverness**: small files, obvious names, and four explicit layers so you can explain the system orally and in documentation.

## Current scope (this step)

- Next.js 14 App Router project skeleton under `web/` (this repo also contains an unrelated legacy ASP.NET sample at the root; the **active stack for this thesis** is inside `web/`).
- **Prisma + Microsoft SQL Server** schema for users, documents, PDF **region selections**, **PDF-region OCR results**, AI results, notebook entries, preferences, and audit logs.
- **TypeScript enums/types** mirrored for forms (Zod) plus Prisma-generated enums on the server.
- **Seed script** with one demo user, three sample documents, one OCR sample, and multiple AI sample rows.
- **Architecture placeholders** for OCR (PDF region only) and AI calls — **no real OCR/AI wiring yet**.
- Minimal **Tailwind + shadcn-style Button** and TanStack Query provider — **not a full UI**.

## Out of scope (explicit)

- Full product UI (dashboards, document viewer, region painter, etc.).
- End-to-end OCR and AI integrations.
- **General image-to-text / arbitrary image OCR** — treated as **future work** only.  
  **Current OCR scope:** extract text from a **user-selected region inside an uploaded PDF**.


## Future work

- **General image-to-text / arbitrary image OCR** is **future work** and is **not part of the current implementation scope**.
- Rich PDF rendering, region selection UX, queue/worker for long jobs, authentication, and cloud storage are expected next milestones after this foundation.

## Architecture (simple English)

1. **Presentation layer** — `app/` and `components/`. Renders UI, collects input, shows results. Talks to the application layer via Server Actions, route handlers, or hooks that call APIs. No direct database or vendor SDK calls in components when avoidable.

2. **Application layer** — `lib/application/`. Orchestrates use-cases (e.g. “save region → run OCR → store text → request AI op”). Calls persistence repositories and external adapters. This is where “what happens step-by-step” is easy to narrate in defense.

3. **Persistence layer** — `lib/persistence/` and `prisma/`. Prisma schema (SQL Server), migrations/seed, and a shared `db` client. All durable state (users, documents, regions, OCR, AI rows, notebook, audit).

4. **External integration layer** — `lib/external/`. OCR engines, AI providers, and later file storage or e-mail. **PDF-region OCR** and **AI operations** are **stubbed** here so the rest of the system has stable interfaces.

**Data flow (plain language):** PDF upload → document row → user draws **normalized rectangle** on a page → `RegionSelection` → OCR adapter fills `OcrResult` (**PDF region only**) → AI adapter fills `AiResult` for operations such as **TRANSLATE**, **SUMMARIZE**, **CREATIVE_WRITE**, **REWRITE**, **VISUALIZE** → optional `NotebookEntry` / `AuditLog` for notes and traceability.

## Folder structure (high level)

```text
web/
├── app/                      # Presentation: routes, layouts, global styles
├── components/
│   ├── providers/            # React providers (e.g. TanStack Query)
│   └── ui/                   # shadcn-style primitives (Button only for now)
├── lib/
│   ├── application/          # Use-cases / orchestration (placeholders)
│   ├── persistence/          # Prisma client singleton
│   ├── external/             # OCR & AI placeholders (vendor-neutral)
│   ├── presentation/         # Marker / future view helpers
│   ├── types/                # Shared enums & core TS types
│   ├── validation/           # Zod schemas for forms
│   └── utils.ts              # `cn()` helper for Tailwind class merging
├── prisma/
│   ├── schema.prisma
│   └── seed.ts
├── public/
├── package.json
├── tailwind.config.ts
├── tsconfig.json
└── next.config.mjs
```

## Prerequisites

- Node.js 18+ and npm (install dependencies locally with `npm install` inside `web/`).
- **Microsoft SQL Server** (LocalDB, Express veya tam sürüm). Örnek veritabanı adı: `creative_doc_db`.
- `DATABASE_URL` — `.env.example` dosyasındaki biçime göre `.env` oluştur (SQL login veya Windows kimlik doğrulama).

### Visual Studio ile çözümü açıyorsan

Kök `pdf_bitirme.csproj` içinde `web\**` **MSBuild dışı** bırakıldı; böylece Next.js klasörü .NET projesi sanılmaz. Web için düzenleme yapmaya devam etmek en iyisi **Cursor / VS Code** içinde `web` klasörünü açmak veya güncel **TypeScript Araçları** ile çalışmak. Hâlâ `node_modules` içi yüzlerce uyarı görürsen, Çözüm Gezgini’nde yalnızca `pdf_bitirme` .NET projesine odaklandığından emin ol; front-end derlemesi için `web` içinde `npm run build` kullan.

### SQL Server quick setup (Windows)

1. SQL Server çalışsın (varsayılan instance genelde `localhost:1433`).
2. SSMS veya T-SQL ile: `CREATE DATABASE creative_doc_db;`
3. `.env` içinde `DATABASE_URL` — kullanıcı/şifre veya `integratedSecurity=true` (bkz. `.env.example`).

## Database commands

```bash
cd web
npm install
npx prisma generate
npm run db:seed
```

**İlk şema oluşturma:** Bu ortamda `npx prisma migrate dev` bazen gölge veritabanı / ek TCP bağlantısı nedeniyle hata verebildiği için **`npx prisma db push`** ile şema SQL Server’a uygulandı. İstersen tablolar oluştuktan sonra `prisma migrate diff` ile SQL betiği üretip süreci migration dosyalarına taşıyabilirsin.

**Yerel ipucu:** SQL Server yalnızca `127.0.0.1:1434` gibi bir portta dinliyorsa `DATABASE_URL` içinde **`127.0.0.1`** kullan (`localhost` zaman aşımına düşebilir). Windows kimlik doğrulama (integrated) Node/Prisma ile genelde çalışır; sunucu yalnızca Windows modundaysa önce SSMS’ten **SQL Server ve Windows kimlik doğrulama (karma)** açılmalıdır.

## Seed plan (summary)

| Item | Purpose |
|------|---------|
| 1 demo user | `demo.student@university.edu` with `UserPreference` |
| 3 `Document` rows | Mixed statuses: READY, PROCESSING, UPLOADED |
| 1 `RegionSelection` + 1 `OcrResult` | Shows **PDF-region OCR** sample text |
| Several `AiResult` rows | TRANSLATE, SUMMARIZE, VISUALIZE (PENDING) demos |
| 1 `NotebookEntry`, 1 `AuditLog` | Notebook + traceability samples |

---

## Veritabanı notu (SQL Server + Prisma)

Prisma’nın **SQL Server** bağlayıcısı şemada **yerel `enum` ve `Json` tipini desteklemediği** için: durum alanları **`String`** (uygulama ve `lib/types/enums.ts` ile doğrulanır), `outputMeta` / `metadata` alanları ise **JSON metin** (`NVarChar(Max)` + `JSON.stringify` / parse).

## MODIFICATION NOTES (TR)

- **Olası değişiklikler:** Hocanın istediği ek alanlar (dosya hash, sayfa önizleme URL’si), farklı OCR motoru, authentication.
- **Nasıl çözülür:** Önce `prisma/schema.prisma` ve migration; sonra seed ve servisler; dış entegrasyon `lib/external` altında kalır.
- **Etkilenen dosyalar:** `prisma/schema.prisma`, `prisma/seed.ts`, `lib/persistence/db.ts`, ilgili servis/placeholder dosyaları, formlar için `lib/validation/schemas.ts`.
- **Zorluk:** Alan ekleme kolay–orta; üretimde veri göçü ve geriye dönük uyumluluk orta seviye.
