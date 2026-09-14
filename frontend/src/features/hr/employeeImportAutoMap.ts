/**
 * Automatic exclusive Excel header → employee import field mapping.
 * One source column can map to at most one field.
 */

export type EmployeeImportFieldKey =
  | 'MobileNumber'
  | 'EmployeeNumber'
  | 'FullName'
  | 'Gender'
  | 'Department'
  | 'Position'
  | 'OperationalRole'
  | 'NationalId'
  | 'DateOfBirth'
  | 'WorkStartDate'
  | 'FingerprintEnrollmentDate'
  | 'WorkEndDate'
  | 'Address'
  | 'Status';

export interface EmployeeImportFieldDef {
  key: EmployeeImportFieldKey;
  en: string;
  ar: string;
  /** Required in the workbook header for automatic import to proceed. */
  requiredInFile: boolean;
  aliases: string[];
}

/**
 * Priority order matters for exclusive claiming.
 * NationalId before Mobile so "Card Number" never becomes phone.
 * Position before OperationalRole so "Title" never becomes Role.
 */
export const employeeImportFields: readonly EmployeeImportFieldDef[] = [
  {
    key: 'EmployeeNumber',
    en: 'Employee Number',
    ar: 'رقم الموظف',
    requiredInFile: true,
    aliases: ['code', 'employee code', 'employee number', 'employee no', 'emp no', 'emp number', 'emp code', 'رقم الموظف', 'كود الموظف'],
  },
  {
    key: 'FullName',
    en: 'Employee Name',
    ar: 'اسم الموظف',
    requiredInFile: true,
    aliases: ['name in arabic', 'name in english', 'arabic name', 'english name', 'employee name', 'full name', 'name', 'اسم الموظف', 'الاسم', 'الاسم بالعربي', 'الاسم بالانجليزي'],
  },
  {
    key: 'NationalId',
    en: 'National ID',
    ar: 'الرقم القومي',
    requiredInFile: true,
    aliases: ['card number', 'national id', 'national id number', 'id number', 'nid', 'الرقم القومي', 'رقم البطاقة', 'رقم قومي'],
  },
  {
    key: 'Position',
    en: 'Position / Job Title',
    ar: 'المسمى الوظيفي',
    requiredInFile: true,
    aliases: ['title', 'position', 'job title', 'job', 'المسمى الوظيفي', 'الوظيفة'],
  },
  {
    key: 'WorkStartDate',
    en: 'Employment Date',
    ar: 'تاريخ التعيين',
    requiredInFile: true,
    aliases: ['date of employment', 'employment date', 'hire date', 'start date', 'work start date', 'joining date', 'تاريخ التعيين'],
  },
  {
    key: 'Department',
    en: 'Department',
    ar: 'القسم',
    requiredInFile: false,
    aliases: ['department', 'dept', 'القسم'],
  },
  {
    key: 'OperationalRole',
    en: 'Employee Role',
    ar: 'الدور الوظيفي',
    // Role is NOT the Excel "Title" column. Sheets often omit it; backend defaults to ADMIN.
    requiredInFile: false,
    aliases: ['role', 'employee role', 'employee type', 'operational role', 'الدور الوظيفي', 'الدور'],
  },
  {
    key: 'MobileNumber',
    en: 'Mobile Number',
    ar: 'رقم الموبايل',
    requiredInFile: false,
    aliases: ['mobile number', 'mobile', 'phone number', 'phone', 'telephone', 'رقم الموبايل', 'رقم الهاتف', 'التليفون', 'موبايل'],
  },
  {
    key: 'Gender',
    en: 'Gender',
    ar: 'النوع',
    requiredInFile: false,
    aliases: ['male female', 'male - female', 'gender', 'sex', 'النوع'],
  },
  {
    key: 'DateOfBirth',
    en: 'Date of Birth',
    ar: 'تاريخ الميلاد',
    requiredInFile: false,
    aliases: ['birth of day', 'date of birth', 'birth date', 'dob', 'تاريخ الميلاد'],
  },
  {
    key: 'FingerprintEnrollmentDate',
    en: 'Fingerprint Date',
    ar: 'تاريخ البصمة',
    requiredInFile: false,
    aliases: ['fingerprint date', 'fingerprint', 'تاريخ البصمة'],
  },
  {
    key: 'WorkEndDate',
    en: 'End Work Date',
    ar: 'تاريخ انتهاء العمل',
    requiredInFile: false,
    aliases: ['date out of work employer', 'end work date', 'termination date', 'work end date', 'تاريخ انتهاء العمل'],
  },
  {
    key: 'Address',
    en: 'Address',
    ar: 'العنوان',
    requiredInFile: false,
    aliases: ['address', 'العنوان'],
  },
  {
    key: 'Status',
    en: 'Status',
    ar: 'الحالة',
    requiredInFile: false,
    aliases: ['status', 'الحالة'],
  },
];

export function normalizeImportHeader(value: string): string {
  return value
    .normalize('NFKC')
    .replace(/^\uFEFF/, '')
    .replace(/[\u200B-\u200D\uFEFF\u00A0\u202F\u2007\u2060]/g, ' ')
    .replace(/[_./\\:|()]+/g, ' ')
    .replace(/[-–—−]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}

function compactHeader(value: string): string {
  return normalizeImportHeader(value).replace(/[^a-z0-9\u0600-\u06ff]+/gi, '');
}

function scoreAliasMatch(column: string, alias: string): number {
  const normalizedColumn = normalizeImportHeader(column);
  const normalizedAlias = normalizeImportHeader(alias);
  if (!normalizedColumn || !normalizedAlias) return 0;
  if (normalizedColumn === normalizedAlias) return 100 + normalizedAlias.length;
  if (compactHeader(column) === compactHeader(alias)) return 90 + normalizedAlias.length;
  return 0;
}

export function autoMapEmployeeImportColumns(detectedColumns: readonly string[]): Record<string, string> {
  const columns: Record<string, string> = Object.fromEntries(employeeImportFields.map((field) => [field.key, '']));
  const claimed = new Set<string>();

  for (const field of employeeImportFields) {
    let bestColumn = '';
    let bestScore = 0;
    for (const column of detectedColumns) {
      if (claimed.has(column) || !column?.trim()) continue;
      for (const alias of field.aliases) {
        const score = scoreAliasMatch(column, alias);
        if (score > bestScore) {
          bestScore = score;
          bestColumn = column;
        }
      }
    }
    if (bestColumn) {
      columns[field.key] = bestColumn;
      claimed.add(bestColumn);
    }
  }

  return columns;
}

export function missingRequiredEmployeeImportFields(columns: Record<string, string>): EmployeeImportFieldDef[] {
  return employeeImportFields.filter((field) => field.requiredInFile && !columns[field.key]?.trim());
}

/** Headers that strongly identify an employee master sheet. */
export const employeeHeaderDetectionHints = [
  'code',
  'name in arabic',
  'male female',
  'title',
  'card number',
  'date of employment',
  'fingerprint date',
  'birth of day',
  'address',
  'date out of work employer',
  'department',
  'mobile',
  'national id',
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
