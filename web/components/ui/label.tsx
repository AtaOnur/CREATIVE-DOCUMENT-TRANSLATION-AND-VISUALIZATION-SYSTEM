"use client";

import * as React from "react";
import * as LabelPrimitive from "@radix-ui/react-label";
import { cn } from "@/lib/utils";

const Label = React.forwardRef<
  React.ElementRef<typeof LabelPrimitive.Root>,
  React.ComponentPropsWithoutRef<typeof LabelPrimitive.Root>
>(({ className, ...props }, ref) => (
  <LabelPrimitive.Root
    ref={ref}
    className={cn(
      "text-sm font-medium leading-none text-slate-700 peer-disabled:cursor-not-allowed peer-disabled:opacity-70",
      className
    )}
    {...props}
  />
));
Label.displayName = LabelPrimitive.Root.displayName;

export { Label };

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Radix Label ile erişilebilir form etiketleri sağlar.
 * [TR] Neden gerekli: input id/htmlFor ilişkisi ve ekran okuyucu uyumu.
 * [TR] Sistem içinde: Auth formları.
 *
 * MODIFICATION NOTES (TR)
 * - Zorunlu alan yıldızı (*), yardım metni: etiket altına slot eklenebilir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
