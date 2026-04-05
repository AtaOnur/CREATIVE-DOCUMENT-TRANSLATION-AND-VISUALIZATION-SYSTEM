import Link from "next/link";
import { Mail } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

type Props = { searchParams: { email?: string; name?: string } };

export default function VerifyEmailPage({ searchParams }: Props) {
  const email = searchParams.email ? decodeURIComponent(searchParams.email) : "—";
  const name = searchParams.name ? decodeURIComponent(searchParams.name) : undefined;

  return (
    <Card className="border-slate-200/80 shadow-md">
      <CardHeader className="text-center sm:text-left">
        <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-slate-600 sm:mx-0">
          <Mail className="h-6 w-6" />
        </div>
        <CardTitle className="text-2xl">E-postanızı doğrulayın</CardTitle>
        <CardDescription className="text-base">
          {name ? (
            <>
              Merhaba <strong>{name}</strong>, doğrulama bağlantısı gönderildi (demo: gönderim yok).
            </>
          ) : (
            <>Doğrulama bağlantısı gönderildi (demo: gönderim yok).</>
          )}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700">
          <span className="text-slate-500">Adres: </span>
          <span className="font-mono text-slate-900">{email}</span>
        </p>
        <p className="text-sm text-slate-600">
          Gerçek uygulamada gelen kutunuzu kontrol edin; bağlantıya tıkladığınızda hesap aktifleşir. Bu sürümde doğrudan
          giriş yaparak devam edebilirsiniz.
        </p>
        <Button className="w-full" asChild>
          <Link href="/login">Giriş sayfasına git</Link>
        </Button>
        <p className="text-center text-sm text-slate-500">
          Yanlış e-posta mı?{" "}
          <Link href="/register" className="font-medium text-slate-900 underline-offset-4 hover:underline">
            Kayıt ol
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /verify-email — doğrulama bekleniyor UI’si; query ile e-posta gösterimi.
 * [TR] Neden gerekli: Kayıt sonrası kullanıcı beklentisi ve sunum.
 * [TR] Sistem içinde: registerAction yönlendirmesi.
 *
 * MODIFICATION NOTES (TR)
 * - “E-postayı yeniden gönder”: rate limit ile API çağrısı.
 * - Deep link: token ile hesap aktivasyonu sayfası.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
