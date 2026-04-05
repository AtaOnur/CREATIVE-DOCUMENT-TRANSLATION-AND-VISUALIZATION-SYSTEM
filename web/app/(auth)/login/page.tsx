import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { LoginForm } from "@/components/auth/login-form";
import { DEMO_PASSWORD } from "@/lib/auth/config";

type Props = { searchParams: { reset?: string } };

export default function LoginPage({ searchParams }: Props) {
  const showResetOk = searchParams.reset === "1";

  return (
    <Card className="border-slate-200/80 shadow-md">
      <CardHeader>
        <CardTitle className="text-2xl">Giriş yap</CardTitle>
        <CardDescription>
          Kurumsal veya okul e-postanızla oturum açın. Demo için şifre: <strong>{DEMO_PASSWORD}</strong>
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {showResetOk ? (
          <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900">
            Şifreniz güncellendi (demo). Aşağıdan giriş yapabilirsiniz.
          </p>
        ) : null}
        <LoginForm />
      </CardContent>
    </Card>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /login rotası; kart içinde giriş formu ve sıfırlama sonrası bilgi bandı.
 * [TR] Neden gerekli: Kimlik doğrulama giriş noktası.
 * [TR] Sistem içinde: middleware yönlendirmesi, kök sayfa yönlendirmesi.
 *
 * MODIFICATION NOTES (TR)
 * - OAuth sağlayıcı butonları: CardContent üstüne ayrı bölüm.
 * - CAPTCHA: form altında koşullu widget.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
