"use client";

import { logoutAction } from "@/lib/auth/actions";
import type { SessionUser } from "@/lib/auth/types";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { ChevronDown, LogOut, User } from "lucide-react";

export function UserMenu({ user }: { user: SessionUser }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" className="gap-1 border-slate-200 pl-2 pr-2">
          <span className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-100 text-slate-600">
            <User className="h-4 w-4" />
          </span>
          <span className="hidden max-w-[120px] truncate text-left text-xs font-medium sm:inline">
            {user.name}
          </span>
          <ChevronDown className="h-4 w-4 text-slate-500" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <div className="px-2 py-1.5">
          <p className="text-sm font-medium text-slate-900">{user.name}</p>
          <p className="truncate text-xs text-slate-500">{user.email}</p>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <form action={logoutAction} className="w-full">
            <button type="submit" className="flex w-full cursor-pointer items-center gap-2 text-left text-sm">
              <LogOut className="h-4 w-4" />
              Çıkış yap
            </button>
          </form>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Oturum kullanıcısını gösterir ve çıkış formunu sunar (server action).
 * [TR] Neden gerekli: Üst çubukta kompakt kimlik ve güvenli çerez silme.
 * [TR] Sistem içinde: AppTopbar.
 *
 * MODIFICATION NOTES (TR)
 * - Profil ve ayarlar sayfaları: yeni DropdownMenuItem + Link.
 * - Google ile giriş: burada sağlayıcı rozeti veya e-posta doğrulama durumu gösterilebilir.
 * - 2FA: “Güvenlik” alt menüsü eklenebilir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
