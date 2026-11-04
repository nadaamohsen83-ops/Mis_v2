import { ArrowUpRight, FileClock, FolderOpen, UsersRound, UserRoundPlus } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../../components/common/EmptyState';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { PageHeader } from '../../components/common/PageHeader';
import { LegalKpi, useLegalText } from '../../features/legal/legalUi';
import { legalService } from '../../features/legal/services/legalService';
import type { LegalDashboard } from '../../features/legal/types/legal';

export function LegalDashboardPage() {
  const l = useLegalText();
  const [data, setData] = useState<LegalDashboard>();
  const [error, setError] = useState(false);

  function load() {
    setError(false);
    legalService.dashboard().then(setData).catch(() => setError(true));
  }

  useEffect(() => { load(); }, []);

  if (error) {
    return <ErrorState title={l.text('تعذر تحميل لوحة العملاء', 'Could not load the client dashboard')} onRetry={load} />;
  }
  if (!data) {
    return <div className="grid min-h-[440px] place-items-center"><LoadingSpinner /></div>;
  }

  const metrics = [
    { label: l.text('إجمالي العملاء القانونيين', 'Total Legal Clients'), value: data.totalClients, icon: UsersRound, tone: 'blue' as const, to: '/legal/clients' },
    { label: l.text('العملاء المضافون مؤخرًا', 'Recently Added Clients'), value: data.recentlyAddedClients, icon: UserRoundPlus, tone: 'green' as const, to: '/legal/clients' },
    { label: l.text('محولون من التحصيل', 'Transferred from Collections'), value: data.transferredFromCollections, icon: ArrowUpRight, tone: 'amber' as const, to: '/legal/clients' },
    { label: l.text('ملفات العملاء المحدّثة مؤخرًا', 'Recently Updated Client Files'), value: data.recentlyUpdatedClientFiles, icon: FileClock, tone: 'red' as const, to: '/legal/client-files' },
  ];

  return (
    <div className="space-y-7">
      <PageHeader
        eyebrow={l.text('الشؤون القانونية', 'LEGAL DEPARTMENT')}
        title={l.text('لوحة العملاء', 'Client Dashboard')}
        description={l.text('نظرة عامة على العملاء والملفات القانونية وعمليات التحويل من التحصيل.', 'An overview of legal clients, client files, and transfers from Collections.')}
      />

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {metrics.map(({ label, value, icon: Icon, tone, to }) => (
          <Link className="group relative" key={label} to={to}>
            <LegalKpi label={label} value={l.number(value)} tone={tone} />
            <Icon className="absolute end-5 top-6 h-5 w-5 text-mis-muted transition group-hover:text-mis-primary" />
          </Link>
        ))}
      </section>

      <section className="overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm">
        <div className="flex items-center justify-between gap-3 border-b border-mis-border px-5 py-4">
          <div>
            <h2 className="text-lg font-bold text-mis-navy">{l.text('أحدث العملاء', 'Recently Added Clients')}</h2>
            <p className="mt-1 text-sm text-slate-500">{l.text('العملاء الجدد خلال آخر 30 يومًا.', 'Clients added during the last 30 days.')}</p>
          </div>
          <Link className="inline-flex items-center gap-1 text-sm font-bold text-mis-primary" to="/legal/clients">
            {l.text('كل العملاء', 'All Clients')} <ArrowUpRight className="h-4 w-4" />
          </Link>
        </div>
        {data.recentlyAdded.length ? (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[46rem] text-sm">
              <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3 text-start">{l.text('اسم العميل', 'Client Name')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الرقم القومي', 'National ID')}</th>
                  <th className="px-4 py-3 text-start">{l.text('البنك', 'Bank Name')}</th>
                  <th className="px-4 py-3 text-start">{l.text('الحالة', 'Client Status')}</th>
                  <th className="px-4 py-3 text-end">{l.text('التفاصيل', 'Details')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-mis-border">
                {data.recentlyAdded.map((client) => (
                  <tr className="hover:bg-slate-50" key={client.customerId}>
                    <td className="px-4 py-3 font-semibold text-mis-navy">{client.name}</td>
                    <td className="px-4 py-3" data-bidi="ltr">{client.nationalId || '—'}</td>
                    <td className="px-4 py-3">{client.bankName}</td>
                    <td className="px-4 py-3">{l.clientStatus(client.status)}</td>
                    <td className="px-4 py-3 text-end">
                      <Link className="font-semibold text-mis-primary hover:underline" to={`/legal/client-files/${client.customerId}`}>
                        {l.text('عرض التفاصيل', 'View Details')}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState
            compact
            icon={<FolderOpen className="h-5 w-5" />}
            title={l.text('لا توجد إضافات حديثة', 'No recently added clients')}
            description={l.text('سيظهر هنا العملاء الجدد عند إضافتهم إلى الشؤون القانونية.', 'Newly added Legal clients will appear here.')}
          />
        )}
      </section>
    </div>
  );
}
