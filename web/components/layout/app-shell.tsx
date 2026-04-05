"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import type { SessionUser } from "@/lib/auth/types";
import { AppSidebar } from "./app-sidebar";
import { AppTopbar } from "./app-topbar";

export function AppShell({
  user,
  children,
}: {
  user: SessionUser;
  children: React.ReactNode;
}) {
  const [mobileNav, setMobileNav] = useState(false);

  return (
    <div className="flex min-h-screen bg-slate-100/80">
      {mobileNav ? (
        <button
          type="button"
          className="fixed inset-0 z-40 bg-slate-900/40 backdrop-blur-sm lg:hidden"
          aria-label="Menüyü kapat"
          onClick={() => setMobileNav(false)}
        />
      ) : null}
      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-50 w-64 border-r border-slate-200 bg-white shadow-lg transition-transform duration-200 lg:static lg:z-0 lg:shadow-none",
          mobileNav ? "translate-x-0" : "-translate-x-full lg:translate-x-0"
        )}
      >
        <AppSidebar onNavigate={() => setMobileNav(false)} className="h-full" />
      </aside>
      <div className="flex min-h-screen min-w-0 flex-1 flex-col">
        <AppTopbar user={user} onMenuClick={() => setMobileNav(true)} />
        {/* [TR] min-h-0: flex çocuğunun küçülmesine izin verir; aksi halde PDF/scrollbar yüksekliği 0 kalabilir. */}
        <div className="min-h-0 flex-1 overflow-auto">{children}</div>
      </div>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Kenar çubuğu + üst çubuk + içerik alanını bir arada düzenler; mobilde menü örtüsü açar.
 * [TR] Neden gerekli: Korumalı sayfaların ortak iskeleti tek bileşende toplanır.
 * [TR] Sistem içinde: app/(protected)/app/layout.tsx.
 *
 * MODIFICATION NOTES (TR)
 * - İçerik sarmalayıcıda min-h-0: iç içe flex + PDF görüntüleyici yüksekliği sıfırlanmasın.
 * - Kenar çubuğu daralt/genişlet (persisted): localStorage + context.
 * - Yukarıda bildirim çubuğu: Shell içine slot.
 * - Zorluk: Orta.
 * -----------------------------------------------------------------------------
 */
