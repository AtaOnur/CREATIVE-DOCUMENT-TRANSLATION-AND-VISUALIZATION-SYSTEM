import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ForgotPasswordForm } from "@/components/auth/forgot-password-form";

export default function ForgotPasswordPage() {
  return (
    <Card className="border-slate-200/80 shadow-md">
      <CardHeader>
        <CardTitle className="text-2xl">Şifremi unuttum</CardTitle>
        <CardDescription>
          E-posta adresinizi girin. Demo ortamında gerçek bir e-posta gönderilmez; sonraki adımlar için yönlendirme
          gösterilir.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ForgotPasswordForm />
      </CardContent>
    </Card>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /forgot-password — sıfırlama isteği formu (mock).
 * [TR] Neden gerekli: Auth paketinin tamamlanması.
 * [TR] Sistem içinde: forgotPasswordAction.
 *
 * MODIFICATION NOTES (TR)
 * - E-posta şablonu ve link üretimi: uygulama katmanı + kuyruk.
 * - Hesap yoksa aynı mesaj: güvenlik için “varsa gönderildi” metni.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
