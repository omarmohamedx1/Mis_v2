using MIS.Application.DTOs.Admin;

namespace MIS.Infrastructure.Services;

internal static class AdminPermissionCatalog
{
    private static readonly string[] DepartmentScopes = ["OWN", "DEPARTMENT"];
    private static readonly string[] CollectionScopes = ["OWN", "TEAM", "CLIENT", "ALL"];
    private static readonly string[] FinanceClientScopes = ["CLIENT", "DEPARTMENT", "ALL"];
    private static readonly string[] GlobalScopes = ["ALL"];

    public static readonly IReadOnlyCollection<AdminPermissionDefinitionDto> All = new[]
    {
        P("admin.dashboard.view","ADMIN","لوحة الإدارة","Admin dashboard","متابعة مؤشرات المستخدمين والمخاطر.","View user and access risk indicators.","MEDIUM",GlobalScopes),
        P("admin.users.manage","ADMIN","إدارة المستخدمين","Manage users","إنشاء وتفعيل وإيقاف الحسابات.","Create, activate, and suspend accounts.","HIGH",GlobalScopes),
        P("admin.access.manage","ADMIN","إدارة الصلاحيات","Manage access","منح وسحب صلاحيات النظام.","Grant and revoke system permissions.","CRITICAL",GlobalScopes),
        P("admin.audit.view","ADMIN","سجل الإدارة","Administration audit","قراءة سجل تغييرات الحسابات والصلاحيات.","View account and access change history.","HIGH",GlobalScopes),
        P("hr.access","HR","دخول الموارد البشرية","HR access","فتح موديول الموارد البشرية.","Open the HR module.","LOW",DepartmentScopes),
        P("hr.employee.view","HR","عرض الموظفين","View employees","عرض ملفات الموظفين الأساسية.","View basic employee profiles.","MEDIUM",DepartmentScopes),
        P("hr.employee.manage","HR","إدارة الموظفين","Manage employees","إضافة وتعديل بيانات الموظفين.","Create and update employee records.","HIGH",DepartmentScopes),
        P("hr.sensitive.view","HR","بيانات HR الحساسة","Sensitive HR data","عرض التعويضات والمستندات الحساسة.","View compensation and sensitive documents.","CRITICAL",DepartmentScopes),
        P("hr.attendance.manage","HR","الحضور والانصراف","Attendance management","استيراد وتصحيح سجلات الحضور.","Import and correct attendance records.","HIGH",DepartmentScopes),
        P("hr.leave.approve","HR","اعتماد الإجازات","Approve leave","اتخاذ قرار على طلبات الإجازات.","Approve or reject leave requests.","HIGH",DepartmentScopes),
        P("hr.report.export","HR","تصدير تقارير HR","Export HR reports","تصدير بيانات الموظفين والتقارير.","Export employee and HR reports.","HIGH",DepartmentScopes),
        P("accounting.access","ACCOUNTING","دخول الحسابات","Accounting access","فتح موديول الحسابات عند إطلاقه.","Open the accounting module when released.","LOW",DepartmentScopes),
        P("accounting.transaction.manage","ACCOUNTING","إدارة القيود","Manage transactions","إنشاء وتعديل العمليات المالية.","Create and update financial transactions.","CRITICAL",DepartmentScopes),
        P("accounting.approve","ACCOUNTING","اعتماد مالي","Financial approval","اعتماد العمليات المالية الحساسة.","Approve sensitive financial operations.","CRITICAL",DepartmentScopes),
        P("accounting.report.export","ACCOUNTING","تصدير التقارير المالية","Export finance reports","تصدير بيانات وتقارير مالية.","Export financial data and reports.","CRITICAL",DepartmentScopes),
        P("finance.access","FINANCE","دخول المالية","Finance access","فتح مركز القيادة المالية والدفاتر.","Open the finance command center and ledgers.","LOW",DepartmentScopes),
        P("finance.journal.manual.create","FINANCE","إنشاء قيد يدوي","Create manual journal","إنشاء وإرسال القيود اليدوية للمراجعة.","Create and submit manual journals.","HIGH",DepartmentScopes),
        P("finance.journal.approve","FINANCE","اعتماد القيود","Approve journals","اعتماد القيود مع فصل المنشئ عن المعتمد.","Approve journals with maker-checker separation.","CRITICAL",DepartmentScopes),
        P("finance.journal.post","FINANCE","ترحيل القيود","Post journals","ترحيل القيود المعتمدة إلى دفتر الأستاذ.","Post approved journals to the general ledger.","CRITICAL",DepartmentScopes),
        P("finance.transaction.reverse","FINANCE","عكس عملية مالية","Reverse finance transaction","إنشاء قيد عكسي مرتبط بالعملية الأصلية.","Create a linked reversal for a posted transaction.","CRITICAL",DepartmentScopes),
        P("finance.period.close","FINANCE","إقفال الفترات","Close accounting periods","الإقفال المرحلي والنهائي وإعادة الفتح المراقبة.","Soft-close, close, and controlled reopen.","CRITICAL",DepartmentScopes),
        P("finance.configuration.manage","FINANCE","إعدادات المالية","Finance configuration","تهيئة السنوات والفترات والإعدادات المحاسبية.","Configure fiscal years and finance setup.","CRITICAL",DepartmentScopes),
        P("finance.report.view","FINANCE","عرض التقارير المالية","View finance reports","عرض ميزان المراجعة وتقارير دفتر الأستاذ.","View trial balance and general-ledger reports.","HIGH",DepartmentScopes),
        P("finance.collection.review","FINANCE","مراجعة التحصيلات المالية","Review financial collections","عرض تفاصيل الإيصال وتوزيعاته وسلسلة القيود.","Review receipts, allocations, and their accounting trace.","HIGH",FinanceClientScopes),
        P("finance.custody.view","FINANCE","عرض عهد المحصلين","View collector custody","عرض أرصدة وحركات عهد المحصلين.","View collector custody balances and movements.","HIGH",DepartmentScopes),
        P("finance.custody.reconcile","FINANCE","تسوية عهد المحصلين","Reconcile collector custody","توريد وتسوية العهد وربطها بقيود دفتر الأستاذ.","Hand over and reconcile custody with general-ledger journals.","CRITICAL",DepartmentScopes),
        P("finance.audit.view","FINANCE","سجل تدقيق المالية","Finance audit","قراءة أثر إنشاء واعتماد وترحيل وعكس القيود.","View journal creation, approval, posting, and reversal audit.","HIGH",DepartmentScopes),
        P("legal.access","LEGAL","دخول الشؤون القانونية","Legal access","فتح موديول الشؤون القانونية عند إطلاقه.","Open the legal module when released.","LOW",DepartmentScopes),
        P("legal.case.manage","LEGAL","إدارة القضايا","Manage legal cases","إضافة وتعديل الإجراءات القانونية.","Create and update legal case actions.","HIGH",DepartmentScopes),
        P("data_entry.access","DATA_ENTRY","دخول إدخال البيانات","Data entry access","فتح موديول إدخال البيانات عند إطلاقه.","Open data entry when released.","LOW",DepartmentScopes),
        P("data_entry.import","DATA_ENTRY","استيراد البيانات","Import data","رفع ومعالجة ملفات البيانات.","Upload and process data files.","HIGH",DepartmentScopes),
        P("collections.access","COLLECTIONS","دخول التحصيل","Collections access","فتح مركز عمليات التحصيل.","Open the collections command center.","LOW",CollectionScopes),
        P("collections.dashboard.view","COLLECTIONS","مؤشرات التحصيل","Collections dashboard","عرض مؤشرات الأداء ضمن النطاق.","View scoped collection KPIs.","MEDIUM",CollectionScopes),
        P("collections.case.view","COLLECTIONS","عرض الحالات","View cases","عرض حالات التحصيل ضمن النطاق.","View collection cases in scope.","MEDIUM",CollectionScopes),
        P("collections.case.view_sensitive","COLLECTIONS","كشف بيانات العميل","Reveal customer data","عرض الهاتف والرقم القومي بدون إخفاء.","Reveal unmasked phone and national ID.","CRITICAL",CollectionScopes),
        P("collections.activity.manage","COLLECTIONS","تسجيل أنشطة التحصيل","Manage activities","تسجيل المكالمات والمتابعات.","Record calls and follow-ups.","MEDIUM",CollectionScopes),
        P("collections.assignment.manage","COLLECTIONS","توزيع الحالات","Manage assignments","توزيع وإعادة توزيع الحالات.","Assign and reassign cases.","HIGH",CollectionScopes),
        P("collections.ptp.manage","COLLECTIONS","إدارة وعود السداد","Manage PTP","إنشاء ومتابعة وعود السداد.","Create and manage promises to pay.","HIGH",CollectionScopes),
        P("collections.payment.submit","COLLECTIONS","تسجيل تحصيل","Submit collection","تسجيل عملية تحصيل للمراجعة.","Submit collection transactions for review.","HIGH",CollectionScopes),
        P("collections.payment.approve","COLLECTIONS","اعتماد التحصيل","Approve collection","اعتماد أو رفض عمليات التحصيل.","Approve or reject collection transactions.","CRITICAL",CollectionScopes),
        P("collections.visit.manage","COLLECTIONS","إدارة الزيارات","Manage visits","تخطيط وتحديث الزيارات الميدانية.","Plan and update field visits.","HIGH",CollectionScopes),
        P("collections.complaint.manage","COLLECTIONS","إدارة الشكاوى","Manage complaints","معالجة شكاوى العملاء وSLA.","Manage customer complaints and SLA.","HIGH",CollectionScopes),
        P("collections.import.manage","COLLECTIONS","استيراد المحافظ","Import portfolios","رفع واعتماد ملفات المحافظ.","Upload and confirm portfolio imports.","CRITICAL",CollectionScopes),
        P("collections.report.view","COLLECTIONS","عرض تقارير التحصيل","View reports","عرض التقارير ضمن النطاق.","View scoped collections reports.","MEDIUM",CollectionScopes),
        P("collections.report.export","COLLECTIONS","تصدير تقارير التحصيل","Export reports","تصدير بيانات التحصيل ضمن النطاق.","Export scoped collections data.","CRITICAL",CollectionScopes),
        P("collections.configuration.manage","COLLECTIONS","إعدادات التحصيل","Collections configuration","تعديل إعدادات العملاء وقواعد التشغيل.","Change client and workflow configuration.","CRITICAL",CollectionScopes),
        P("collections.audit.view","COLLECTIONS","سجل تدقيق التحصيل","Collections audit","عرض أثر العمليات والتغييرات.","View collections audit trails.","HIGH",CollectionScopes),
    };

    public static readonly IReadOnlyDictionary<string, AdminPermissionDefinitionDto> ByCode = All.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
    public static bool IsPrivileged(string code) => ByCode.TryGetValue(code, out var item) && item.RiskLevel is "HIGH" or "CRITICAL";
    private static AdminPermissionDefinitionDto P(string code, string group, string ar, string en, string dar, string den, string risk, IReadOnlyCollection<string> scopes)
        => new(code, group, ar, en, dar, den, risk, scopes);
}
