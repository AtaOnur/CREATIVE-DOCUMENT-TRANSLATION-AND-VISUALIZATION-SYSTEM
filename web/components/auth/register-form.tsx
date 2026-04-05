"use client";

import { useFormState } from "react-dom";
import Link from "next/link";
import { registerAction } from "@/lib/auth/actions";
import type { AuthMessageState } from "@/lib/auth/actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const initial: AuthMessageState = {};

export function RegisterForm() {
  const [state, formAction] = useFormState(registerAction, initial);

  return (
    <form action={formAction} className="space-y-4">
      {state.error ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
          {state.error}
        </p>
      ) : null}
      <div className="space-y-2">
        <Label htmlFor="name">Ad soyad</Label>
        <Input id="name" name="name" type="text" autoComplete="name" required placeholder="Adınız" />
      </div>
      <div className="space-y-2">
        <Label htmlFor="email">E-posta</Label>
        <Input id="email" name="email" type="email" autoComplete="email" required placeholder="ornek@edu.tr" />
      </div>
      <div className="space-y-2">
        <Label htmlFor="password">Şifre</Label>
        <Input
          id="password"
          name="password"
          type="password"
          autoComplete="new-password"
          required
          minLength={6}
          placeholder="En az 6 karakter"
        />
      </div>
      <Button type="submit" className="w-full">
        Kayıt ol
      </Button>
      <p className="text-center text-sm text-slate-500">
        Zaten hesabın var mı?{" "}
        <Link href="/login" className="font-medium text-slate-900 underline-offset-4 hover:underline">
          Giriş yap
        </Link>
      </p>
    </form>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Kayıt formu; doğrulama sonrası verify-email sayfasına yönlendirme (mock).
 * [TR] Neden gerekli: Akışın parçası; gerçek uygulamada kullanıcı oluşturma buraya bağlanır.
 * [TR] Sistem içinde: /register.
 *
 * MODIFICATION NOTES (TR)
 * - E-posta doğrulama zorunluluğu: kayıt sonrası token tablosu ve e-posta kuyruğu.
 * - Kurumsal kayıt / davet kodu: ek alan ve sunucu doğrulaması.
 * - Şifre gücü göstergesi: istemci tarafı yardımcı.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
