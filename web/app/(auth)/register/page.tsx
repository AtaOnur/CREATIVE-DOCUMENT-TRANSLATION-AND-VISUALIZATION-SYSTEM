import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { RegisterForm } from "@/components/auth/register-form";

export default function RegisterPage() {
  return (
    <Card className="border-slate-200/80 shadow-md">
      <CardHeader>
        <CardTitle className="text-2xl">Kayıt ol</CardTitle>
        <CardDescription>
          Demo akış: kayıt sonrası e-posta doğrulama sayfasına yönlendirilirsiniz. Veritabanı yazımı henüz yok.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <RegisterForm />
      </CardContent>
    </Card>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /register — yeni kullanıcı formu (mock, doğrulama sayfasına yönlendirir).
 * [TR] Neden gerekli: Tam auth akışının parçası.
 * [TR] Sistem içinde: auth layout, registerAction.
 *
 * MODIFICATION NOTES (TR)
 * - Kullanım şartları onay kutusu: zorunlu alan + backend kaydı.
 * - E-posta mükerrerlik kontrolü: Prisma ile unique email sorgusu.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
