import type { TranslationKey } from '../../localization/translations';
import type { EmployeeListItem, EmployeeOperationalRole, EmployeeOrganizationAssignment } from './types/employee';

export function organizationLabel(org: Pick<EmployeeOrganizationAssignment, 'nameArabic' | 'nameEnglish' | 'code'>, language: 'ar' | 'en'): string {
  return (language === 'ar' ? org.nameArabic : org.nameEnglish) || org.code;
}

export function lookupLabel(item: { name?: string | null; nameArabic?: string | null; nameEnglish?: string | null }, language: string): string {
  return (language === 'ar' ? item.nameArabic : item.nameEnglish) || item.nameEnglish || item.nameArabic || item.name || '';
}

export function inferOperationalRole(position?: { code?: string | null; nameEnglish?: string | null; nameArabic?: string | null; name?: string | null } | null): EmployeeOperationalRole {
  const text = `${position?.code ?? ''} ${position?.nameEnglish ?? ''} ${position?.name ?? ''} ${position?.nameArabic ?? ''}`.toLowerCase();
  if (/(office boy|office girl|عامل خدمات|عاملة خدمات|اوفيس|أوفيس|أوفس|الاوفيس|الأوفيس|\boffice\b)/i.test(text)) return 'OFFICE';
  if (/(supervisor|مشرف|collection manager|collections manager|مدير تحصيل|مدير التحصيل)/i.test(text)) return 'SUPERVISOR';
  if (/(collector|محصل)/i.test(text)) return 'COLLECTOR';
  return 'ADMIN';
}

export function operationalRoleKey(role: EmployeeOperationalRole): TranslationKey {
  if (role === 'COLLECTOR') return 'collectorRole';
  if (role === 'SUPERVISOR') return 'supervisorRole';
  if (role === 'OFFICE') return 'officeRole';
  return 'adminRole';
}

export function keepArabicEmployeeName(value: string) {
  return keepPersonEmployeeName(value);
}

export function keepEnglishEmployeeName(value: string) {
  return keepPersonEmployeeName(value);
}

export function keepPersonEmployeeName(value: string) {
  return value.replace(/[^\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFFA-Za-z\s.\-'\u2019،ـ]/g, '');
}

function isPersonEmployeeName(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return true;
  return !/[^\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFFA-Za-z\s.\-'\u2019،ـ]/.test(trimmed)
    && (/[\u0600-\u06FF]/.test(trimmed) || /[A-Za-z]/.test(trimmed));
}

export function isArabicEmployeeName(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return true;
  return isPersonEmployeeName(trimmed) && /[\u0600-\u06FF]/.test(trimmed);
}

export function isEnglishEmployeeName(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return true;
  return isPersonEmployeeName(trimmed) && /[A-Za-z]/.test(trimmed);
}

export function employeeNameForLanguage(
  employee: { fullName?: string | null; fullNameArabic?: string | null; fullNameEnglish?: string | null },
  language: 'ar' | 'en',
): string {
  const preferred = language === 'ar' ? employee.fullNameArabic : employee.fullNameEnglish;
  return preferred?.trim() || employee.fullName?.trim() || '';
}

export function secondaryEmployeeName(employee: Pick<EmployeeListItem, 'fullName'> & { fullNameArabic?: string | null; fullNameEnglish?: string | null }, language: 'ar' | 'en'): string | null {
  const other = language === 'ar' ? employee.fullNameEnglish : employee.fullNameArabic;
  const trimmed = other?.trim();
  const primary = employeeNameForLanguage(employee, language);
  if (!trimmed || trimmed === primary) return null;
  return trimmed;
}
