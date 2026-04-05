"use client";

import { useCallback, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { FileUp, Loader2 } from "lucide-react";
import { uploadPdfAction } from "@/lib/documents/actions";
import { MAX_UPLOAD_BYTES, ALLOWED_UPLOAD_MIME } from "@/lib/documents/constants";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export function PdfUploadForm() {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [progress, setProgress] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [title, setTitle] = useState("");

  const validateFile = useCallback((f: File): string | null => {
    if (f.size > MAX_UPLOAD_BYTES) {
      return `Dosya çok büyük (max ${MAX_UPLOAD_BYTES / (1024 * 1024)} MB).`;
    }
    if (f.type && f.type !== ALLOWED_UPLOAD_MIME) {
      return "Yalnızca PDF dosyası yükleyebilirsiniz.";
    }
    if (!f.name.toLowerCase().endsWith(".pdf")) {
      return "Dosya uzantısı .pdf olmalıdır.";
    }
    return null;
  }, []);

  const onFile = useCallback(
    (f: File | undefined) => {
      if (!f) return;
      setError(null);
      const err = validateFile(f);
      if (err) {
        setError(err);
        setFile(null);
        return;
      }
      setFile(f);
      if (!title.trim()) {
        setTitle(f.name.replace(/\.pdf$/i, ""));
      }
    },
    [title, validateFile]
  );

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    if (!file) {
      setError("Lütfen bir PDF dosyası seçin veya sürükleyin.");
      return;
    }
    const err = validateFile(file);
    if (err) {
      setError(err);
      return;
    }

    setBusy(true);
    setProgress(8);

    const fd = new FormData();
    fd.set("file", file);
    fd.set("title", title.trim());

    const tick = setInterval(() => {
      setProgress((p) => (p < 88 ? p + 7 : p));
    }, 220);

    try {
      const res = await uploadPdfAction(fd);
      clearInterval(tick);
      if (!res.ok) {
        setError(res.error);
        setProgress(0);
        setBusy(false);
        return;
      }
      setProgress(100);
      await new Promise((r) => setTimeout(r, 400));
      router.push("/app/documents?uploaded=1");
      router.refresh();
    } catch {
      clearInterval(tick);
      setError("Yükleme sırasında beklenmeyen hata.");
      setProgress(0);
      setBusy(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {error ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
          {error}
        </p>
      ) : null}

      <div className="space-y-2">
        <Label htmlFor="title">Belge başlığı (isteğe bağlı)</Label>
        <Input
          id="title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Örn. Makale taslağı"
          disabled={busy}
        />
      </div>

      <div>
        <Label className="mb-2 block">PDF dosyası</Label>
        <input
          ref={inputRef}
          type="file"
          accept=".pdf,application/pdf"
          className="hidden"
          disabled={busy}
          onChange={(e) => onFile(e.target.files?.[0])}
        />
        <button
          type="button"
          disabled={busy}
          onClick={() => inputRef.current?.click()}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            const f = e.dataTransfer.files?.[0];
            if (f) onFile(f);
          }}
          className={cn(
            "flex w-full flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed px-6 py-14 transition-colors",
            dragOver ? "border-slate-900 bg-slate-50" : "border-slate-200 bg-white hover:border-slate-300",
            busy && "pointer-events-none opacity-70"
          )}
        >
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-slate-700">
            <FileUp className="h-6 w-6" />
          </div>
          <div className="text-center text-sm text-slate-600">
            <span className="font-medium text-slate-900">Sürükleyip bırakın</span> veya dosya seçin
            <p className="mt-1 text-xs text-slate-500">Yalnızca PDF · en fazla 20 MB</p>
          </div>
          {file ? (
            <p className="text-xs font-medium text-slate-800">
              Seçilen: {file.name} ({(file.size / 1024).toFixed(1)} KB)
            </p>
          ) : null}
        </button>
      </div>

      {busy ? (
        <div className="space-y-2">
          <div className="flex items-center gap-2 text-sm text-slate-600">
            <Loader2 className="h-4 w-4 shrink-0 animate-spin" />
            Yükleniyor… (demo ilerleme)
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-slate-100">
            <div
              className="h-full rounded-full bg-slate-900 transition-all duration-300"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      ) : null}

      <Button type="submit" className="w-full" disabled={busy || !file}>
        Yükle
      </Button>

      <p className="text-center text-xs text-slate-500">
        Genel görüntü dosyası veya görüntüden metin (OCR) yükleme{" "}
        <strong className="font-normal text-slate-600">bu sürümde yoktur</strong>; yalnızca PDF iş akışı desteklenir.
        Gelecek çalışmalar için not düşülmüştür.
      </p>
    </form>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: PDF için sürükle-bırak seçim, istemci doğrulama, sahte ilerleme çubuğu ve server action çağrısı.
 * [TR] Neden gerekli: Savunmada uçtan uca yükleme deneyimi; sunucuda tekrar doğrulama actions.ts içinde.
 * [TR] Sistem içinde: /app/upload
 *
 * MODIFICATION NOTES (TR)
 * - Gerçek ilerleme: XMLHttpRequest / fetch ile yüzde veya parçalı yükleme.
 * - DOCX: accept listesi ve sunucu tarafı dönüştürme tetikleme.
 * - Çoklu dosya: input multiple + kuyruk.
 * - Bulut: önce imzalı URL ile tarayıcıdan doğrudan depoya yükleme.
 * - Zorluk: Orta–yüksek.
 * -----------------------------------------------------------------------------
 */
