"use client";

import { useFormState } from "react-dom";
import Link from "next/link";
import { forgotPasswordAction } from "@/lib/auth/actions";
import type { AuthMessageState } from "@/lib/auth/actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const initial: AuthMessageState = {};

export function ForgotPasswordForm() {
  const [state, formAction] = useFormState(forgotPasswordAction, initial);

  if (state.success) {
    return (
      <div className="space-y-4">
        <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-900">{state.success}</p>
        <Button variant="outline" className="w-full" asChild>
          <Link href="/reset-password">Demo: sıfırlama sayfasına git</Link>
        </Button>
        <p className="text-center text-sm text-slate-500">
          <Link href="/login" className="font-medium text-slate-900 underline-offset-4 hover:underline">
            Girişe dön
          </Link>
        </p>
      </div>
    );
  }

  return (
    <form action={formAction} className="space-y-4">
      {state.error ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
          {state.error}
        </p>
      ) : null}
      <div className="space-y-2">
        <Label htmlFor="email">E-posta</Label>
        <Input id="email" name="email" type="email" autoComplete="email" required placeholder="ornek@edu.tr" />
      </div>
      <Button type="submit" className="w-full">
        Sıfırlama bağlantısı gönder
      </Button>
      <p className="text-center text-sm text-slate-500">
        <Link href="/login" className="font-medium text-slate-900 underline-offset-4 hover:underline">
          Girişe dön
        </Link>
      </p>
    </form>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Şifre unuttum formu; mock başarı mesajı ve demo linki.
 * [TR] Neden gerekli: Tam auth akışı sunumu ve ileride e-posta entegrasyonu için iskelet.
 * [TR] Sistem içinde: /forgot-password.
 *
 * MODIFICATION NOTES (TR)
 * - Gerçek e-posta: queue + şablon + tek kullanımlık token kaydı.
 * - Bot koruması: hCaptcha vb.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
