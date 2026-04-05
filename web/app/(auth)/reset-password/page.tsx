import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ResetPasswordForm } from "@/components/auth/reset-password-form";

type Props = { searchParams: { token?: string } };

export default function ResetPasswordPage({ searchParams }: Props) {
  const token = searchParams.token;

  return (
    <Card className="border-slate-200/80 shadow-md">
      <CardHeader>
        <CardTitle className="text-2xl">Yeni şifre belirle</CardTitle>
        <CardDescription>
          Güçlü bir şifre seçin. Demo: token doğrulaması yapılmaz; üretimde tek kullanımlık bağlantı zorunludur.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ResetPasswordForm token={token} />
      </CardContent>
    </Card>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /reset-password — yeni şifre formu; isteğe bağlı ?token= query.
 * [TR] Neden gerekli: Şifre sıfırlama akışının UI tamamlayıcısı.
 * [TR] Sistem içinde: resetPasswordAction.
 *
 * MODIFICATION NOTES (TR)
 * - Süresi dolmuş token: hata sayfası ve yeniden istek.
 * - Audit log: başarılı sıfırlama kaydı.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
