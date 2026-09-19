/**
 * Automatic exclusive Excel header → employee import field mapping.
 * One source column can map to at most one field.
 */

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
    aliases: ['code', 'employee code', 'employee number', 'employee no', 'emp no', 'emp number', 'emp code', 'رقم الموظف', 'رقم المظف', 'كود الموظف'],
  },
  {
    key: 'FullNameArabic',
    en: 'Employee Name Arabic',
    ar: 'اسم الموظف بالعربية',
    requiredInFile: false,
    aliases: ['name in arabic', 'arabic name', 'arabic full name', 'employee name arabic', 'اسم الموظف بالعربية', 'الاسم بالعربي', 'الاسم العربي', 'اسم الموظف', 'اسم المظف', 'الاسم'],
  },
  {
    key: 'FullNameEnglish',
    en: 'Employee Name English',
    ar: 'اسم الموظف بالإنجليزية',
    requiredInFile: false,
    aliases: ['name in english', 'english name', 'english full name', 'employee name english', 'employee name', 'full name', 'اسم الموظف بالانجليزي', 'الاسم بالانجليزي', 'الاسم الإنجليزي'],
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
    aliases: ['position / job title', 'position job title', 'title', 'position', 'job title', 'job', 'المسمى الوظيفي', 'الوظيفة'],
  },
  {
    key: 'WorkStartDate',
    en: 'Employment Date',
    ar: 'تاريخ التعيين',
    requiredInFile: true,
    aliases: ['date of employment', 'date of empoloyment', 'employment date', 'empoloyment date', 'hire date', 'start date', 'work start date', 'joining date', 'تاريخ التعيين'],
  },
  {
    key: 'Department',
    en: 'Department',
    ar: 'القسم',
    requiredInFile: false,
    aliases: ['department', 'dept', 'القسم'],
  },
  {
    key: 'Organization',
    en: 'Assigned Bank / Company',
    ar: 'البنك / الشركة المكلّف بها',
    requiredInFile: false,
    aliases: [
      'assigned bank / company',
      'assigned bank company',
      'assigned bank',
      'assigned company',
      'bank',
      'company',
      'client',
      'organization',
      'البنك / الشركة المكلف بها',
      'البنك / الشركة',
      'البنك',
      'الشركة',
      'الجهة',
    ],
  },
  {
    key: 'WorkNumber',
    en: 'Work Number',
    ar: 'رقم الشغل',
    requiredInFile: false,
    aliases: ['work number', 'job number', 'work no', 'worknumber', 'رقم الشغل', 'رقم العمل'],
  },
  {
    key: 'PackageType',
    en: 'Package Type',
    ar: 'نوع الباقة',
    requiredInFile: false,
    aliases: ['package type', 'packagetype', 'package', 'bundle', 'نوع الباقة', 'نوع الباقه', 'الباقة', 'الباقه'],
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
  {
    key: 'BasicSalary',
    en: 'Basic Salary',
    ar: 'الراتب الأساسي',
    requiredInFile: false,
    aliases: ['basic salary', 'salary', 'basic pay', 'gross salary', 'الراتب الأساسي', 'المرتب', 'الراتب'],
  },
  {
    key: 'Allowances',
    en: 'Allowances',
    ar: 'البدلات',
    requiredInFile: false,
    aliases: ['allowances', 'allowance', 'بدلات', 'البدلات', 'البدل'],
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
  const compactColumn = compactHeader(column);
  const compactAlias = compactHeader(alias);
  if (compactColumn && compactColumn === compactAlias) return 90 + normalizedAlias.length;
  const aliasWords = normalizedAlias.split(' ').filter(Boolean);
  if (aliasWords.length >= 2) {
    if (` ${normalizedColumn} `.includes(` ${normalizedAlias} `)) return 70 + normalizedAlias.length;
    if (compactColumn.startsWith(compactAlias) || compactColumn.endsWith(compactAlias)) return 60 + normalizedAlias.length;
  }
  return 0;
}

function fieldMatchLabels(field: EmployeeImportFieldDef): string[] {
  return [field.en, field.ar, ...field.aliases];
}

export function autoMapEmployeeImportColumns(detectedColumns: readonly string[]): Record<string, string> {
  const columns: Record<string, string> = Object.fromEntries(employeeImportFields.map((field) => [field.key, '']));
  const claimed = new Set<string>();

  for (const field of employeeImportFields) {
    let bestColumn = '';
    let bestScore = 0;
    for (const column of detectedColumns) {
      if (claimed.has(column) || !column?.trim()) continue;
      for (const alias of fieldMatchLabels(field)) {
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

export function scoreEmployeeSheetMapping(detectedColumns: readonly string[]): number {
  const mapped = autoMapEmployeeImportColumns(detectedColumns);
  const mappedCount = Object.values(mapped).filter((value) => value.trim()).length;
  const missingRequired = missingRequiredEmployeeImportFields(mapped).length;
  return mappedCount * 10 - missingRequired * 50 + scoreEmployeeHeaderRow(detectedColumns);
}

export function pickEmployeeImportSheetIndex(sheets: readonly { detectedColumns: readonly string[] }[]): number {
  let bestIndex = 0;
  let bestScore = Number.NEGATIVE_INFINITY;
  sheets.forEach((sheet, index) => {
    const score = scoreEmployeeSheetMapping(sheet.detectedColumns);
    if (score > bestScore) {
      bestScore = score;
      bestIndex = index;
    }
  });
  return bestIndex;
}

export function missingRequiredEmployeeImportFields(columns: Record<string, string>): EmployeeImportFieldDef[] {
  const mappedName = Boolean(columns.FullNameArabic?.trim() || columns.FullNameEnglish?.trim());
  return employeeImportFields.filter((field) => {
    if (field.key === 'FullNameArabic' || field.key === 'FullNameEnglish') return false;
    return field.requiredInFile && !columns[field.key]?.trim();
  }).concat(mappedName ? [] : employeeImportFields.filter((field) => field.key === 'FullNameArabic'));
}

/** Headers that strongly identify an employee master sheet. */
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
