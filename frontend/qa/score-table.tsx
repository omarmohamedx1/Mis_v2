// Synthetic data only. Exercises the real Score page without touching a server.
import { createRoot } from 'react-dom/client';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { LocalizationProvider } from '../src/context/LocalizationContext';
import { ToastProvider } from '../src/components/common/Toast';
import { CollectionsScoreDashboardPage } from '../src/pages/collections/CollectionsScoreDashboardPage';
import { apiClient } from '../src/services/apiClient';
import type { CollectionScoreBucket, CollectionScoreCell, CollectionScoreDashboard } from '../src/features/collections/types/score';
import '../src/styles.css';

const options = new URLSearchParams(location.search);
localStorage.setItem('mis.language', options.get('lang') ?? 'en');
const organizationId = '11111111-1111-4111-8111-111111111111';
const portfolioId = '22222222-2222-4222-8222-222222222222';
const sourceId = '33333333-3333-4333-8333-333333333333';
const missing = options.get('scenario') === 'missing';
const cell = (category: string, caseCount: number, actualBalance: number, targetPercent: number, targetBalance: number, weight: number | null, ratio: number | null): CollectionScoreCell => ({
  category, caseCount: missing ? null : caseCount, actualBalance: missing ? null : actualBalance, targetPercent,
  targetBalance: missing ? null : targetBalance, missing: missing ? null : actualBalance - targetBalance,
  percent: missing ? null : Math.abs(actualBalance) / 6908831 * 100, weight,
  actualVsTarget: missing ? null : ratio, score: missing || weight === null || ratio === null ? null : weight * ratio / 100,
});
const categories = [cell('STAB', 52, 2132520, 45, 3108973.95, null, null), cell('NORM', 21, 863317, 10, 690883.1, null, 124.9584), cell('FWD', 74, -3912994, 45, -3108973.95, 10, 79.45256)];
const buckets: CollectionScoreBucket[] = Array.from({ length: 6 }, (_, index) => ({
  code: `BKT${index + 1}`, version: `version${index}`, categories: index === 5 ? [cell('MORE', 7, 67257, 4, 308158, null, 21.82549)]
    : index === 0 ? categories : [cell('STAB', 3, 43181, index === 4 ? -5 : 20, index === 4 ? -117775.65 : 471102.6, null, null), cell('BWD', 9, 127881, 10, 235551.3, 7, 54.29), cell('NORM', 18, 29961, 5, 117775.65, 3, 25.44), cell('FWD', 243, -2154490, 90, -2119961.7, 6, 98.4)],
  caseCount: missing ? null : index === 0 ? 147 : 273, actualBalance: missing ? null : index === 0 ? 6908831 : 2355513,
  targetBalance: missing ? null : 3799857.05, weight: 10, score: missing ? null : index === 0 ? 7.945256 : 10.47,
  issues: missing ? ['MISSING_PERIOD_SOURCE', 'MISSING_TARGETS'] : [], systemCaseCount: index === 0 ? 147 : 273,
}));
const dashboard: CollectionScoreDashboard = {
  organization: { id: organizationId, code: 'QA', nameArabic: 'بنك اختبار محلي', nameEnglish: 'Local test bank', organizationType: 'BANK' },
  portfolioType: 'ACT', portfolio: { id: portfolioId, organizationId, code: 'ACT_LOAN', nameArabic: 'محفظة القروض', nameEnglish: 'Loan portfolio', currencyCode: 'EGP' },
  reportingPeriod: '2026-11', canEdit: true, summary: { totalDueAmount: 7703950.2564, paid: 67257, scoreAll: null, upperTableScore: missing ? null : 67.6, systemCaseCount: 1418, periodCaseCount: missing ? 0 : 1418, unclassifiedCaseCount: 0, unmappedSystemCaseCount: 0 }, buckets,
};
const qa = (window as unknown as { scoreQa: { saves: unknown[] } }).scoreQa = { saves: [] };
apiClient.defaults.adapter = async config => {
  const reply = (data: unknown) => ({ config, status: 200, statusText: 'OK', headers: {}, data });
  if (config.method === 'put') { qa.saves.push(JSON.parse(config.data)); return reply(buckets[0]); }
  if (config.url?.endsWith('/periods')) return reply({ currentPeriod: '2026-11', periods: ['2026-11', '2026-10'] });
  if (config.url?.endsWith('/source')) return reply({ version: 'source-version', openingImportId: sourceId, paymentSource: 'SYSTEM', imports: [{ id: sourceId, fileName: 'Synthetic opening.csv', caseCount: 1418 }], fields: { openingBucket: 'Current BKT', openingBalance: 'Current Balance', startingDues: 'StartingTotal Dues', payment: 'Payment', dues0: 'BKT0', dues1: 'BKT1', dues2: 'BKT2', dues3: 'BKT3', dues4: 'BKT4', dues5: 'BKT5' } });
  return reply(dashboard);
};
createRoot(document.getElementById('root')!).render(<LocalizationProvider><ToastProvider><MemoryRouter initialEntries={[`/collections/score/${organizationId}/ACT/${portfolioId}`]}><Routes><Route path="/collections/score/:organizationId/:portfolioType/:portfolioId" element={<main className="p-3 sm:p-6"><CollectionsScoreDashboardPage /></main>} /></Routes></MemoryRouter></ToastProvider></LocalizationProvider>);
