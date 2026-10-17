// Synthetic local API: exercises the real pages and service without business data or network writes.
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Outlet, Route, Routes } from 'react-router-dom';
import { LocalizationProvider } from '../src/context/LocalizationContext';
import { AuthProvider } from '../src/context/AuthContext';
import { ToastProvider } from '../src/components/common/Toast';
import { BankCaseDistributionPage } from '../src/pages/banks/BankCaseDistributionPage';
import { CollectionAssignmentsPage } from '../src/pages/collections/CollectionAssignmentsPage';
import { apiClient } from '../src/services/apiClient';
import '../src/styles.css';

const options = new URLSearchParams(location.search);
localStorage.setItem('mis.language', options.get('lang') ?? 'en');
const kind = options.get('kind') ?? 'bank';
const id = '2656263e-8c57-44d5-b178-6b57451e6a12';
const qa: any = (window as any).__distributionQa = { requests: [], errors: [], fail: false, empty: false };
window.addEventListener('error', event => qa.errors.push(event.message));
window.addEventListener('unhandledrejection', event => qa.errors.push(String(event.reason)));
if (kind !== 'general') history.replaceState(null, '', `/${kind === 'installment' ? 'installment-companies' : 'banks'}/${id}/act/auto/distribution`);
const collectors = [
  { id: 'c1', name: 'أحمد محمد · Ahmed Mohamed', assignedCases: 12, totalOutstanding: 24000, activeWorkload: 12, teamName: 'فريق التحصيل / Collections', teamId: 'team' },
  { id: 'c2', name: 'سارة علي · Sara Ali', assignedCases: 7, totalOutstanding: 14000, activeWorkload: 7, teamName: 'فريق التحصيل / Collections', teamId: 'team' },
];
const rows = Array.from({ length: 125 }, (_, i) => ({ id: `case-${i}`, caseNumber: `CASE-${String(i).padStart(3, '0')}`, customerName: i < 3 ? 'FILTER عميل مطابق' : `عميل رقم ${i} · Customer ${i}`, outstandingAmount: 1000 + i, outstandingBalance: 1000 + i, mobile: '01012345678', status: 'ACTIVE', importName: 'محفظة أكتوبر / October portfolio', clientName: 'بنك تجريبي / Demo bank', portfolioName: 'October portfolio', customerCode: `C-${i}`, daysPastDue: 45, bucket: '31–60', priority: 'MEDIUM' }));
const assignedRows = rows.slice(0, 2).map(row => ({ ...row, collectorId: 'c1', collectorName: collectors[0].name, assignedCollectorName: collectors[0].name, assignedAt: '2026-10-10T10:00:00Z' }));
apiClient.defaults.adapter = async config => {
  const url = new URL(config.url!, 'http://synthetic.test');
  Object.entries(config.params ?? {}).forEach(([key, value]) => { if (value !== undefined) url.searchParams.set(key, String(value)); });
  const payload = config.data ? JSON.parse(config.data) : undefined;
  qa.requests.push({ path: url.pathname, params: Object.fromEntries(url.searchParams), method: config.method, payload });
  await new Promise(resolve => setTimeout(resolve, 40));
  if (qa.fail) throw new Error('Synthetic loading failure');
  let data: any;
  if (/\/summary$/.test(url.pathname) || url.pathname === '/collections/dashboard') data = { totalCases: 127, unassignedCases: 125, assignedCases: 2, collectors: 2, activeCollectors: 2 };
  else if (/\/collectors$/.test(url.pathname)) data = collectors;
  else if (/\/imports$/.test(url.pathname)) data = [{ id: 'import', name: 'October portfolio' }];
  else if (/\/clients$/.test(url.pathname)) data = { items: [{ id: 'client', name: 'Demo bank' }], totalCount: 1, totalPages: 1 };
  else if (config.method === 'get' && (/\/(unassigned|assigned)$/.test(url.pathname) || url.pathname === '/collections/cases')) {
    let items = qa.empty ? [] : (/\/assigned$/.test(url.pathname) || url.searchParams.get('unassigned') === 'false') ? assignedRows : rows;
    if (url.searchParams.get('search')) items = items.filter(item => item.customerName.toLowerCase().includes(url.searchParams.get('search')!.toLowerCase()));
    const page = Number(url.searchParams.get('page') ?? 1), pageSize = Number(url.searchParams.get('pageSize') ?? 20);
    data = { items: items.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: items.length, totalPages: Math.ceil(items.length / pageSize) };
  } else if (/\/auto\/preview$/.test(url.pathname)) {
    data = { method: payload.method, totalCases: payload.caseIds.length, totalOutstanding: payload.caseIds.reduce((total: number, caseId: string) => total + rows.find(row => row.id === caseId)!.outstandingAmount, 0), collectors: payload.collectorIds.map((collectorId: string, index: number) => ({ collectorId, collectorName: collectors.find(c => c.id === collectorId)!.name, caseCount: Math.floor(payload.caseIds.length / payload.collectorIds.length) + (index < payload.caseIds.length % payload.collectorIds.length ? 1 : 0), outstandingAmount: 2000 })) };
  } else if (/\/automatic(\/preview)?$/.test(url.pathname)) {
    data = { ruleCode: 'BALANCED_GEO_V1', caseCount: payload.caseIds.length, assignments: payload.caseIds.map((caseId: string, index: number) => ({ caseId, collectorId: payload.collectorIds[index % payload.collectorIds.length] })), collectors: payload.collectorIds.map((collectorId: string) => ({ collectorId, collectorName: collectors.find(c => c.id === collectorId)!.name, currentWorkload: 12, proposedAdditionalCases: 2, resultingWorkload: 14 })) };
  } else if (/\/preview$/.test(url.pathname)) data = { caseCount: payload.caseIds.length, totalOutstanding: 2000, collectorId: payload.collectorId, collectorName: collectors[0].name, previousCollectors: [collectors[1].name], collectors: [{ collectorId: 'c1', collectorName: collectors[0].name, currentWorkload: 12, proposedAdditionalCases: payload.caseIds.length, resultingWorkload: 12 + payload.caseIds.length }] };
  else if (config.method === 'post') data = { caseCount: payload?.caseIds?.length ?? 0, collectorName: collectors[0].name };
  else data = { ...rows[0], assignedCollectorName: collectors[0].name };
  return { data, config, status: 200, statusText: 'OK', headers: {} };
};
function BankShell() { return <Outlet context={{ bank: { id }, organizationKind: kind, classification: { primary: 'act', secondary: 'auto' } }} />; }
createRoot(document.getElementById('root')!).render(<BrowserRouter><LocalizationProvider><ToastProvider><AuthProvider><main className="mx-auto max-w-7xl p-3 sm:p-6"><Routes>{kind === 'general' ? <Route path="*" element={<CollectionAssignmentsPage />} /> : <Route element={<BankShell />}><Route path="*" element={<BankCaseDistributionPage />} /></Route>}</Routes></main></AuthProvider></ToastProvider></LocalizationProvider></BrowserRouter>);
