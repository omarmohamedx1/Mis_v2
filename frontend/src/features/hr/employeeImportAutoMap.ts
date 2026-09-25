/**
 * Automatic exclusive Excel header → employee import field mapping.
 * One source column can map to at most one field.
 */

import {
  autoMapImportColumns,
  missingRequiredImportFields,
  normalizeImportHeader,
  pickBestImportSheetIndex,
  scoreAliasMatch,
  scoreSheetMapping,
} from '../import/excelAutoMap';
import { employeeImportCatalog } from '../import/fieldCatalogs';

export type EmployeeImportFieldKey =
  | 'MobileNumber'
  | 'EmployeeNumber'
  | 'FullNameArabic'
  | 'FullNameEnglish'
  | 'Gender'
  | 'Department'
  | 'Organization'
  | 'WorkNumber'
  | 'PackageType'
  | 'Position'
  | 'OperationalRole'
  | 'NationalId'
  | 'DateOfBirth'
  | 'WorkStartDate'
  | 'FingerprintEnrollmentDate'
  | 'WorkEndDate'
  | 'Address'
  | 'Status'
  | 'BasicSalary'
  | 'Allowances';

export interface EmployeeImportFieldDef {
  key: EmployeeImportFieldKey;
  en: string;
  ar: string;
  requiredInFile: boolean;
  aliases: string[];
}

export const employeeImportFields: readonly EmployeeImportFieldDef[] = employeeImportCatalog.map((field) => ({
  key: field.key as EmployeeImportFieldKey,
  en: field.en,
  ar: field.ar,
  requiredInFile: Boolean(field.required),
  aliases: [...field.aliases],
}));

export { normalizeImportHeader };

export function autoMapEmployeeImportColumns(detectedColumns: readonly string[]): Record<string, string> {
  return autoMapImportColumns(employeeImportCatalog, detectedColumns);
}

export function scoreEmployeeSheetMapping(detectedColumns: readonly string[]): number {
  return scoreSheetMapping(employeeImportCatalog, detectedColumns);
}

export function pickEmployeeImportSheetIndex(sheets: readonly { detectedColumns: readonly string[] }[]): number {
  return pickBestImportSheetIndex(employeeImportCatalog, sheets);
}

export function missingRequiredEmployeeImportFields(columns: Record<string, string>): EmployeeImportFieldDef[] {
  const mappedName = Boolean(columns.FullNameArabic?.trim() || columns.FullNameEnglish?.trim());
  return missingRequiredImportFields(employeeImportCatalog, columns, (field) => {
    if (field.key === 'FullNameArabic' || field.key === 'FullNameEnglish') return mappedName;
    return undefined;
  }).map((field) => employeeImportFields.find((item) => item.key === field.key)!);
}

export const employeeHeaderDetectionHints = [
  'code',
  'employee number',
  'name in arabic',
  'employee name arabic',
  'employee name english',
  'male female',
  'title',
  'position / job title',
  'card number',
  'date of employment',
  'date of empoloyment',
  'fingerprint date',
  'birth of day',
  'address',
  'date out of work employer',
  'department',
  'assigned bank / company',
  'assigned bank',
  'work number',
  'package type',
  'رقم الشغل',
  'نوع الباقة',
  'employee role',
  'mobile',
  'national id',
  'basic salary',
  'allowances',
  'رقم المظف',
  'اسم المظف',
  'الراتب الأساسي',
  'البدلات',
] as const;

export function scoreEmployeeHeaderRow(cells: readonly string[]): number {
  let score = 0;
  for (const cell of cells) {
    if (!cell?.trim()) continue;
    const normalized = normalizeImportHeader(cell);
    if (/[a-z\u0600-\u06ff]/i.test(normalized)) score += 1;
    for (const hint of employeeHeaderDetectionHints) {
      if (scoreAliasMatch(cell, hint) > 0) {
        score += 25;
        break;
      }
    }
  }
  return score;
}
