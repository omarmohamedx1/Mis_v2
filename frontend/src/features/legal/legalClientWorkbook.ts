import { autoMapImportColumns, type ImportFieldDef } from '../import/excelAutoMap';
import type { LegalClientColumnMapping, LegalClientWorkbook } from './types/legal';

export const legalClientImportFields: ImportFieldDef[] = [
  { key: 'name', ar: 'اسم العميل', en: 'Client name', required: true, aliases: ['name', 'fullname', 'customer name', 'customer full name', 'Cust Full Name', 'name arabic', 'name english', 'الاسم', 'الاسم الكامل', 'اسم العميل بالعربي', 'اسم العميل بالعربية'] },
  { key: 'nationalId', ar: 'الرقم القومي', en: 'National ID', aliases: ['ID Number', 'nationalid', 'national id number', 'رقم الهوية', 'رقم قومي'] },
  { key: 'mobileNumber', ar: 'رقم الموبايل', en: 'Mobile number', aliases: ['mobile', 'phone', 'telephone', 'phone number', 'رقم الهاتف', 'الهاتف', 'الموبايل'] },
  { key: 'externalIdentifier', ar: 'رقم الحساب أو معرّف العميل', en: 'Account or customer identifier', aliases: ['Installment Unique ID', 'external identifier', 'customer code', 'customer id', 'account', 'account no', 'account number', 'رقم الحساب', 'كود العميل', 'رقم العميل', 'رقم العقد'] },
  { key: 'address', ar: 'العنوان', en: 'Address', aliases: ['client address', 'customer address', 'address arabic', 'العنوان بالعربي'] },
  { key: 'notes', ar: 'ملاحظات', en: 'Notes', aliases: ['note', 'comments', 'remarks', 'الملاحظات'] },
];

// Keep this naming rule aligned with LegalClientWorkbookReader.BuildHeaders.
export function workbookHeaders(rows: string[][], headerRow: number): string[] {
  const width = Math.max(0, ...rows.map(row => row.length));
  const used = new Set<string>();
  return Array.from({ length: width }, (_, index) => {
    const basis = rows[headerRow - 1]?.[index]?.trim() || `Column ${index + 1}`;
    let header = basis;
    for (let suffix = 2; used.has(header.toLowerCase()); suffix++) header = `${basis} (${suffix})`;
    used.add(header.toLowerCase());
    return header;
  });
}

export function suggestWorkbookMapping(workbook: LegalClientWorkbook, sheetIndex: number, headerRow?: number): LegalClientColumnMapping {
  const sheet = workbook.sheets[sheetIndex];
  if (!sheet) return { sheetName: null, headerRow: 1, columns: {} };
  let selectedRow = headerRow ?? 1;
  if (headerRow === undefined) {
    let bestScore = -1;
    sheet.rows.forEach((row, index) => {
      const mapping = autoMapImportColumns(legalClientImportFields, workbookHeaders(sheet.rows, index + 1));
      const score = Object.values(mapping).filter(Boolean).length * 100 + row.filter(cell => cell.trim()).length;
      if (score > bestScore) { bestScore = score; selectedRow = index + 1; }
    });
  }
  return {
    sheetName: sheet.name,
    headerRow: selectedRow,
    columns: autoMapImportColumns(legalClientImportFields, workbookHeaders(sheet.rows, selectedRow)),
    extraColumns: workbookHeaders(sheet.rows, selectedRow),
  };
}

export function hasClientIdentityMapping(mapping: LegalClientColumnMapping): boolean {
  return Boolean(mapping.columns.name && (mapping.columns.nationalId || mapping.columns.mobileNumber || mapping.columns.externalIdentifier));
}
