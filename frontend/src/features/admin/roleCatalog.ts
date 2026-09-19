export const PERMISSION_GROUPS: Record<string, { ar: string; en: string; planned?: boolean }> = {
  ADMIN: { ar: 'إدارة النظام', en: 'Administration' },
  HR: { ar: 'الموارد البشرية', en: 'Human Resources' },
  COLLECTIONS: { ar: 'التحصيل والبنوك', en: 'Collections' },
  FINANCE: { ar: 'المالية والدفاتر', en: 'Finance & ledger' },
  ACCOUNTING: { ar: 'الحسابات والرواتب', en: 'Accounting & payroll' },
  DATA_ENTRY: { ar: 'إدخال البيانات', en: 'Data entry' },
  LEGAL: { ar: 'الشؤون القانونية', en: 'Legal' },
};

export const GROUP_ORDER = Object.keys(PERMISSION_GROUPS);

export interface RoleGuide {
  ar: string;
  en: string;
  summaryAr: string;
  summaryEn: string;
  packs: string[];
}

export const ROLE_GUIDE: Record<string, RoleGuide> = {
  Admin: {
    ar: 'مدير النظام',
    en: 'System administrator',
    summaryAr: 'يفتح النظام بالكامل: المستخدمين، الصلاحيات، وكل الموديولات.',
    summaryEn: 'Unrestricted control over users, access, and every module.',
    packs: ['*'],
  },
  HrManager: {
    ar: 'مدير موارد بشرية',
    en: 'HR manager',
    summaryAr: 'ملفات الموظفين، الحضور، اعتماد الإجازات، والتقارير الحساسة.',
    summaryEn: 'Employees, attendance, leave approval, and sensitive HR reports.',
    packs: ['hr.access', 'hr.sensitive.view', 'hr.employee.view', 'hr.employee.manage', 'hr.attendance.manage', 'hr.leave.approve', 'hr.report.export'],
  },
  HrOfficer: {
    ar: 'أخصائي موارد بشرية',
    en: 'HR officer',
    summaryAr: 'إدارة ملفات الموظفين والحضور دون اعتماد الإجازات أو التقارير الحساسة.',
    summaryEn: 'Employee files and attendance, without leave approval or sensitive exports.',
    packs: ['hr.access', 'hr.employee.view', 'hr.employee.manage', 'hr.attendance.manage'],
  },
  CollectionsCollector: {
    ar: 'محصل',
    en: 'Collector',
    summaryAr: 'متابعة الحالات، الأنشطة، الوعود، التحصيل، والزيارات ضمن نطاقه.',
    summaryEn: 'Cases, activities, promises, collections, and visits within assigned scope.',
    packs: ['collections.access', 'collections.dashboard.view', 'collections.case.view', 'collections.activity.manage', 'collections.ptp.manage', 'collections.payment.submit', 'collections.visit.manage'],
  },
  CollectionsSupervisor: {
    ar: 'مشرف تحصيل',
    en: 'Collections supervisor',
    summaryAr: 'توزيع الحالات، البيانات الحساسة، التقارير، ومراجعة دفعات الإدخال.',
    summaryEn: 'Assignments, sensitive data, reports, and data-entry batch review.',
    packs: ['collections.access', 'collections.dashboard.view', 'collections.case.view', 'collections.case.view_sensitive', 'collections.activity.manage', 'collections.assignment.manage', 'collections.ptp.manage', 'collections.visit.manage', 'collections.complaint.manage', 'collections.report.view', 'collections.report.export', 'collections.data_batch.review'],
  },
  CollectionsReviewer: {
    ar: 'مراجع تحصيل',
    en: 'Collections reviewer',
    summaryAr: 'اعتماد التحصيل وعرض الحالات والتقارير دون إدارة التوزيع.',
    summaryEn: 'Approve collections and view cases and reports, without assignment control.',
    packs: ['collections.access', 'collections.dashboard.view', 'collections.case.view', 'collections.payment.approve', 'collections.report.view'],
  },
  CollectionsOperationsManager: {
    ar: 'مدير عمليات التحصيل',
    en: 'Collections operations manager',
    summaryAr: 'تشغيل التحصيل بالكامل بما فيه الاعتماد، الاستيراد، والإعدادات.',
    summaryEn: 'Full collections operations including approvals, imports, and configuration.',
    packs: ['collections.access', 'collections.dashboard.view', 'collections.case.view', 'collections.case.view_sensitive', 'collections.activity.manage', 'collections.assignment.manage', 'collections.ptp.manage', 'collections.payment.submit', 'collections.payment.approve', 'collections.visit.manage', 'collections.complaint.manage', 'collections.import.manage', 'collections.report.view', 'collections.report.export', 'collections.configuration.manage', 'collections.audit.view', 'collections.data_batch.review'],
  },
  CollectionsAuditor: {
    ar: 'مدقق تحصيل',
    en: 'Collections auditor',
    summaryAr: 'قراءة الحالات والتقارير وسجل التدقيق دون تعديل تشغيلي.',
    summaryEn: 'Read cases, reports, and audit history without operational changes.',
    packs: ['collections.access', 'collections.dashboard.view', 'collections.case.view', 'collections.report.view', 'collections.audit.view'],
  },
  CollectionsClientViewer: {
    ar: 'عرض عميل/بنك',
    en: 'Client viewer',
    summaryAr: 'عرض مؤشرات وحالات وتقارير البنوك المسموحة فقط.',
    summaryEn: 'View KPIs, cases, and reports for allowed banks only.',
    packs: ['collections.access', 'collections.dashboard.view', 'collections.case.view', 'collections.report.view'],
  },
  DataEntry: {
    ar: 'إدخال بيانات',
    en: 'Data entry',
    summaryAr: 'فتح إدخال البيانات وإنشاء العملاء ورفع الدفعات.',
    summaryEn: 'Open data entry, create clients, and upload batches.',
    packs: ['data_entry.access', 'data_entry.manage'],
  },
  LegalOfficer: {
    ar: 'مسؤول شؤون قانونية',
    en: 'Legal officer',
    summaryAr: 'يفتح طابور القضايا المحوّلة من التحصيل ويسجّل الإجراءات والأحكام والتسويات والإعادة.',
    summaryEn: 'Opens the legal queue from collections and records actions, judgments, settlements, and returns.',
    packs: ['legal.access', 'legal.case.manage'],
  },
};

export const DEPARTMENT_ROLE_HINT: Record<string, string> = {
  HR: 'HrOfficer',
  COLLECTIONS: 'CollectionsCollector',
  DATA_ENTRY: 'DataEntry',
  LEGAL: 'LegalOfficer',
};

export function roleLabel(name: string, language: 'ar' | 'en') {
  const guide = ROLE_GUIDE[name];
  return guide ? guide[language] : name;
}

export function roleSummary(name: string, language: 'ar' | 'en') {
  const guide = ROLE_GUIDE[name];
  if (!guide) return '';
  return language === 'ar' ? guide.summaryAr : guide.summaryEn;
}

export function impliedPermissionCodes(roleNames: string[]) {
  const codes = new Set<string>();
  for (const name of roleNames) {
    const packs = ROLE_GUIDE[name]?.packs ?? [];
    for (const code of packs) codes.add(code);
  }
  return codes;
}

export function suggestUsername(fullName: string) {
  return fullName
    .trim()
    .toLowerCase()
    .replace(/\s+/g, '.')
    .replace(/[^a-z0-9._-]/g, '')
    .slice(0, 100);
}
