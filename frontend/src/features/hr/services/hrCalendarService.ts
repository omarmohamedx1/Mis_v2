import { apiClient } from '../../../services/apiClient';
import type {
  CalendarExceptionDetails,
  CalendarExceptionQuery,
  PagedCalendarExceptions,
  SaveCalendarExceptionRequest,
  UpdateWorkingCalendarRequest,
  WorkingCalendar,
} from '../types/calendar';
import { loadAllHrPages } from './hrPaging';

export const hrCalendarService = {
  async getWorkingCalendar(): Promise<WorkingCalendar> {
    const { data } = await apiClient.get<WorkingCalendar>('/hr/calendar/working-calendar');
    return data;
  },

  async updateWorkingCalendar(request: UpdateWorkingCalendarRequest): Promise<WorkingCalendar> {
    const { data } = await apiClient.put<WorkingCalendar>('/hr/calendar/working-calendar', request);
    return data;
  },

  async getExceptions(query: CalendarExceptionQuery): Promise<PagedCalendarExceptions> {
    return loadAllHrPages(async (page, pageSize) => {
      const { data } = await apiClient.get<PagedCalendarExceptions>('/hr/calendar/exceptions', {
      params: {
        dateFrom: query.dateFrom || undefined,
        dateTo: query.dateTo || undefined,
        isActive: query.isActive ?? undefined,
        page,
        pageSize,
        search: query.search || undefined,
        type: query.type || undefined,
      },
    });
      return data;
    });
  },

  async getException(id: string): Promise<CalendarExceptionDetails> {
    const { data } = await apiClient.get<CalendarExceptionDetails>(`/hr/calendar/exceptions/${id}`);
    return data;
  },

  async createException(request: SaveCalendarExceptionRequest): Promise<CalendarExceptionDetails> {
    const { data } = await apiClient.post<CalendarExceptionDetails>('/hr/calendar/exceptions', request);
    return data;
  },

  async updateException(id: string, request: SaveCalendarExceptionRequest): Promise<CalendarExceptionDetails> {
    const { data } = await apiClient.put<CalendarExceptionDetails>(`/hr/calendar/exceptions/${id}`, request);
    return data;
  },

  async setExceptionActive(id: string, isActive: boolean): Promise<CalendarExceptionDetails> {
    const { data } = await apiClient.patch<CalendarExceptionDetails>(`/hr/calendar/exceptions/${id}/active`, { isActive });
    return data;
  },

  async deleteException(id: string, reason?: string): Promise<void> {
    await apiClient.delete(`/hr/calendar/exceptions/${id}`, { data: { reason: reason?.trim() || null } });
  },
};
