import * as React from "react";
import { cn } from "@/lib/utils";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {}

const Input = React.forwardRef<HTMLInputElement, InputProps>(({ className, type, ...props }, ref) => {
  return (
    <input
      type={type}
      className={cn(
        "flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900",
        "ring-offset-white file:border-0 file:bg-transparent file:text-sm file:font-medium",
        "placeholder:text-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2",
        "disabled:cursor-not-allowed disabled:opacity-50",
        className
      )}
      ref={ref}
      {...props}
    />
  );
});
Input.displayName = "Input";

export { Input };

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Form ve arama alanları için tutarlı stil verilmiş text input.
 * [TR] Neden gerekli: Auth ve kabuk aramasında tekrarlanan sınıfları tek bileşende toplar.
 * [TR] Sistem içinde: Giriş/kayıt formları, üst çubuk arama kutusu.
 *
 * MODIFICATION NOTES (TR)
 * - Hata durumu: border-red ve aria-invalid eklenebilir.
 * - İkon prefix/suffix: wrapper div ile genişletilebilir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
