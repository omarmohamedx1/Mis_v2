export const attendanceStatuses = ['Present', 'Absent', 'Late', 'Leave', 'Holiday', 'Weekend'] as const;
export type AttendanceStatus = (typeof attendanceStatuses)[number];

export const attendanceSources = ['ExcelImport', 'Manual', 'DeviceIntegration', 'SystemProcessing'] as const;
export type AttendanceSource = (typeof attendanceSources)[number];

export interface AttendanceListItem {
  id: string;
  employeeId: string;
  employeeNumber: string;
  employeeName: string;
  departmentId: string;
  departmentName: string;
  branchId: string | null;
  branchName: string | null;
  attendanceDate: string;
  checkIn: string | null;
  checkOut: string | null;
  workingHours: number;
  lateMinutes: number;
  earlyLeaveMinutes: number;
  overtimeMinutes: number;
  status: AttendanceStatus;
  source: AttendanceSource;
  isManuallyAdjusted: boolean;
}

export interface AttendanceDetails extends AttendanceListItem {
  notes: string | null;
  importBatchId: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface PagedAttendanceRecords {
  items: AttendanceListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AttendanceQuery {
  page: number;
  pageSize: number;
  search?: string;
  employeeId?: string;
  departmentId?: string;
  branchId?: string;
  dateFrom?: string;
  dateTo?: string;
  status?: AttendanceStatus | '';
  source?: AttendanceSource | '';
  sortBy?: string;
  sortDescending?: boolean;
}

export interface SaveManualAttendanceRequest {
  employeeId: string;
  attendanceDate: string;
  checkIn: string | null;
  checkOut: string | null;
  status: AttendanceStatus;
  notes: string | null;
}

export interface ProcessAttendanceDayResult {
  attendanceDate: string;
  createdRecords: number;
  absent: number;
  onLeave: number;
  holiday: number;
  weekend: number;
  existingRecordsSkipped: number;
}

export const attendanceImportLayouts = ['CheckInCheckOutColumns', 'PunchRows'] as const;
export type AttendanceImportLayout = (typeof attendanceImportLayouts)[number];

export const attendanceImportBatchStatuses = ['Uploaded', 'PreviewReady', 'Confirmed', 'Failed', 'Cancelled'] as const;
export type AttendanceImportBatchStatus = (typeof attendanceImportBatchStatuses)[number];

export const attendanceImportCategories = ['Valid', 'Invalid', 'EmployeeNotFound', 'Duplicate', 'MissingCheckIn', 'MissingCheckOut'] as const;
export type AttendanceImportCategory = (typeof attendanceImportCategories)[number];

export interface AttendanceImportSheet {
  suggestedTimeSystem: '24-hour' | '12-hour';
  sheetName: string | null;
  suggestedHeaderRowNumber: number;
  detectedColumns: string[];
}

export interface AttendanceImportUpload {
  batchId: string;
  fileName: string;
  fileSize: number;
  fileHash: string;
  status: AttendanceImportBatchStatus;
  sheets: AttendanceImportSheet[];
  uploadedAt: string;
}

export interface AttendanceImportMappingRequest {
  sheetName: string | null;
  headerRowNumber: number;
  dataStartRowNumber: number;
  layout: AttendanceImportLayout;
  employeeNumberColumn: string;
  employeeNameColumn: string | null;
  attendanceDateColumn: string | null;
  checkInColumn: string | null;
  checkOutColumn: string | null;
  punchDateTimeColumn: string | null;
  punchTypeColumn: string | null;
  timeColumn: string | null;
  amPmColumn: string | null;
  dateFormat: string | null;
  timeFormat: string | null;
  timeSystem: '24-hour' | '12-hour';
  cultureName: string | null;
  timeZoneId: string;
}

export interface AttendanceImportSummary {
  totalRows: number;
  validRows: number;
  invalidRows: number;
  employeeNotFoundRows: number;
  duplicateRows: number;
  missingCheckInRows: number;
  missingCheckOutRows: number;
}

export interface AttendanceImportBatch {
  batchId: string;
  fileName: string;
  fileHash: string;
  status: AttendanceImportBatchStatus;
  mapping: AttendanceImportMappingRequest | null;
  summary: AttendanceImportSummary | null;
  failureReason: string | null;
  uploadedAt: string;
  previewedAt: string | null;
  confirmedAt: string | null;
}

export interface AttendanceImportPreviewRow {
  sourceRows: Record<string, string | null>[];
  id: string;
  batchId: string;
  sourceRowNumbers: number[];
  sourceEmployeeNumber: string | null;
  sourceEmployeeName: string | null;
  employeeId: string | null;
  employeeNumber: string | null;
  employeeName: string | null;
  attendanceDate: string | null;
  checkIn: string | null;
  checkOut: string | null;
  punches: string[];
  canImport: boolean;
  categories: AttendanceImportCategory[];
  errors: string[];
}

export interface PagedAttendanceImportPreview {
  items: AttendanceImportPreviewRow[];
  summary: AttendanceImportSummary;
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AttendanceImportPreviewQuery {
  page: number;
  pageSize: number;
  category?: AttendanceImportCategory | '';
  search?: string;
}

export interface AttendanceImportConfirmResult {
  batchId: string;
  importedRecords: number;
  skippedRows: number;
  duplicateRows: number;
  failedRows: number;
  confirmedAt: string;
}

export interface AttendanceImportHistoryItem {
  batchId: string;
  fileName: string;
  fileHash: string;
  status: AttendanceImportBatchStatus;
  summary: AttendanceImportSummary | null;
  importedRecords: number;
  uploadedByUserId: string;
  uploadedByUsername: string;
  uploadedAt: string;
  confirmedAt: string | null;
}

export interface PagedAttendanceImportHistory {
  items: AttendanceImportHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AttendanceImportHistoryQuery {
  page: number;
  pageSize: number;
  search?: string;
  status?: AttendanceImportBatchStatus | '';
  uploadedFrom?: string;
  uploadedTo?: string;
}
