import Link from "next/link";
import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { PdfUploadForm } from "@/components/upload/pdf-upload-form";
import { MAX_UPLOAD_BYTES } from "@/lib/documents/constants";

export default function UploadPage() {
  const session = getSession();
  if (!session) redirect("/login");

  return (
    <div className="mx-auto max-w-lg space-y-6 pb-10">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">PDF yükle</h1>
        <p className="mt-1 text-sm text-slate-600">
          Dosya türü: PDF · üst boyut: {MAX_UPLOAD_BYTES / (1024 * 1024)} MB. İşleme ve OCR sonraki adımlarda eklenecek.
        </p>
      </div>

      <Card className="border-slate-200 shadow-md">
        <CardHeader>
          <CardTitle>Yeni belge</CardTitle>
          <CardDescription>Başlık isteğe bağlıdır; dosya adından otomatik öneri yapılır.</CardDescription>
        </CardHeader>
        <CardContent>
          <PdfUploadForm />
        </CardContent>
      </Card>

      <p className="text-center text-sm text-slate-500">
        <Link href="/app/documents" className="font-medium text-slate-800 underline-offset-4 hover:underline">
          Belgelere dön
        </Link>
      </p>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app/upload — PDF yükleme kartı ve kısa yönlendirme linki.
 * [TR] Neden gerekli: Mock iş akışının giriş noktası.
 * [TR] Sistem içinde: sidebar Yükle bağlantısı.
 *
 * MODIFICATION NOTES (TR)
 * - DOCX yükleme: ayrı sekme veya MIME seçimi (gelecek).
 * - Depo kotası göstergesi: kullanıcı başı limit.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
