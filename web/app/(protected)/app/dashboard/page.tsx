import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth/session";
import {
  dashboardStats,
  continueWorking,
  getRecentActivity,
  listDocumentsForUser,
} from "@/lib/documents/mock-store";
import { DashboardSummaryCards } from "@/components/dashboard/dashboard-summary-cards";
import { DashboardContinue } from "@/components/dashboard/dashboard-continue";
import { DashboardActivity } from "@/components/dashboard/dashboard-activity";
import { DashboardEmpty } from "@/components/dashboard/dashboard-empty";

export default function DashboardPage() {
  const session = getSession();
  if (!session) redirect("/login");

  const docs = listDocumentsForUser(session.email);
  const stats = dashboardStats(session.email);
  const continuing = continueWorking(session.email, 4);
  const activity = getRecentActivity(6);

  return (
    <div className="mx-auto max-w-6xl space-y-8 pb-10">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-slate-900">Pano</h1>
        <p className="mt-1 text-sm text-slate-600">
          Merhaba, <span className="font-medium text-slate-800">{session.name}</span>. Belgelerinin özeti aşağıda.
        </p>
      </div>

      {docs.length === 0 ? (
        <DashboardEmpty />
      ) : (
        <>
          <DashboardSummaryCards stats={stats} />
          <div className="grid gap-6 lg:grid-cols-2">
            <DashboardContinue documents={continuing} />
            <DashboardActivity entries={activity} />
          </div>
        </>
      )}
    </div>
  );
}

/*
 * -----------------------------------------------------------------------------
 * [TR] Bu dosya ne işe yarar: /app/dashboard — özet kartlar, devam et, aktivite ve boş durum.
 * [TR] Neden gerekli: Uygulama girişinden sonra ana üretkenlik ekranı.
 * [TR] Sistem içinde: korumalı kabuk layout altında.
 *
 * MODIFICATION NOTES (TR)
 * - Grafikler (Chart.js vb.): aylık yükleme istatistikleri.
 * - Prisma ile canlı veri: mock-store çağrıları repository ile değiştirilir.
 * - Zorluk: Kolay–orta.
 * -----------------------------------------------------------------------------
 */
