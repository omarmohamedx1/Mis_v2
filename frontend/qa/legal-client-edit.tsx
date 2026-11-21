// All requests use synthetic in-memory records. No business API is contacted.
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { AxiosError } from 'axios';
import { LocalizationProvider } from '../src/context/LocalizationContext';
import { ToastProvider } from '../src/components/common/Toast';
import { apiClient } from '../src/services/apiClient';
import { LegalClientsPage } from '../src/pages/legal/LegalClientsPage';
import '../src/styles.css';

const params = new URLSearchParams(location.search);
localStorage.setItem('mis.language', params.get('lang') ?? 'en');
const scenario = params.get('scenario') ?? 'ok';
const rows = [1, 2].map(index => ({ customerId: `00000000-0000-0000-0000-00000000000${index}`, name: `Client ${index}`, nationalId: `ID-${index}`, bankName: 'Test bank', status: 'ACTIVE', transferredFromCollections: false, updatedAt: '2026-01-01T00:00:00Z' }));
const notes: Record<string, string> = { [rows[0].customerId]: 'First notes', [rows[1].customerId]: 'Second notes' };
const qa = { requests: [] as Array<{ method?: string; url?: string; data?: unknown }>, rows, notes };
(window as unknown as { legalEditQa: typeof qa }).legalEditQa = qa;
apiClient.defaults.adapter = async config => {
  qa.requests.push({ method: config.method, url: config.url, data: config.data });
  const reply = (data: unknown, status = 200) => ({ data, status, statusText: String(status), headers: {}, config });
  if (config.url === '/legal/clients') return reply({ items: rows, page: 1, pageSize: 20, totalCount: 2, totalPages: 1 });
  const row = rows.find(item => config.url === `/legal/clients/${item.customerId}`);
  if (!row) throw new Error(`Unexpected test request: ${config.url}`);
  const failure = /^\d+$/.test(scenario) ? Number(scenario) : scenario === 'save-error' && config.method === 'put' ? 500 : 0;
  if (failure) throw new AxiosError('Test failure', 'ERR_BAD_RESPONSE', config, undefined, reply({ message: 'Test failure' }, failure));
  if (scenario === 'network') throw new AxiosError('Network Error', 'ERR_NETWORK', config);
  if (scenario === 'malformed') return reply({ canManage: true });
  if (config.method === 'put') {
    const input = JSON.parse(config.data);
    row.status = input.status;
    notes[row.customerId] = input.notes;
  }
  return reply({ client: scenario === 'wrong-id' ? rows[0] : row, notes: notes[row.customerId], canManage: scenario !== 'readonly', documents: [], collectionCaseNumbers: [] });
};
createRoot(document.getElementById('root')!).render(<LocalizationProvider><ToastProvider><MemoryRouter><LegalClientsPage /></MemoryRouter></ToastProvider></LocalizationProvider>);
