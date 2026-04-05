"use client";

import { useFormState } from "react-dom";
import Link from "next/link";
import { resetPasswordAction } from "@/lib/auth/actions";
import type { AuthMessageState } from "@/lib/auth/actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const initial: AuthMessageState = {};

export function ResetPasswordForm({ token }: { token: string | undefined }) {
  const [state, formAction] = useFormState(resetPasswordAction, initial);

  return (
    <form action={formAction} className="space-y-4">
      {state.error ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800" role="alert">
          {state.error}
        </p>
      ) : null}
      {token ? <input type="hidden" name="token" value={token} /> : null}
      {token ? null : (
        <p className="text-sm text-amber-800">
          Demo: URL’de token yok. Gerçek uygulamada e-postadaki bağlantı{" "}
          <code className="rounded bg-slate-100 px-1">?token=…</code> içerir.
        </p>
      )}
      <div className="space-y-2">
        <Label htmlFor="password">Yeni şifre</Label>
        <Input id="password" name="password" type="password" autoComplete="new-password" required minLength={6} />
      </div>
      <div className="space-y-2">
        <Label htmlFor="confirm">Yeni şifre (tekrar)</Label>
        <Input id="confirm" name="confirm" type="password" autoComplete="new-password" required minLength={6} />
      </div>
      <Button type="submit" className="w-full">
        Şifreyi güncelle
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
 * [TR] Bu dosya ne işe yarar: Yeni şifre formu; isteğe bağlı token gizli alanı (demo).
 * [TR] Neden gerekli: Şifre sıfırlama UI’si; başarıda login’e yönlendirme.
 * [TR] Sistem içinde: /reset-password.
 *
 * MODIFICATION NOTES (TR)
 * - Token doğrulama: sunucu eyleminde hash, süre, tek kullanım kontrolü.
 * - Zod şemaları: şifre politikası tek yerden.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
