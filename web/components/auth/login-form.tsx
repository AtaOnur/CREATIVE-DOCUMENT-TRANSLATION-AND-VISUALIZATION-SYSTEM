"use client";

import { useFormState } from "react-dom";
import Link from "next/link";
import { loginAction } from "@/lib/auth/actions";
import type { AuthMessageState } from "@/lib/auth/actions";
import { DEMO_PASSWORD } from "@/lib/auth/config";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const initial: AuthMessageState = {};

export function LoginForm() {
  const [state, formAction] = useFormState(loginAction, initial);

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
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="password">Şifre</Label>
          <Link href="/forgot-password" className="text-xs font-medium text-slate-600 hover:text-slate-900">
            Şifremi unuttum
          </Link>
        </div>
        <Input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          placeholder={`Demo: ${DEMO_PASSWORD}`}
        />
      </div>
      <Button type="submit" className="w-full">
        Giriş yap
      </Button>
      <p className="text-center text-sm text-slate-500">
        Hesabın yok mu?{" "}
        <Link href="/register" className="font-medium text-slate-900 underline-offset-4 hover:underline">
          Kayıt ol
        </Link>
      </p>
    </form>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: E-posta/şifre ile giriş formu; sunucu eylemi ve hata gösterimi.
 * [TR] Neden gerekli: Kimlik doğrulama akışının sunum katmanı.
 * [TR] Sistem içinde: /login sayfası.
 *
 * MODIFICATION NOTES (TR)
 * - Google ile giriş: üste ayrı buton + OAuth callback route.
 * - Parola göster/gizle, “beni hatırla”: küçük UX iyileştirmeleri.
 * - Rate limit / CAPTCHA: server action veya API gateway’de.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
