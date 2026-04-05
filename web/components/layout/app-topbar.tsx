"use client";

import { Menu, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { UserMenu } from "./user-menu";
import type { SessionUser } from "@/lib/auth/types";

export function AppTopbar({
  user,
  onMenuClick,
}: {
  user: SessionUser;
  onMenuClick: () => void;
}) {
  return (
    <header className="sticky top-0 z-30 flex h-14 shrink-0 items-center gap-3 border-b border-slate-200 bg-white px-4 shadow-sm">
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="lg:hidden"
        onClick={onMenuClick}
        aria-label="Menüyü aç"
      >
        <Menu className="h-5 w-5" />
      </Button>
      <div className="relative max-w-xl flex-1">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <Input
          defaultValue=""
          placeholder="Belgelerde ara… (henüz bağlı değil)"
          className="h-9 border-slate-200 bg-slate-50 pl-9 text-sm"
          readOnly
          title="İleride arama API’sine bağlanacak"
        />
      </div>
      <div className="ml-auto flex items-center gap-2">
        <UserMenu user={user} />
      </div>
    </header>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Üst çubuk — mobil menü, arama alanı (placeholder), kullanıcı menüsü.
 * [TR] Neden gerekli: Uygulama kabuğunun tutarlı üst çerçevesi.
 * [TR] Sistem içinde: AppShell.
 *
 * MODIFICATION NOTES (TR)
 * - Arama: TanStack Query + API ile bağlanır; disabled kaldırılır.
 * - Bildirim zili, tema: sağ tarafa ikonlar eklenebilir.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
