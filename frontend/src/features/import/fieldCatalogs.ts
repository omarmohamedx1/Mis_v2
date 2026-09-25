import type { ImportFieldDef } from './excelAutoMap';

export const employeeImportCatalog: ImportFieldDef[] = [
  { key: 'EmployeeNumber', en: 'Employee Number', ar: 'رقم الموظف', required: true, aliases: ['code', 'employee code', 'employee number', 'employee no', 'emp no', 'emp number', 'emp code', 'رقم الموظف', 'رقم المظف', 'كود الموظف'] },
  { key: 'FullNameArabic', en: 'Employee Name Arabic', ar: 'اسم الموظف بالعربية', aliases: ['name in arabic', 'arabic name', 'arabic full name', 'employee name arabic', 'اسم الموظف بالعربية', 'الاسم بالعربي', 'الاسم العربي', 'اسم الموظف', 'اسم المظف', 'الاسم'] },
  { key: 'FullNameEnglish', en: 'Employee Name English', ar: 'اسم الموظف بالإنجليزية', aliases: ['name in english', 'english name', 'english full name', 'employee name english', 'employee name', 'full name', 'اسم الموظف بالانجليزي', 'الاسم بالانجليزي', 'الاسم الإنجليزي'] },
  { key: 'NationalId', en: 'National ID', ar: 'الرقم القومي', required: true, aliases: ['card number', 'national id', 'national id number', 'id number', 'nid', 'الرقم القومي', 'رقم البطاقة', 'رقم قومي'] },
  { key: 'Position', en: 'Position / Job Title', ar: 'المسمى الوظيفي', required: true, aliases: ['position / job title', 'position job title', 'title', 'position', 'job title', 'job', 'المسمى الوظيفي', 'الوظيفة'] },
  { key: 'WorkStartDate', en: 'Employment Date', ar: 'تاريخ التعيين', required: true, aliases: ['date of employment', 'date of empoloyment', 'employment date', 'empoloyment date', 'hire date', 'start date', 'work start date', 'joining date', 'تاريخ التعيين'] },
  { key: 'Department', en: 'Department', ar: 'القسم', aliases: ['department', 'dept', 'القسم'] },
  { key: 'Organization', en: 'Assigned Bank / Company', ar: 'البنك / الشركة المكلّف بها', aliases: ['assigned bank / company', 'assigned bank company', 'assigned bank', 'assigned company', 'bank', 'company', 'client', 'organization', 'البنك / الشركة المكلف بها', 'البنك / الشركة', 'البنك', 'الشركة', 'الجهة'] },
  { key: 'WorkNumber', en: 'Work Number', ar: 'رقم الشغل', aliases: ['work number', 'job number', 'work no', 'worknumber', 'رقم الشغل', 'رقم العمل'] },
  { key: 'PackageType', en: 'Package Type', ar: 'نوع الباقة', aliases: ['package type', 'packagetype', 'package', 'bundle', 'نوع الباقة', 'نوع الباقه', 'الباقة', 'الباقه'] },
  { key: 'OperationalRole', en: 'Employee Role', ar: 'الدور الوظيفي', aliases: ['role', 'employee role', 'employee type', 'operational role', 'الدور الوظيفي', 'الدور'] },
  { key: 'MobileNumber', en: 'Mobile Number', ar: 'رقم الموبايل', aliases: ['mobile number', 'mobile', 'phone number', 'phone', 'telephone', 'رقم الموبايل', 'رقم الهاتف', 'التليفون', 'موبايل'] },
  { key: 'Gender', en: 'Gender', ar: 'النوع', aliases: ['male female', 'male - female', 'gender', 'sex', 'النوع'] },
  { key: 'DateOfBirth', en: 'Date of Birth', ar: 'تاريخ الميلاد', aliases: ['birth of day', 'date of birth', 'birth date', 'dob', 'تاريخ الميلاد'] },
  { key: 'FingerprintEnrollmentDate', en: 'Fingerprint Date', ar: 'تاريخ البصمة', aliases: ['fingerprint date', 'fingerprint', 'تاريخ البصمة'] },
  { key: 'WorkEndDate', en: 'End Work Date', ar: 'تاريخ انتهاء العمل', aliases: ['date out of work employer', 'end work date', 'termination date', 'work end date', 'تاريخ انتهاء العمل'] },
  { key: 'Address', en: 'Address', ar: 'العنوان', aliases: ['address', 'العنوان'] },
  { key: 'Status', en: 'Status', ar: 'الحالة', aliases: ['status', 'الحالة'] },
  { key: 'BasicSalary', en: 'Basic Salary', ar: 'الراتب الأساسي', aliases: ['basic salary', 'salary', 'basic pay', 'gross salary', 'الراتب الأساسي', 'المرتب', 'الراتب'] },
  { key: 'Allowances', en: 'Allowances', ar: 'البدلات', aliases: ['allowances', 'allowance', 'بدلات', 'البدلات', 'البدل'] },
];

export const absenceImportCatalog: ImportFieldDef[] = [
  { key: 'EmployeeNumber', en: 'Employee Number', ar: 'رقم الموظف', aliases: ['code', 'employee code', 'employee no', 'employee number', 'رقم الموظف'] },
  { key: 'EmployeeName', en: 'Employee Name', ar: 'اسم الموظف', aliases: ['name', 'employee name', 'اسم الموظف'] },
  { key: 'NationalId', en: 'National ID', ar: 'الرقم القومي', aliases: ['national id', 'id number', 'الرقم القومي'] },
  { key: 'MobileNumber', en: 'Mobile Number', ar: 'رقم الموبايل', aliases: ['mobile', 'phone', 'mobile number', 'phone number', 'telephone', 'رقم الموبايل', 'رقم الهاتف', 'التليفون'] },
  { key: 'AbsenceDate', en: 'Absence Date', ar: 'تاريخ الغياب', required: true, aliases: ['date', 'absence date', 'غياب', 'تاريخ الغياب'] },
  { key: 'AbsenceType', en: 'Absence Type', ar: 'نوع الغياب', aliases: ['type', 'absence type', 'نوع الغياب'] },
  { key: 'Reason', en: 'Reason', ar: 'السبب', aliases: ['reason', 'السبب'] },
  { key: 'Notes', en: 'Notes', ar: 'ملاحظات', aliases: ['notes', 'ملاحظات'] },
  { key: 'Status', en: 'Status', ar: 'الحالة', aliases: ['status', 'حالة', 'الحالة'] },
];

export const socialInsuranceImportCatalog: ImportFieldDef[] = [
  { key: 'EmployeeNumber', en: 'Employee Number', ar: 'رقم الموظف', aliases: ['code', 'employee code', 'employee no', 'employee number', 'رقم الموظف'] },
  { key: 'EmployeeName', en: 'Employee Name', ar: 'اسم الموظف', aliases: ['name', 'employee name', 'اسم الموظف'] },
  { key: 'NationalId', en: 'National ID', ar: 'الرقم القومي', aliases: ['national id', 'id number', 'الرقم القومي'] },
  { key: 'EmployeeId', en: 'Employee ID', ar: 'معرف الموظف بالنظام', aliases: ['employee id', 'employee uuid'] },
  { key: 'SocialInsuranceNumber', en: 'Social Insurance Number', ar: 'الرقم التأميني', required: true, aliases: ['insurance no', 'social insurance no', 'insurance number', 'social insurance number', 'الرقم التأميني'] },
  { key: 'InsuranceStartDate', en: 'Insurance Start Date', ar: 'تاريخ بداية التأمين', aliases: ['start date', 'insurance start', 'insurance start date', 'تاريخ بداية التأمين'] },
  { key: 'InsuranceEndDate', en: 'Insurance End Date', ar: 'تاريخ نهاية التأمين', aliases: ['end date', 'insurance end', 'insurance end date', 'تاريخ نهاية التأمين'] },
  { key: 'InsurableSalary', en: 'Insurable Salary', ar: 'الأجر التأميني', required: true, aliases: ['insurance salary', 'insurable salary', 'الأجر التأميني'] },
  { key: 'InsuranceStatus', en: 'Insurance Status', ar: 'حالة التأمين', required: true, aliases: ['status', 'insurance status', 'حالة التأمين'] },
  { key: 'InsuranceOffice', en: 'Insurance Office', ar: 'مكتب التأمينات', aliases: ['office', 'insurance office', 'مكتب التأمينات'] },
  { key: 'ReferenceNumber', en: 'Reference Number', ar: 'رقم الاستمارة / المرجع', aliases: ['reference', 'reference number', 'form number', 'رقم الاستمارة', 'المرجع'] },
  { key: 'Notes', en: 'Notes', ar: 'ملاحظات', aliases: ['notes', 'ملاحظات'] },
];

export const dataEntryImportCatalog: ImportFieldDef[] = [
  { key: 'CustomerCode', en: 'Customer number', ar: 'رقم العميل', aliases: ['customer code', 'customer id', 'customer number', 'كود العميل', 'رقم العميل'] },
  { key: 'CustomerName', en: 'Customer name', ar: 'اسم العميل', required: true, aliases: ['name', 'customer name', 'client name', 'اسم العميل', 'الاسم'] },
  { key: 'NationalId', en: 'National ID', ar: 'الرقم القومي', aliases: ['national id', 'id number', 'nid', 'الرقم القومي'] },
  { key: 'MobileNumber', en: 'Mobile number', ar: 'رقم الموبايل', aliases: ['mobile', 'phone', 'phone number', 'رقم الموبايل', 'رقم الهاتف'] },
  { key: 'Address', en: 'Address', ar: 'العنوان', aliases: ['address', 'العنوان'] },
  { key: 'Feedback', en: 'Feedback', ar: 'فيدباك', aliases: ['feedback', 'فيدباك', 'تعليق'] },
  { key: 'Notes', en: 'Notes', ar: 'ملاحظات', aliases: ['notes', 'ملاحظات', 'ملاحظة'] },
  { key: 'AccountNumber', en: 'Account number', ar: 'رقم الحساب', required: true, aliases: ['account', 'account no', 'account number', 'رقم الحساب'] },
  { key: 'ContractNumber', en: 'Contract number', ar: 'رقم العقد', aliases: ['contract', 'contract no', 'contract number', 'رقم العقد'] },
  { key: 'OutstandingAmount', en: 'Outstanding amount', ar: 'المديونية', aliases: ['outstanding', 'outstanding amount', 'المديونية', 'المبلغ المستحق'] },
  { key: 'OverdueAmount', en: 'Overdue amount', ar: 'المتأخر', aliases: ['overdue', 'overdue amount', 'المتأخر'] },
  { key: 'DaysPastDue', en: 'Days past due', ar: 'أيام التأخر', aliases: ['dpd', 'days past due', 'أيام التأخر'] },
];

export const bankCustomerImportCatalog: ImportFieldDef[] = [
  { key: 'CustomerCode', en: 'Customer Code', ar: 'كود العميل', aliases: ['customer code', 'customer id', 'كود العميل'] },
  { key: 'CustomerName', en: 'Customer Name', ar: 'اسم العميل', required: true, aliases: ['name', 'customer name', 'client name', 'اسم العميل'] },
  { key: 'MobileNumber', en: 'Mobile Number', ar: 'رقم الموبايل', aliases: ['mobile', 'phone', 'phone number', 'رقم الموبايل', 'رقم الهاتف'] },
  { key: 'NationalId', en: 'National ID', ar: 'الرقم القومي', aliases: ['national id', 'id number', 'الرقم القومي'] },
  { key: 'Address', en: 'Address', ar: 'العنوان', aliases: ['address', 'العنوان'] },
  { key: 'AccountNumber', en: 'Account Number', ar: 'رقم الحساب', aliases: ['account', 'account no', 'account number', 'رقم الحساب'] },
  { key: 'ContractNumber', en: 'Contract Number', ar: 'رقم العقد', aliases: ['contract', 'contract no', 'contract number', 'رقم العقد'] },
  { key: 'ProductType', en: 'Product Type', ar: 'نوع المنتج', aliases: ['product', 'product type', 'نوع المنتج'] },
  { key: 'OutstandingAmount', en: 'Outstanding Amount', ar: 'المديونية', aliases: ['outstanding', 'outstanding amount', 'المديونية', 'المبلغ المستحق'] },
  { key: 'PaidAmount', en: 'Paid Amount', ar: 'المسدد', aliases: ['paid', 'paid amount', 'المسدد'] },
  { key: 'RemainingAmount', en: 'Remaining Amount', ar: 'المتبقي', aliases: ['balance', 'remaining', 'remaining amount', 'المتبقي'] },
  { key: 'OverdueAmount', en: 'Overdue Amount', ar: 'المتأخر', aliases: ['overdue', 'overdue amount', 'المتأخر'] },
  { key: 'DaysPastDue', en: 'Days Past Due', ar: 'أيام التأخر', aliases: ['dpd', 'days past due', 'أيام التأخر'] },
];

export const bankDistributionImportCatalog: ImportFieldDef[] = [
  { key: 'CaseNumber', en: 'Case Number', ar: 'رقم الحالة', aliases: ['case number', 'case id', 'رقم الحالة'] },
  { key: 'AccountReference', en: 'Account Reference', ar: 'رقم الحساب', aliases: ['account', 'account no', 'account reference', 'account number', 'رقم الحساب'] },
  { key: 'ContractNumber', en: 'Contract Number', ar: 'رقم العقد', aliases: ['contract', 'contract no', 'contract number', 'رقم العقد'] },
  { key: 'CustomerCode', en: 'Customer Code', ar: 'كود العميل', aliases: ['customer code', 'customer id', 'كود العميل'] },
  { key: 'CustomerName', en: 'Customer Name', ar: 'اسم العميل', aliases: ['customer name', 'name', 'اسم العميل'] },
  { key: 'CollectorEmployeeNumber', en: 'Collector Employee Number', ar: 'رقم الموظف', aliases: ['employee number', 'collector id', 'collector employee', 'رقم الموظف'] },
  { key: 'CollectorUsername', en: 'Collector Username', ar: 'اسم المستخدم', aliases: ['username', 'user name', 'اسم المستخدم'] },
  { key: 'CollectorEmail', en: 'Collector Email', ar: 'البريد', aliases: ['email', 'collector email', 'البريد'] },
  { key: 'CollectorName', en: 'Collector Name', ar: 'اسم المحصل', aliases: ['collector name', 'collector', 'اسم المحصل', 'المحصل'] },
];

export const leaveImportCatalog: ImportFieldDef[] = [
  { key: 'EmployeeNumber', en: 'Employee Number', ar: 'رقم الموظف', required: true, aliases: ['employee number', 'employee no', 'emp no', 'رقم الموظف'] },
  { key: 'LeaveType', en: 'Leave Type', ar: 'نوع الإجازة', required: true, aliases: ['leave type', 'type', 'نوع الإجازة'] },
  { key: 'StartDate', en: 'Start Date', ar: 'تاريخ البداية', required: true, aliases: ['start date', 'from', 'تاريخ البداية', 'من'] },
  { key: 'EndDate', en: 'End Date', ar: 'تاريخ النهاية', required: true, aliases: ['end date', 'to', 'تاريخ النهاية', 'إلى'] },
  { key: 'Reason', en: 'Reason', ar: 'السبب', aliases: ['reason', 'السبب'] },
  { key: 'Status', en: 'Status', ar: 'الحالة', aliases: ['status', 'الحالة'] },
];
