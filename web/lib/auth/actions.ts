"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE, DEMO_PASSWORD, SESSION_MAX_AGE_SEC } from "./config";
import type { SessionUser } from "./types";

export type AuthMessageState = {
  error?: string;
  success?: string;
};

function sessionCookieOptions() {
  return {
    httpOnly: true,
    sameSite: "lax" as const,
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: SESSION_MAX_AGE_SEC,
  };
}

function setSession(user: SessionUser) {
  cookies().set(AUTH_COOKIE, JSON.stringify(user), sessionCookieOptions());
}

export async function loginAction(
  _prev: AuthMessageState | null,
  formData: FormData
): Promise<AuthMessageState> {
  const email = String(formData.get("email") ?? "").trim().toLowerCase();
  const password = String(formData.get("password") ?? "");

  if (!email || !password) {
    return { error: "E-posta ve şifre gerekli." };
  }
  if (password !== DEMO_PASSWORD) {
    return {
      error: `Demo ortamı: şifre olarak "${DEMO_PASSWORD}" kullanın veya ileride gerçek doğrulama ekleyin.`,
    };
  }

  const user: SessionUser = {
    email,
    name: email.split("@")[0] || "Kullanıcı",
  };
  setSession(user);
  redirect("/app");
}

export async function registerAction(
  _prev: AuthMessageState | null,
  formData: FormData
): Promise<AuthMessageState> {
  const email = String(formData.get("email") ?? "").trim().toLowerCase();
  const password = String(formData.get("password") ?? "");
  const name = String(formData.get("name") ?? "").trim();

  if (!email || !password || !name) {
    return { error: "Ad, e-posta ve şifre gerekli." };
  }
  if (password.length < 6) {
    return { error: "Şifre en az 6 karakter olmalı." };
  }

  // Mock: gerçek akışta kullanıcı DB’ye yazılır ve doğrulama e-postası kuyruğa alınır.
  redirect(`/verify-email?email=${encodeURIComponent(email)}&name=${encodeURIComponent(name)}`);
}

export async function logoutAction() {
  cookies().delete(AUTH_COOKIE);
  redirect("/login");
}

export async function forgotPasswordAction(
  _prev: AuthMessageState | null,
  formData: FormData
): Promise<AuthMessageState> {
  const email = String(formData.get("email") ?? "").trim().toLowerCase();
  if (!email) {
    return { error: "E-posta gerekli." };
  }
  // Mock: e-posta gönderimi yok; UI başarı mesajı gösterir.
  return {
    success:
      "Bu bir demodur. Gerçek uygulamada sıfırlama bağlantısı e-posta ile gönderilir. Şimdilik “Şifre sıfırlama” sayfasına gidebilirsiniz.",
  };
}

export async function resetPasswordAction(
  _prev: AuthMessageState | null,
  formData: FormData
): Promise<AuthMessageState> {
  const password = String(formData.get("password") ?? "");
  const confirm = String(formData.get("confirm") ?? "");
  if (password.length < 6) {
    return { error: "Şifre en az 6 karakter olmalı." };
  }
  if (password !== confirm) {
    return { error: "Şifreler eşleşmiyor." };
  }
  // Mock: token doğrulaması yok; gerçekte tek kullanımlık token DB’de kontrol edilir.
  redirect("/login?reset=1");
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Giriş, kayıt, çıkış ve şifre formlarının sunucu eylemleri (mock).
 * [TR] Neden gerekli: httpOnly çerez ve yönlendirmeler sunucuda kalır; yapı ileride Prisma/API’ye bağlanır.
 * [TR] Sistem içinde: auth sayfalarındaki form action / useFormState.
 *
 * MODIFICATION NOTES (TR)
 * - Google / OAuth: ayrı route (ör. /api/auth/callback) ve çerez set etmek için actions genişler.
 * - 2FA: loginAction iki aşamalı hale gelir (önce şifre, sonra TOTP doğrulama sayfası).
 * - Admin: kayıt sonrası rol ataması veya davetiye ile sınırlı kayıt.
 * - Etkilenen dosyalar: Bu dosya, prisma/user servisi, e-posta servisi, middleware.
 * - Zorluk: Orta–yüksek (gerçek güvenlik gereksinimlerine göre).
 * -----------------------------------------------------------------------------
 */
