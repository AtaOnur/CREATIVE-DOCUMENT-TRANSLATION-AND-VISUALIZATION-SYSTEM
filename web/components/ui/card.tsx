import * as React from "react";
import { cn } from "@/lib/utils";

function Card({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn("rounded-xl border border-slate-200 bg-white text-slate-950 shadow-sm", className)}
      {...props}
    />
  );
}

function CardHeader({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("flex flex-col space-y-1.5 p-6 pb-3", className)} {...props} />;
}

function CardTitle({ className, ...props }: React.HTMLAttributes<HTMLHeadingElement>) {
  return <h3 className={cn("text-lg font-semibold leading-none tracking-tight", className)} {...props} />;
}

function CardDescription({ className, ...props }: React.HTMLAttributes<HTMLParagraphElement>) {
  return <p className={cn("text-sm text-slate-500", className)} {...props} />;
}

function CardContent({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("p-6 pt-0", className)} {...props} />;
}

export { Card, CardHeader, CardTitle, CardDescription, CardContent };

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: Kart kapsayıcı (auth sayfalarında form kutusu görünümü).
 * [TR] Neden gerekli: Sunumu profesyonel tutmak için tekrar kullanılabilir yüzey.
 * [TR] Sistem içinde: login, register, şifre sayfaları.
 *
 * MODIFICATION NOTES (TR)
 * - CardFooter, hover gölgesi: pano kartları eklendiğinde genişletilebilir.
 * - Zorluk: Kolay.
 * -----------------------------------------------------------------------------
 */
