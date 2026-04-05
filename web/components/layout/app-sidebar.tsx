"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { BookOpen, FileText, LayoutDashboard, Layers, Upload } from "lucide-react";
import { cn } from "@/lib/utils";

const mainNav = [
  { href: "/app/dashboard", label: "Pano", icon: LayoutDashboard, exact: true },
  { href: "/app/documents", label: "Belgeler", icon: FileText, exact: false },
  { href: "/app/upload", label: "Yükle", icon: Upload, exact: true },
  { href: "/app/notebook", label: "Not defteri", icon: BookOpen, disabled: true, exact: true },
];

function isActive(pathname: string, href: string, exact: boolean) {
  if (exact) return pathname === href;
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function AppSidebar({
  className,
  onNavigate,
}: {
  className?: string;
  onNavigate?: () => void;
}) {
  const pathname = usePathname();

  return (
    <div className={cn("flex h-full flex-col", className)}>
      <div className="flex h-14 items-center gap-2 border-b border-slate-200 px-4">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-slate-900 text-xs font-bold text-white">
          CD
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-slate-900">Creative Doc</p>
          <p className="truncate text-xs text-slate-500">PDF çeviri & görselleştirme</p>
        </div>
      </div>
      <nav className="flex-1 space-y-0.5 p-3">
        {mainNav.map((item) => {
          const Icon = item.icon;
          const exact = item.exact ?? false;
          if (item.disabled) {
            return (
              <span
                key={item.label}
                className="flex cursor-not-allowed items-center gap-2 rounded-md px-3 py-2 text-sm text-slate-400"
                title="Yakında"
              >
                <Icon className="h-4 w-4 shrink-0" />
                {item.label}
                <span className="ml-auto text-[10px] font-medium uppercase tracking-wide">yakında</span>
              </span>
            );
          }
          const active = isActive(pathname, item.href, exact);
          return (
            <Link
              key={item.href}
              href={item.href}
              onClick={onNavigate}
              className={cn(
                "flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                active ? "bg-slate-100 text-slate-900" : "text-slate-600 hover:bg-slate-50 hover:text-slate-900"
              )}
            >
              <Icon className="h-4 w-4 shrink-0" />
              {item.label}
            </Link>
          );
        })}
      </nav>
      <div className="border-t border-slate-200 p-3">
        <div className="flex items-center gap-2 rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-500">
          <Layers className="h-4 w-4 shrink-0 text-slate-400" />
          Yalnızca PDF iş akışı; OCR/AI modülleri sırayla eklenecek.
        </div>
      </div>
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Sol menü — Pano, Belgeler, Yükle ve yakında not defteri.
 * [TR] Neden gerekli: Uygulama bölümleri arasında gezinme.
 * [TR] Sistem içinde: AppShell.
 *
 * MODIFICATION NOTES (TR)
 * - Belgeler alt menü (klasörler): nested yapı veya collapsible.
 * - Admin bağlantısı: session rolü ile koşullu.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
