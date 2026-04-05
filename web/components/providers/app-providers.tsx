"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import type { ReactNode } from "react";

export function AppProviders({ children }: { children: ReactNode }) {
  const [client] = useState(() => new QueryClient());
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * TanStack Query istemci örneğini React ağacına sağlar.
 *
 * [TR] Neden gerekli
 * Sunucu durumu, önbellek ve yeniden deneme politikaları tek yerden yönetilir.
 *
 * [TR] Sistem içinde nerede kullanılır
 * `app/layout.tsx` tüm uygulamayı bu provider ile sarar.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: varsayılan staleTime, React Query Devtools, hata sınırı.
 * - Nasıl çözülür: QueryClient oluştururken `defaultOptions` eklenir.
 * - Etkilenen dosyalar: Bu dosya, veri çeken bileşenler.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
