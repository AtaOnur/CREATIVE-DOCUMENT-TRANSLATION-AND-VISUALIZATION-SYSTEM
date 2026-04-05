import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { cva } from "class-variance-authority";
import type { VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const buttonVariants = cva(
  "inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 disabled:pointer-events-none disabled:opacity-50",
  {
    variants: {
      variant: {
        default: "bg-slate-900 text-white hover:bg-slate-800",
        outline: "border border-slate-200 bg-white hover:bg-slate-50",
        ghost: "hover:bg-slate-100 text-slate-700",
        link: "text-slate-900 underline-offset-4 hover:underline",
      },
      size: {
        default: "h-10 px-4 py-2",
        sm: "h-9 rounded-md px-3",
        icon: "h-9 w-9",
      },
    },
    defaultVariants: { variant: "default", size: "default" },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button";
    return (
      <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />
    );
  }
);
Button.displayName = "Button";

export { Button, buttonVariants };

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar
 * shadcn/ui düzeninde, Radix Slot ve CVA ile küçük bir Button bileşeni sunar.
 *
 * [TR] Neden gerekli
 * İlerideki formlar ve sayfalarda tutarlı etkileşim elemanı hazır olur (foundation adımı).
 *
 * [TR] Sistem içinde nerede kullanılır
 * Sunum katmanı — `components/ui` ve sayfa bileşenleri.
 *
 * MODIFICATION NOTES (TR)
 * - Olası değişiklik: ikonlu varyantlar, yükleme durumu, tam genişlik.
 * - Nasıl çözülür: `buttonVariants` içine yeni variant; gerekirse alt bileşen.
 * - Etkilenen dosyalar: Bu dosya, tasarım token’ları (globals.css / tailwind).
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
