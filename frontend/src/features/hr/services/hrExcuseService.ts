import { apiClient } from '../../../services/apiClient';
import { loadAllHrPages } from './hrPaging';

export interface ExcuseAttachment {
  id: string;
  excuseId: string;
  fileName: string;
  contentType: string;
  length: number;
  uploadedByUserId: string;
  uploadedAt: string;
}

export interface ExcuseItem {
  id: string;
  employeeId: string | null;
  employeeNumber: string | null;
  employeeName: string;
  departmentId: string | null;
  department: string | null;
  position: string | null;
  type: string;
  source: string;
  date: string;
  fromTime: string | null;
  toTime: string | null;
  status: string;
  visitId: string | null;
  collectorUserId: string | null;
  caseId: string | null;
  caseReference: string | null;
  customer: string | null;
  organization: string | null;
  address: string | null;
  visitStatus: string | null;
  visitResult: string | null;
  visitNotes: string | null;
  visitCreatedBy: string | null;
  reason: string | null;
  notes: string | null;
  decisionBy: string | null;
  decisionAt: string | null;
  rejectionReason: string | null;
  createdAt: string;
  updatedAt: string;
  sourceChanged: boolean;
  attachments: ExcuseAttachment[];
  fullDay?: boolean;
}

export interface ExcusePage {
  items: ExcuseItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  pendingApproval: number;
  canManage: boolean;
  canApprove: boolean;
}

export interface ExcuseTypeOption {
  code: string;
  name: string;
  reasonRequired: boolean;
}

export interface ManualExcuse {
  employeeId: string;
  type: string;
  date: string;
  fromTime: string | null;
  toTime: string | null;
  fullDay: boolean;
  approveImmediately: boolean;
  reason: string;
  notes: string | null;
  expectedUpdatedAt?: string | null;
}

const base = '/hr/excuses';

export const hrExcuseService = {
  async types() {
    return (await apiClient.get<ExcuseTypeOption[]>(`${base}/types`)).data;
  },
  async list(params: Record<string, string | number | undefined>) {
    return loadAllHrPages(async (page, pageSize) => (await apiClient.get<ExcusePage>(base, {
      params: Object.fromEntries(Object.entries({ ...params, page, pageSize }).filter(([, value]) => value !== undefined && (typeof value !== 'string' || value !== ''))),
    })).data);
  },
  async details(id: string) {
    return (await apiClient.get<ExcuseItem>(`${base}/${id}`)).data;
  },
  async notifications() {
    return (await apiClient.get<ExcuseItem[]>(`${base}/notifications`)).data;
  },
  async create(form: ManualExcuse) {
    return (await apiClient.post<string>(base, form)).data;
  },
  async edit(id: string, form: ManualExcuse) {
    return (await apiClient.put<string>(`${base}/${id}`, form)).data;
  },
  async decide(item: ExcuseItem, approve: boolean, reason: string, notes: string, toTime: string) {
    await apiClient.post(`${base}/${item.id}/decision`, {
      approve,
      reason,
      notes,
      toTime: toTime || null,
      expectedUpdatedAt: item.updatedAt,
    });
  },
  async cancel(item: ExcuseItem, reason: string) {
    await apiClient.post(`${base}/${item.id}/cancel`, { reason, expectedUpdatedAt: item.updatedAt });
  },
  async link(collectorId: string, employeeId: string) {
    await apiClient.post(`${base}/collectors/${collectorId}/employee`, { employeeId });
  },
  async upload(id: string, file: File, attachmentId?: string) {
    const form = new FormData();
    form.append('file', file);
    if (attachmentId) form.append('attachmentId', attachmentId);
    await apiClient.post(`${base}/${id}/attachments`, form);
  },
  async remove(id: string, attachmentId: string) {
    await apiClient.delete(`${base}/${id}/attachments/${attachmentId}`);
  },
  async file(id: string, attachment: ExcuseAttachment, download: boolean) {
    const response = await apiClient.get<Blob>(`${base}/${id}/attachments/${attachment.id}`, {
      responseType: 'blob',
      params: { download },
    });
    const url = URL.createObjectURL(response.data);
    const a = document.createElement('a');
    a.href = url;
    if (download) a.download = attachment.fileName;
    else {
      a.target = '_blank';
      a.rel = 'noopener noreferrer';
    }
    a.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 60000);
  },
};

export function formatExcuseTime(value: string | null | undefined, language: 'ar' | 'en') {
  if (!value) return null;
  const [hoursText, minutesText] = value.slice(0, 5).split(':');
  const hours = Number(hoursText);
  const minutes = minutesText ?? '00';
  if (Number.isNaN(hours)) return value.slice(0, 5);
  const suffix = language === 'ar' ? (hours >= 12 ? 'م' : 'ص') : hours >= 12 ? 'PM' : 'AM';
  const hour12 = hours % 12 || 12;
  return `${String(hour12).padStart(2, '0')}:${minutes} ${suffix}`;
}
