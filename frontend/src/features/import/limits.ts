export const EXCEL_IMPORT_MAX_BYTES = 50 * 1024 * 1024;
export const EXCEL_IMPORT_ACCEPT = '.xlsx,.xls,.csv';
export const EXCEL_IMPORT_EXTENSIONS = ['.xlsx', '.xls', '.csv'] as const;

export function excelFileExtension(name: string): string {
  const index = name.lastIndexOf('.');
  return index >= 0 ? name.slice(index).toLowerCase() : '';
}

export function isSupportedExcelFile(file: File): boolean {
  return EXCEL_IMPORT_EXTENSIONS.includes(excelFileExtension(file.name) as (typeof EXCEL_IMPORT_EXTENSIONS)[number]);
}

export function formatExcelBytes(value: number, arabic: boolean): string {
  const units = arabic ? ['بايت', 'ك.ب', 'م.ب'] : ['B', 'KB', 'MB'];
  if (value < 1024) return `${value} ${units[0]}`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} ${units[1]}`;
  return `${(value / 1024 / 1024).toFixed(1)} ${units[2]}`;
}

export function validateExcelFile(file: File, arabic: boolean): string | null {
  if (!isSupportedExcelFile(file)) {
    return arabic ? 'اختار ملف Excel أو CSV (XLSX / XLS / CSV).' : 'Choose an Excel or CSV file (XLSX / XLS / CSV).';
  }
  if (file.size <= 0) {
    return arabic ? 'الملف فاضي.' : 'The selected file is empty.';
  }
  if (file.size > EXCEL_IMPORT_MAX_BYTES) {
    return arabic ? 'حجم الملف أكبر من 50 ميجابايت.' : 'The file is larger than 50 MB.';
  }
  return null;
}
