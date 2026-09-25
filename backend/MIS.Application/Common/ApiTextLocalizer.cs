using System.Globalization;
using System.Text.RegularExpressions;

namespace MIS.Application.Common;

/// <summary>
/// Localizes API text at the HTTP boundary while keeping domain values and API codes stable.
/// English remains the canonical language stored in audit/import history; Arabic is selected
/// per request through <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
public static partial class ApiTextLocalizer
{
    private const string GenericArabicError = "تعذر إكمال الطلب. يُرجى مراجعة البيانات والمحاولة مرة أخرى.";

    private static readonly IReadOnlyDictionary<string, string> Arabic =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HrExcuseMission"] = "الأعذار والمأموريات",
            ["ExcuseRequestCreated"] = "إنشاء طلب عذر",
            ["VisitRequestSubmitted"] = "إرسال طلب اعتماد زيارة",
            ["VisitExcuseUpdated"] = "تحديث عذر مرتبط بزيارة",
            ["ExcuseApproved"] = "اعتماد عذر",
            ["ExcuseRejected"] = "رفض عذر",
            ["ExcuseCancelled"] = "إلغاء عذر",
            ["ExcuseEdited"] = "تعديل عذر",
            ["ExcuseAttachmentUploaded"] = "رفع مرفق عذر",
            ["ExcuseAttachmentReplaced"] = "استبدال مرفق عذر",
            ["ExcuseAttachmentDeleted"] = "حذف مرفق عذر",
            ["CollectorEmployeeLinked"] = "ربط المحصل بموظف موجود",
            ["Link the collector to an existing HR employee before approval."] = "يجب ربط المحصل بموظف موجود قبل الموافقة.",
            ["Only pending requests can be reviewed."] = "يمكن مراجعة الطلبات التي تنتظر الموافقة فقط.",
            ["Request changed. Refresh before reviewing."] = "تم تغيير الطلب. حدّث الصفحة قبل المراجعة.",
            ["Visit changed. Refresh before reviewing."] = "تم تغيير الزيارة. حدّث الصفحة قبل المراجعة.",
            ["End time must be after start time on the same day."] = "يجب أن يكون وقت النهاية بعد وقت البداية في اليوم نفسه.",
            ["Excuse access is required."] = "يلزم تصريح عرض الأعذار.",
            ["Excuse management permission is required."] = "يلزم تصريح إدارة الأعذار.",
            ["Excuse approval permission is required."] = "يلزم تصريح اعتماد الأعذار.",
            ["InsuranceRecordCreated"] = "تم إنشاء سجل تأمين اجتماعي",
            ["InsuranceRecordEdited"] = "تم تعديل سجل تأمين اجتماعي",
            ["InsuranceEnded"] = "تم إنهاء التأمين الاجتماعي",
            ["SocialInsuranceRecord"] = "سجل التأمينات الاجتماعية",
            ["Ended insurance records cannot be edited."] = "لا يمكن تعديل سجلات التأمين المنتهية.",
            ["Invalid insurance status."] = "حالة التأمين غير صحيحة.",
            ["Social insurance number is required and must not exceed 50 characters."] = "الرقم التأميني مطلوب ويجب ألا يتجاوز ٥٠ حرفًا.",
            ["Insurance start date is required."] = "تاريخ بداية التأمين مطلوب.",
            ["Insurable salary must be positive with at most two decimal places."] = "يجب أن يكون الأجر التأميني موجبًا وبحد أقصى منزلتين عشريتين.",
            ["Insurance text exceeds the maximum length."] = "تجاوز النص الحد الأقصى المسموح به.",
            ["Insurance has already ended."] = "تم إنهاء التأمين بالفعل.",
            ["Insurance end date is required and cannot precede its start date."] = "تاريخ نهاية التأمين مطلوب ولا يمكن أن يسبق تاريخ البداية.",
            ["Social insurance access is required."] = "يلزم الحصول على صلاحية عرض التأمينات الاجتماعية.",
            ["Social insurance management permission is required."] = "يلزم الحصول على صلاحية إدارة التأمينات الاجتماعية.",
            ["Insurance record not found."] = "سجل التأمين غير موجود.",
            ["The insurance employee cannot be changed."] = "لا يمكن تغيير الموظف المرتبط بسجل التأمين.",
            ["Insurance changed. Refresh and try again."] = "تم تغيير سجل التأمين. حدّث الصفحة وحاول مجددًا.",
            ["An active insurance record already exists for this employee or insurance number."] = "يوجد بالفعل سجل تأمين قائم لهذا الموظف أو لهذا الرقم التأميني.",
            ["A confirmed attendance import cannot be cancelled."] = "لا يمكن إلغاء عملية استيراد حضور تم تأكيدها.",
            ["A confirmed or cancelled import cannot be previewed again."] = "لا يمكن إعادة معاينة عملية استيراد مؤكدة أو ملغاة.",
            ["A document file is required."] = "ملف المستند مطلوب.",
            ["A failed import must be uploaded again."] = "يجب رفع ملف الاستيراد الفاشل مرة أخرى.",
            ["A file name is required."] = "اسم الملف مطلوب.",
            ["A future attendance day cannot be processed."] = "لا يمكن معالجة يوم حضور في المستقبل.",
            ["A leave period cannot exceed two years."] = "لا يمكن أن تتجاوز مدة الإجازة سنتين.",
            ["A master-data record with this code already exists."] = "يوجد بالفعل سجل بيانات أساسية بهذا الكود.",
            ["A rejection or cancellation reason is required."] = "سبب الرفض أو الإلغاء مطلوب.",
            ["A replacement contract cannot start before the current contract version."] = "لا يمكن أن يبدأ العقد البديل قبل نسخة العقد الحالية.",
            ["A replacement file is required."] = "الملف البديل مطلوب.",
            ["This portfolio has collection payments and cannot be deleted. Archive it instead."] = "هذه المحفظة عليها تحصيلات ولا يمكن حذفها. استخدم الأرشفة بدلاً من ذلك.",
            ["A storage key is required."] = "مرجع التخزين مطلوب.",
            ["A valid absence date is required."] = "تاريخ غياب صحيح مطلوب.",
            ["A valid department is required."] = "يجب اختيار قسم صحيح.",
            ["Banks and companies are not HR departments."] = "البنوك والشركات ليست أقسام موارد بشرية. سجّلها على الموظف في البنك / الشركة المكلّف بها.",
            ["A valid employee is required."] = "يجب اختيار موظف صحيح.",
            ["Absence record was not found."] = "سجل الغياب غير موجود.",
            ["Absence status is invalid."] = "حالة الغياب غير صحيحة.",
            ["Absence type is invalid."] = "نوع الغياب غير صحيح.",
            ["Absence already exists / غياب مسجل بالفعل"] = "غياب مسجل بالفعل",
            ["Absence import was not found."] = "لم يتم العثور على استيراد الغياب.",
            ["Absence date is required. / تاريخ الغياب مطلوب"] = "تاريخ الغياب مطلوب.",
            ["An approved excuse or field duty already covers this date. / يوجد عذر أو مأمورية معتمدة في هذا التاريخ"] = "يوجد عذر أو مأمورية معتمدة في هذا التاريخ.",
            ["Build and review the absence preview before confirming."] = "أنشئ معاينة الغياب وراجعها قبل التأكيد.",
            ["Choose a CSV, XLS, or XLSX file no larger than 20 MB."] = "اختر ملف CSV أو XLS أو XLSX بحد أقصى 20 ميجابايت.",
            ["Choose a CSV, XLS, or XLSX file no larger than 50 MB."] = "اختر ملف CSV أو XLS أو XLSX بحد أقصى 50 ميجابايت.",
            ["Invalid absence date. / تاريخ الغياب غير صالح"] = "تاريخ الغياب غير صالح.",
            ["Map an employee identifier. / يجب تعيين معرف الموظف"] = "يجب تعيين معرف الموظف.",
            ["Map each source column only once. / لا يمكن تعيين العمود أكثر من مرة"] = "لا يمكن تعيين العمود أكثر من مرة.",
            ["Map the absence date column. / يجب تعيين عمود تاريخ الغياب"] = "يجب تعيين عمود تاريخ الغياب.",
            ["Multiple employees matched. / تطابق أكثر من موظف"] = "تطابق أكثر من موظف.",
            ["Multiple employees matched by name. / تطابق أكثر من موظف بالاسم"] = "تطابق أكثر من موظف بالاسم.",
            ["Select an absence import file."] = "اختر ملف استيراد الغياب.",
            ["This absence import is already complete."] = "تم إكمال استيراد الغياب بالفعل.",
            ["Unmatched Employee / موظف غير موجود"] = "موظف غير موجود.",
            ["Unsupported absence field mapping. / تعيين حقول غير صالح"] = "تعيين حقول غير صالح.",
            ["Approved deduction amount is required."] = "مبلغ الخصم المعتمد مطلوب.",
            ["Decision must be Approve or Exclude."] = "يجب أن يكون القرار اعتماد الخصم أو استبعاده.",
            ["Only an unexcused absence can affect payroll."] = "لا يؤثر على المرتب إلا الغياب بدون عذر.",
            ["Reverse the approved payroll deduction before changing the employee, date, or absence status."] = "يجب استبعاد الخصم المعتمد قبل تغيير الموظف أو تاريخ الغياب أو حالته.",
            ["An absence with an approved payroll deduction cannot be deleted. Exclude its payroll impact first."] = "لا يمكن حذف غياب له خصم راتب معتمد؛ استبعد تأثيره على المرتب أولًا.",
            ["Absent, leave, holiday, and weekend attendance cannot contain check-in or check-out punches."] = "لا يمكن لحالات الغياب أو الإجازة أو العطلة أو نهاية الأسبوع أن تحتوي على بصمات دخول أو خروج.",
            ["Allowed document formats are PDF, JPEG, PNG, and DOCX, and the file extension must match its content."] = "صيغ المستندات المسموحة هي PDF وJPEG وPNG وDOCX، ويجب أن يتطابق امتداد الملف مع محتواه.",
            ["An active employee is required."] = "يجب اختيار موظف نشط.",
            ["An active leave type is required."] = "يجب اختيار نوع إجازة نشط.",
            ["An attendance interval cannot exceed 48 hours."] = "لا يمكن أن تتجاوز مدة الحضور 48 ساعة.",
            ["An authenticated user is required."] = "يجب تسجيل الدخول أولًا.",
            ["An employee cannot be their own direct manager."] = "لا يمكن أن يكون الموظف مديرًا مباشرًا لنفسه.",
            ["An employee with this employee ID already exists."] = "يوجد بالفعل موظف بهذا الرقم الوظيفي.",
            ["An employee with this National ID already exists."] = "يوجد بالفعل موظف بهذا الرقم القومي.",
            ["An expiry date is required for this document type."] = "تاريخ الانتهاء مطلوب لهذا النوع من المستندات.",
            ["An unexpected error occurred."] = "حدث خطأ غير متوقع.",
            ["Another calendar exception already exists for this date."] = "يوجد بالفعل استثناء تقويم لهذا التاريخ.",
            ["Another employee already uses this national ID."] = "الرقم القومي مستخدم بالفعل لموظف آخر.",
            ["At least one of check-in or check-out must be mapped."] = "يجب ربط عمود واحد على الأقل للدخول أو الخروج.",
            ["At least one standard working day must be configured before adding a working-day override."] = "يجب إعداد يوم عمل قياسي واحد على الأقل قبل إضافة استثناء لساعات العمل.",
            ["Attendance already exists for this employee and date."] = "يوجد بالفعل سجل حضور لهذا الموظف في هذا التاريخ.",
            ["Attendance date is required."] = "تاريخ الحضور مطلوب.",
            ["Attendance date is missing or invalid."] = "تاريخ الحضور مفقود أو غير صحيح.",
            ["Attendance date is invalid."] = "تاريخ الحضور غير صحيح.",
            ["Check-in is invalid."] = "وقت الدخول غير صحيح.",
            ["Invalid check-in time."] = "وقت الحضور غير صالح.",
            ["Invalid check-out time."] = "وقت الانصراف غير صالح.",
            ["Import time system is invalid."] = "نظام الوقت غير صالح.",
            ["Employee validation failed."] = "فشل التحقق من بيانات الموظف.",
            ["Existing employee; skipped."] = "موظف موجود بالفعل؛ سيتم التجاوز.",
            ["Duplicate employee within this file; skipped."] = "موظف مكرر داخل الملف؛ سيتم التجاوز.",
            ["End work date is recorded; employee status is unchanged, as in manual creation."] = "سيُحفظ تاريخ انتهاء العمل دون تغيير الحالة، وفقًا لقواعد الإضافة اليدوية.",
            ["Gender: invalid value."] = "النوع: قيمة غير صالحة.",
            ["Status: invalid value."] = "الحالة: قيمة غير صالحة.",
            ["Department: must match one existing lookup value."] = "القسم: يجب مطابقته مع قيمة واحدة موجودة في البيانات الأساسية.",
            ["Department was inferred from the selected position."] = "تم استنتاج القسم تلقائيًا من المسمى الوظيفي المسجل؛ راجعه قبل تأكيد الاستيراد.",
            ["Department does not match the selected position."] = "القسم لا يتطابق مع القسم المرتبط بالمسمى الوظيفي في البيانات الأساسية.",
            ["Position: must match one existing lookup value."] = "المسمى الوظيفي: يجب مطابقته مع قيمة واحدة موجودة في البيانات الأساسية.",
            ["The EmployeeNumber field is required."] = "رقم الموظف مطلوب.",
            ["The FullName field is required."] = "اسم الموظف مطلوب.",
            ["The NationalId field is required."] = "الرقم القومي مطلوب.",
            ["Employee imports cannot exceed 2000 rows."] = "لا يمكن استيراد أكثر من 2000 صف في الملف.",
            ["Build and review the employee preview before confirming."] = "أنشئ معاينة الموظفين وراجعها قبل التأكيد.",
            ["No employee data rows were found."] = "لم يتم العثور على صفوف بيانات الموظفين.",
            ["WorkStartDate: invalid date."] = "تاريخ التعيين: تاريخ غير صالح.",
            ["WorkEndDate: invalid date."] = "تاريخ انتهاء العمل: تاريخ غير صالح.",
            ["FingerprintEnrollmentDate: invalid date."] = "تاريخ البصمة: تاريخ غير صالح.",
            ["DateOfBirth: invalid date."] = "تاريخ الميلاد: تاريخ غير صالح.",
            ["National ID must contain exactly 14 digits."] = "يجب أن يتكون الرقم القومي من 14 رقمًا بالضبط.",
            ["National ID must start with 2 or 3 and contain a valid birth date."] = "الرقم القومي غير صحيح: يجب أن يبدأ بـ 2 أو 3 وأن يحتوي على تاريخ ميلاد صالح.",
            ["Date of birth does not match the birth date encoded in the National ID."] = "تاريخ الميلاد لا يطابق تاريخ الميلاد المسجل داخل الرقم القومي.",
            ["Mobile number must contain exactly 11 digits and use a valid Egyptian mobile prefix."] = "رقم الموبايل يجب أن يكون 11 رقمًا ويبدأ بـ 010 أو 011 أو 012 أو 015.",
            ["The missing leading zero was restored in the mobile number."] = "تم استرجاع الصفر الأول الذي أسقطه Excel من رقم الموبايل؛ راجعه قبل التأكيد.",
            ["The mobile number was standardized to the Egyptian local format."] = "تم توحيد رقم الموبايل إلى الصيغة المصرية المحلية المكونة من 11 رقمًا.",
            ["Work start date cannot be before date of birth."] = "تاريخ التعيين لا يمكن أن يسبق تاريخ الميلاد.",
            ["Fingerprint enrollment date cannot be before date of birth."] = "تاريخ تسجيل البصمة لا يمكن أن يسبق تاريخ الميلاد.",
            ["AM/PM value is missing or invalid."] = "قيمة AM/PM مفقودة أو غير صالحة.",
            ["Attendance date mapping is required for separate punch times."] = "يجب تعيين عمود التاريخ لأوقات البصمات المنفصلة.",
            ["AM/PM mapping is required for separate 12-hour punch times."] = "يجب تعيين عمود AM/PM لأوقات البصمات المنفصلة بنظام 12 ساعة.",
            ["Check-out is invalid."] = "وقت الخروج غير صحيح.",
            ["Check-out is before check-in."] = "وقت الخروج يسبق وقت الدخول.",
            ["Punch date/time is missing."] = "تاريخ أو وقت البصمة مفقود.",
            ["Punch date/time is invalid."] = "تاريخ أو وقت البصمة غير صحيح.",
            ["Punch type is invalid."] = "نوع البصمة غير صحيح.",
            ["The file contains more than one check-in/check-out row for this employee and date."] = "يحتوي الملف على أكثر من صف دخول وخروج لهذا الموظف في التاريخ نفسه.",
            ["Attendance date mapping is required for check-in/check-out layout."] = "ربط عمود تاريخ الحضور مطلوب لتنسيق الدخول والخروج.",
            ["Attendance file content is unavailable."] = "محتوى ملف الحضور غير متاح.",
            ["Attendance file name is required."] = "اسم ملف الحضور مطلوب.",
            ["Attendance import batch was not found."] = "عملية استيراد الحضور غير موجودة.",
            ["Attendance import files cannot exceed 20 MB."] = "لا يمكن أن يتجاوز حجم ملف استيراد الحضور 20 ميجابايت.",
            ["Attendance import files cannot exceed 50 MB."] = "لا يمكن أن يتجاوز حجم ملف استيراد الحضور 50 ميجابايت.",
            ["Attendance import layout is invalid."] = "تنسيق استيراد الحضور غير صحيح.",
            ["Attendance import status is invalid."] = "حالة استيراد الحضور غير صحيحة.",
            ["Attendance preview category is invalid."] = "تصنيف معاينة الحضور غير صحيح.",
            ["Attendance record was not found."] = "سجل الحضور غير موجود.",
            ["Attendance sort field is invalid."] = "حقل ترتيب الحضور غير صحيح.",
            ["Attendance source is invalid."] = "مصدر الحضور غير صحيح.",
            ["Attendance source must be Manual for V1."] = "يجب أن يكون مصدر الحضور يدويًا في الإصدار الحالي.",
            ["Attendance status is invalid."] = "حالة الحضور غير صحيحة.",
            ["Audit entity identifiers must be valid GUID values."] = "معرّفات سجلات التدقيق غير صحيحة.",
            ["Authentication is required."] = "يجب تسجيل الدخول للوصول إلى هذا المورد.",
            ["Build the import preview first."] = "أنشئ معاينة الاستيراد أولًا.",
            ["Calendar exception type is invalid."] = "نوع استثناء التقويم غير صحيح.",
            ["Calendar exception was not found."] = "استثناء التقويم غير موجود.",
            ["Calendar override mode is invalid."] = "وضع استثناء التقويم غير صحيح.",
            ["Check-out cannot be before check-in."] = "لا يمكن أن يكون وقت الخروج قبل وقت الدخول.",
            ["Contract status is invalid."] = "حالة العقد غير صحيحة.",
            ["Contract status must be Draft, Active, Expired, or Terminated."] = "يجب أن تكون حالة العقد: مسودة أو نشط أو منتهي أو مُنهى.",
            ["Contract type and start date are required."] = "نوع العقد وتاريخ بدايته مطلوبان.",
            ["Custom working hours require start and end times."] = "ساعات العمل المخصصة تتطلب وقت بداية ووقت نهاية.",
            ["Data must start after the configured header row."] = "يجب أن تبدأ البيانات بعد صف العناوين المحدد.",
            ["Data start row is outside the supported range."] = "رقم صف بداية البيانات خارج النطاق المدعوم.",
            ["Date of birth must be a valid past date."] = "يجب أن يكون تاريخ الميلاد تاريخًا صحيحًا في الماضي.",
            ["Date to cannot be before date from."] = "لا يمكن أن يكون تاريخ النهاية قبل تاريخ البداية.",
            ["Default annual entitlement is required for leave types."] = "الرصيد السنوي الافتراضي مطلوب لأنواع الإجازات.",
            ["Delegation number already exists."] = "رقم التفويض موجود بالفعل.",
            ["Delegation status is invalid."] = "حالة التفويض غير صحيحة.",
            ["Delegation type is required."] = "نوع التفويض مطلوب.",
            ["Delegation was not found."] = "التفويض غير موجود.",
            ["Document expiry status is invalid."] = "حالة انتهاء المستند غير صحيحة.",
            ["Document type is required."] = "نوع المستند مطلوب.",
            ["Employee document was not found."] = "مستند الموظف غير موجود.",
            ["Employee ID mapping is required."] = "ربط عمود الرقم الوظيفي مطلوب.",
            ["Employee ID is missing."] = "الرقم الوظيفي مفقود.",
            ["Employee ID was not found."] = "لم يتم العثور على الرقم الوظيفي.",
            ["Employee ID matched more than one employee because of case-insensitive duplicates."] = "تطابق الرقم الوظيفي مع أكثر من موظف بسبب تكرار لا يفرق بين الحروف الكبيرة والصغيرة.",
            ["Employee is required."] = "الموظف مطلوب.",
            ["Employee status must be Active, Inactive, OnLeave, Suspended, or Terminated."] = "يجب أن تكون حالة الموظف: نشط أو غير نشط أو في إجازة أو موقوف أو منتهية خدمته.",
            ["Employee was not found."] = "الموظف غير موجود.",
            ["Only an active employee can be linked to a user account."] = "لا يمكن ربط حساب إلا بموظف نشط.",
            ["Office employees cannot be linked to operational system accounts."] = "لا يمكن ربط موظفي الأوفيس بحسابات تشغيلية على النظام.",
            ["This employee is already linked to another account."] = "هذا الموظف مربوط بالفعل بحساب آخر.",
            ["This user is already linked to a different employee."] = "هذا الحساب مربوط بالفعل بموظف آخر.",
            ["End date cannot be before start date."] = "لا يمكن أن يكون تاريخ النهاية قبل تاريخ البداية.",
            ["Expiry date cannot be before issue date."] = "لا يمكن أن يكون تاريخ الانتهاء قبل تاريخ الإصدار.",
            ["Expiry window must be between 1 and 365 days."] = "يجب أن تكون فترة قرب الانتهاء بين يوم واحد و365 يومًا.",
            ["Import time zone is required."] = "المنطقة الزمنية للاستيراد مطلوبة.",
            ["Invalid pagination values."] = "قيم ترقيم الصفحات غير صحيحة.",
            ["Invalid report pagination values."] = "قيم ترقيم صفحات التقرير غير صحيحة.",
            ["Invalid username or password."] = "اسم المستخدم أو كلمة المرور غير صحيحة.",
            ["Leave balance year is invalid."] = "سنة رصيد الإجازة غير صحيحة.",
            ["Leave decision status is invalid."] = "قرار طلب الإجازة غير صحيح.",
            ["Leave end date cannot be before start date."] = "لا يمكن أن يكون تاريخ انتهاء الإجازة قبل تاريخ بدايتها.",
            ["Leave entitlement values are outside the allowed range."] = "قيم استحقاق الإجازة خارج النطاق المسموح.",
            ["Leave request sort field is invalid."] = "حقل ترتيب طلبات الإجازة غير صحيح.",
            ["Leave request status is invalid."] = "حالة طلب الإجازة غير صحيحة.",
            ["Leave request was not found."] = "طلب الإجازة غير موجود.",
            ["Leave start and end dates are required."] = "تاريخا بداية الإجازة ونهايتها مطلوبان.",
            ["Leave status is invalid."] = "حالة الإجازة غير صحيحة.",
            ["No usable header row was detected."] = "لم يتم العثور على صف عناوين صالح للاستخدام.",
            ["Only a preview-ready attendance import can be confirmed."] = "لا يمكن التأكيد إلا بعد تجهيز معاينة استيراد الحضور.",
            ["Only CSV, XLS, and XLSX attendance files are supported."] = "ملفات الحضور المدعومة هي CSV وXLS وXLSX فقط.",
            ["Page must be at least 1 and pageSize must be between 1 and 100."] = "يجب ألا يقل رقم الصفحة عن 1، وأن يكون حجم الصفحة بين 1 و100.",
            ["Pagination values are invalid."] = "قيم ترقيم الصفحات غير صحيحة.",
            ["Punch date/time mapping is required for punch-row layout."] = "ربط عمود تاريخ ووقت البصمة مطلوب لتنسيق صفوف البصمات.",
            ["Report export format must be excel or pdf."] = "يجب أن تكون صيغة تصدير التقرير Excel أو PDF.",
            ["Report was not found."] = "التقرير غير موجود.",
            ["Search cannot exceed 160 characters."] = "لا يمكن أن يتجاوز البحث 160 حرفًا.",
            ["Select a non-empty attendance file."] = "اختر ملف حضور غير فارغ.",
            ["Select a non-empty CSV or Excel file."] = "اختر ملف CSV أو Excel غير فارغ.",
            ["Sort direction must be asc or desc."] = "يجب أن يكون اتجاه الترتيب تصاعديًا أو تنازليًا.",
            ["Start date is required."] = "تاريخ البداية مطلوب.",
            ["Start, end, and break values are only valid for custom working hours."] = "قيم البداية والنهاية والاستراحة متاحة فقط مع ساعات العمل المخصصة.",
            ["Status must be all, active, inactive, on leave, suspended, or terminated."] = "يجب أن تكون الحالة: الكل أو نشط أو غير نشط أو في إجازة أو موقوف أو منتهية خدمته.",
            ["Status must be all, pending, excused, or unexcused."] = "يجب أن تكون الحالة: الكل أو قيد الانتظار أو بعذر أو بدون عذر.",
            ["Status must be Pending, Excused, or Unexcused."] = "يجب أن تكون الحالة: قيد الانتظار أو بعذر أو بدون عذر.",
            ["Stored attendance files must be seekable."] = "ملفات الحضور المخزنة غير قابلة للقراءة بالطريقة المطلوبة.",
            ["Stored attendance import JSON is invalid."] = "بيانات استيراد الحضور المخزنة غير صالحة.",
            ["Termination date cannot be in the future or before the employee hire date."] = "لا يمكن أن يكون تاريخ إنهاء الخدمة في المستقبل أو قبل تاريخ التعيين.",
            ["The attendance import is already cancelled."] = "تم إلغاء عملية استيراد الحضور بالفعل.",
            ["The authenticated user identifier is invalid."] = "معرّف المستخدم المسجل غير صحيح.",
            ["The configured CSV header row was not found before the data rows."] = "لم يتم العثور على صف عناوين CSV المحدد قبل صفوف البيانات.",
            ["The configured CSV header row was not found."] = "لم يتم العثور على صف عناوين CSV المحدد.",
            ["The configured Excel header row was not found before the data rows."] = "لم يتم العثور على صف عناوين Excel المحدد قبل صفوف البيانات.",
            ["The current compensation is future-dated and must be corrected before adding a new version."] = "بيانات الراتب الحالية مؤرخة في المستقبل ويجب تصحيحها قبل إضافة نسخة جديدة.",
            ["The current working day cannot be processed because its scheduled end time is not configured."] = "لا يمكن معالجة يوم العمل الحالي لأن وقت انتهاء الدوام غير مُعد.",
            ["The document file name is required and cannot exceed 255 characters."] = "اسم ملف المستند مطلوب ولا يمكن أن يتجاوز 255 حرفًا.",
            ["The employee already has an overlapping pending or approved leave request."] = "لدى الموظف طلب إجازة آخر متداخل قيد الانتظار أو معتمد.",
            ["The employee has an approved leave on this date; manual attendance punches cannot be recorded until the leave is resolved."] = "لدى الموظف إجازة معتمدة في هذا التاريخ؛ لا يمكن تسجيل بصمات حضور يدوية حتى تتم معالجة الإجازة.",
            ["The file content is not a supported document format."] = "محتوى الملف ليس بإحدى صيغ المستندات المدعومة.",
            ["The file extension is invalid."] = "امتداد الملف غير صحيح.",
            ["The request conflicts with existing data. Refresh and try again."] = "يتعارض الطلب مع بيانات موجودة. حدّث الصفحة وحاول مرة أخرى.",
            ["The request violates a data integrity rule."] = "يخالف الطلب إحدى قواعد سلامة البيانات.",
            ["The requested master-data record was not found."] = "سجل البيانات الأساسية المطلوب غير موجود.",
            ["The selected attachment does not belong to this employee or is unavailable."] = "المرفق المحدد لا يخص هذا الموظف أو غير متاح.",
            ["The selected branch does not exist or is inactive."] = "الفرع المحدد غير موجود أو غير نشط.",
            ["The selected contract type does not exist or is inactive."] = "نوع العقد المحدد غير موجود أو غير نشط.",
            ["The selected delegation type does not exist or is inactive."] = "نوع التفويض المحدد غير موجود أو غير نشط.",
            ["The selected department does not exist or is inactive."] = "القسم المحدد غير موجود أو غير نشط.",
            ["The selected direct manager does not exist or is inactive."] = "المدير المباشر المحدد غير موجود أو غير نشط.",
            ["The selected direct manager would create a reporting-line cycle."] = "اختيار هذا المدير المباشر سيؤدي إلى حلقة غير صالحة في التسلسل الإداري.",
            ["The selected document type does not exist or is inactive."] = "نوع المستند المحدد غير موجود أو غير نشط.",
            ["The selected employee does not exist or is inactive."] = "الموظف المحدد غير موجود أو غير نشط.",
            ["The selected employment type does not exist or is inactive."] = "نوع التوظيف المحدد غير موجود أو غير نشط.",
            ["The selected import culture is invalid."] = "إعداد لغة وتنسيق الاستيراد غير صحيح.",
            ["The selected import time zone is invalid."] = "المنطقة الزمنية المحددة للاستيراد غير صحيحة.",
            ["The selected import time zone is not available on this server."] = "المنطقة الزمنية المحددة للاستيراد غير متاحة على الخادم.",
            ["The selected leave type requires an attachment."] = "نوع الإجازة المحدد يتطلب مرفقًا.",
            ["The selected local time does not exist because of a daylight-saving transition."] = "الوقت المحلي المحدد غير موجود بسبب الانتقال للتوقيت الصيفي.",
            ["The selected period does not contain any working days."] = "الفترة المحددة لا تحتوي على أي أيام عمل.",
            ["The selected position does not exist, is inactive, or belongs to another department."] = "المسمى الوظيفي المحدد غير موجود أو غير نشط أو يتبع قسمًا آخر.",
            ["The selected time zone configuration is invalid."] = "إعداد المنطقة الزمنية المحددة غير صحيح.",
            ["The selected time zone is not available on this server."] = "المنطقة الزمنية المحددة غير متاحة على الخادم.",
            ["The selected worksheet was not found."] = "ورقة العمل المحددة غير موجودة.",
            ["The storage key is invalid."] = "مرجع التخزين غير صحيح.",
            ["The stored attendance file must be seekable."] = "ملف الحضور المخزن غير قابل للقراءة بالطريقة المطلوبة.",
            ["The stored file was not found."] = "الملف المخزن غير موجود.",
            ["The to date cannot be earlier than the from date."] = "لا يمكن أن يكون تاريخ النهاية قبل تاريخ البداية.",
            ["The uploaded file content does not match its CSV or Excel extension."] = "محتوى الملف المرفوع لا يتطابق مع امتداد CSV أو Excel.",
            ["The uploaded file does not contain any rows."] = "الملف المرفوع لا يحتوي على أي صفوف.",
            ["The uploaded file exceeds the 10 MB limit."] = "يتجاوز الملف المرفوع الحد الأقصى البالغ 10 ميجابايت.",
            ["The uploaded file is empty."] = "الملف المرفوع فارغ.",
            ["The workbook does not contain any worksheets."] = "ملف Excel لا يحتوي على أي أوراق عمل.",
            ["The working calendar has not been configured."] = "لم يتم إعداد تقويم العمل بعد.",
            ["The working calendar must contain at least one working day."] = "يجب أن يحتوي تقويم العمل على يوم عمل واحد على الأقل.",
            ["The working calendar must define every day of the week exactly once."] = "يجب أن يحدد تقويم العمل كل يوم من أيام الأسبوع مرة واحدة فقط.",
            ["This attendance file has already been confirmed."] = "تم تأكيد ملف الحضور هذا بالفعل.",
            ["This attendance file has already been uploaded."] = "تم رفع ملف الحضور هذا بالفعل.",
            ["This document is attached to leave history and cannot be deleted. Replace the file if a correction is required."] = "هذا المستند مرتبط بسجل إجازة ولا يمكن حذفه. استبدل الملف إذا كان يلزم تصحيحه.",
            ["Total leave entitlement cannot be negative."] = "لا يمكن أن يكون إجمالي استحقاق الإجازة سالبًا.",
            ["Type must be Absent for V1."] = "يجب أن يكون النوع غيابًا في الإصدار الحالي.",
            ["Unknown master-data category."] = "تصنيف البيانات الأساسية غير معروف.",
            ["Unknown master-data entity."] = "نوع سجل البيانات الأساسية غير معروف.",
            ["Uploaded to cannot be before uploaded from."] = "لا يمكن أن يكون تاريخ الرفع حتى قبل تاريخ الرفع من.",
            ["Validation failed."] = "تعذر التحقق من صحة البيانات.",
            ["You are not authorized to access this resource."] = "غير مصرح لك بالوصول إلى هذا المورد.",
            ["The data changed during this request. Refresh and try again."] = "تغيّرت البيانات أثناء تنفيذ الطلب. حدّث الصفحة وحاول مرة أخرى.",
            ["Egyptian national ID must contain exactly 14 digits."] = "يجب أن يتكون الرقم القومي المصري من 14 رقمًا بالضبط.",
            ["Egyptian national ID has an invalid century digit."] = "خانة القرن في الرقم القومي المصري غير صحيحة.",
            ["Egyptian national ID contains an invalid birth date."] = "الرقم القومي المصري يحتوي على تاريخ ميلاد غير صحيح.",
            ["Egyptian national ID cannot contain a future birth date."] = "لا يمكن أن يحتوي الرقم القومي المصري على تاريخ ميلاد مستقبلي.",
            ["Date of birth cannot be in the future."] = "تاريخ الميلاد لا يمكن أن يكون في المستقبل.",
            ["Date of birth does not match the Egyptian national ID."] = "تاريخ الميلاد لا يطابق الرقم القومي المصري.",
            ["Work end date cannot be before work start date."] = "تاريخ انتهاء العمل لا يمكن أن يسبق تاريخ بدء العمل.",
            ["Gender does not match the Egyptian national ID."] = "النوع لا يطابق الرقم القومي المصري.",
            ["Phone number is required."] = "رقم الهاتف مطلوب.",
            ["Mobile number must be a valid phone number of at most 32 characters."] = "يجب إدخال رقم موبايل صالح لا يتجاوز 32 حرفاً.",
            ["Phone number must be a valid international phone number."] = "يجب إدخال رقم هاتف دولي صحيح.",
            ["Phone number must be a valid Egyptian mobile number (010, 011, 012, or 015)."] = "يجب إدخال رقم محمول مصري صحيح يبدأ بـ 010 أو 011 أو 012 أو 015.",
            ["IBAN format is invalid."] = "صيغة رقم IBAN غير صحيحة.",
            ["Egyptian IBAN must start with EG and contain 29 characters."] = "يجب أن يبدأ رقم IBAN المصري بـ EG وأن يتكون من 29 حرفًا ورقمًا.",
            ["IBAN checksum is invalid."] = "رقم التحقق الخاص بـ IBAN غير صحيح.",
            ["Hire date cannot be in the future."] = "لا يمكن أن يكون تاريخ التعيين في المستقبل.",
            ["Hire date must be after the employee date of birth."] = "يجب أن يكون تاريخ التعيين بعد تاريخ ميلاد الموظف.",
            ["Hire date cannot be after the employee termination date."] = "لا يمكن أن يكون تاريخ التعيين بعد تاريخ انتهاء الخدمة.",
            ["Contract start date cannot be before the employee hire date."] = "لا يمكن أن يبدأ العقد قبل تاريخ تعيين الموظف.",
            ["Contract start date cannot be after the employee termination date."] = "لا يمكن أن يبدأ العقد بعد تاريخ انتهاء خدمة الموظف.",
            ["Attendance date cannot be before the employee hire date."] = "لا يمكن تسجيل الحضور قبل تاريخ تعيين الموظف.",
            ["Attendance date cannot be after the employee termination date."] = "لا يمكن تسجيل الحضور بعد تاريخ انتهاء خدمة الموظف.",
            ["Attendance cannot be recorded for a future date."] = "لا يمكن تسجيل الحضور في تاريخ مستقبلي.",
            ["Attendance cannot be imported for a future date."] = "لا يمكن استيراد حضور في تاريخ مستقبلي.",
            ["Attendance date is before the employee hire date."] = "تاريخ الحضور يسبق تاريخ تعيين الموظف.",
            ["Attendance date is after the employee termination date."] = "تاريخ الحضور يلي تاريخ انتهاء خدمة الموظف.",
            ["Leave cannot start before the employee hire date."] = "لا يمكن أن تبدأ الإجازة قبل تاريخ تعيين الموظف.",
            ["Leave cannot extend beyond the employee termination date."] = "لا يمكن أن تمتد الإجازة بعد تاريخ انتهاء خدمة الموظف.",
            ["Delegation cannot start before the employee hire date."] = "لا يمكن أن يبدأ التفويض قبل تاريخ تعيين الموظف.",
            ["Delegation cannot extend beyond the employee termination date."] = "لا يمكن أن يمتد التفويض بعد تاريخ انتهاء خدمة الموظف.",
            ["Absence cannot be recorded for a future date."] = "لا يمكن تسجيل غياب في تاريخ مستقبلي.",
            ["Absence date must fall within the employee employment period."] = "يجب أن يقع تاريخ الغياب داخل فترة خدمة الموظف.",
            ["An absence case already exists for this employee and date."] = "توجد بالفعل حالة غياب لهذا الموظف في التاريخ نفسه.",
            ["An absence case cannot be recorded on an approved leave date."] = "لا يمكن تسجيل غياب في يوم إجازة معتمدة.",
            ["Recorded attendance conflicts with this absence date. Resolve the attendance record first."] = "يوجد سجل حضور يتعارض مع تاريخ الغياب. عالج سجل الحضور أولًا.",
            ["Employee employment data changed after the attendance preview. Rebuild the preview before confirming."] = "تغيرت بيانات خدمة الموظف بعد معاينة الحضور. أعد إنشاء المعاينة قبل التأكيد.",
            ["Compensation effective date is required and cannot be in the future."] = "تاريخ سريان الراتب مطلوب ولا يمكن أن يكون في المستقبل.",
            ["Compensation effective date cannot be before the employee hire date."] = "لا يمكن أن يسبق تاريخ سريان الراتب تاريخ تعيين الموظف.",
            ["Compensation effective date cannot be after the employee termination date."] = "لا يمكن أن يلي تاريخ سريان الراتب تاريخ انتهاء خدمة الموظف.",
            ["Compensation effective date cannot be before the current compensation version."] = "لا يمكن أن يسبق تاريخ سريان الراتب النسخة الحالية من بيانات الراتب.",
            ["Only an HR manager can access employee compensation and banking information."] = "بيانات الراتب والبنك متاحة لمدير الموارد البشرية المصرح له فقط.",
            ["Termination date and reason are required."] = "تاريخ وسبب انتهاء الخدمة مطلوبان.",

            // Successful response text and audit presentation.
            ["System"] = "النظام",
            ["Unassigned"] = "غير محدد",
            ["Default company calendar"] = "تقويم العمل الافتراضي للشركة",
            ["Human Resources"] = "الموارد البشرية", ["Legal"] = "الشؤون القانونية",
            ["Administration"] = "الإدارة", ["Data Entry"] = "إدخال البيانات", ["Accounting"] = "الحسابات",
            ["Main Branch"] = "الفرع الرئيسي", ["Full Time"] = "دوام كامل", ["Part Time"] = "دوام جزئي",
            ["Temporary"] = "مؤقت", ["Internship"] = "تدريب", ["Permanent"] = "دائم",
            ["Fixed Term"] = "محدد المدة", ["Project Based"] = "مرتبط بمشروع",
            ["Annual Leave"] = "إجازة سنوية", ["Sick Leave"] = "إجازة مرضية", ["Emergency Leave"] = "إجازة طارئة",
            ["Unpaid Leave"] = "إجازة بدون راتب", ["Maternity Leave"] = "إجازة وضع", ["Permission"] = "إذن",
            ["Early Leave"] = "انصراف مبكر", ["Contract"] = "عقد العمل",
            ["Graduation Certificate"] = "شهادة التخرج", ["Military Certificate"] = "شهادة الموقف من التجنيد",
            ["Insurance Document"] = "مستند التأمينات", ["Medical Document"] = "مستند طبي", ["CV"] = "السيرة الذاتية",
            ["Other"] = "أخرى", ["Cheque Collection"] = "استلام شيكات", ["Document Collection"] = "استلام مستندات",
            ["Government Procedures"] = "إجراءات حكومية", ["General Administrative"] = "تفويض إداري عام",
            ["HR Manager"] = "مدير الموارد البشرية", ["HR Officer"] = "مسؤول موارد بشرية",
            ["Yes"] = "نعم",
            ["No"] = "لا",
            ["Employee birthday"] = "عيد ميلاد الموظف",
            ["Probation period"] = "فترة التجربة",
            ["Recorded employee absence."] = "تم تسجيل غياب الموظف.",
            ["Updated employee absence."] = "تم تحديث غياب الموظف.",
            ["Deleted employee absence."] = "تم حذف غياب الموظف.",
            ["Updated personal information."] = "تم تحديث البيانات الشخصية.",
            ["Updated contact information."] = "تم تحديث بيانات التواصل.",
            ["Updated employment information."] = "تم تحديث بيانات التوظيف.",
            ["Updated emergency contact information."] = "تم تحديث بيانات جهة اتصال الطوارئ.",
            ["Changed employee status."] = "تم تغيير حالة الموظف.",
            ["Created employee contract information."] = "تم إنشاء بيانات عقد الموظف.",
            ["Replaced employee contract information while preserving the previous version."] = "تم استبدال بيانات عقد الموظف مع الاحتفاظ بالنسخة السابقة.",
            ["Created restricted employee compensation information. The audit contains metadata only."] = "تم إنشاء بيانات راتب الموظف المقيدة. يحتوي سجل التدقيق على البيانات الوصفية فقط.",
            ["Corrected the current restricted employee compensation version. The audit contains metadata only."] = "تم تصحيح النسخة الحالية من بيانات راتب الموظف المقيدة. يحتوي سجل التدقيق على البيانات الوصفية فقط.",
            ["Replaced restricted employee compensation information while preserving history. The audit contains metadata only."] = "تم استبدال بيانات راتب الموظف المقيدة مع الاحتفاظ بالسجل التاريخي. يحتوي سجل التدقيق على البيانات الوصفية فقط.",
            ["Updated employee document details."] = "تم تحديث بيانات مستند الموظف.",
            ["Replaced employee document file."] = "تم استبدال ملف مستند الموظف.",
            ["Deleted employee document."] = "تم حذف مستند الموظف.",
            ["Soft-deleted attendance record."] = "تم حذف سجل الحضور مع الاحتفاظ بتاريخه.",
            ["Uploaded attendance source file."] = "تم رفع ملف مصدر الحضور.",
            ["Cancelled attendance import."] = "تم إلغاء استيراد الحضور.",
            ["Updated company working calendar and weekend rules."] = "تم تحديث تقويم عمل الشركة وقواعد العطلة الأسبوعية.",
            ["Activated calendar exception."] = "تم تفعيل استثناء التقويم.",
            ["Deactivated calendar exception."] = "تم إلغاء تفعيل استثناء التقويم.",
            ["Soft-deleted calendar exception."] = "تم حذف استثناء التقويم مع الاحتفاظ بتاريخه.",
            ["Updated pending leave request."] = "تم تحديث طلب الإجازة المعلق.",
            ["Approved leave request."] = "تم اعتماد طلب الإجازة.",
            ["Rejected leave request."] = "تم رفض طلب الإجازة.",
            ["Cancelled leave request."] = "تم إلغاء طلب الإجازة.",

            // Report catalog.
            ["Employee List"] = "قائمة الموظفين",
            ["Current employee directory and organization assignments."] = "دليل الموظفين الحالي وتوزيعهم التنظيمي.",
            ["Employee Details"] = "تفاصيل الموظفين",
            ["Detailed employee profile without compensation or bank information."] = "ملف الموظف التفصيلي دون بيانات الرواتب أو الحسابات البنكية.",
            ["Attendance Report"] = "تقرير الحضور",
            ["Attendance records and calculated working-time values."] = "سجلات الحضور وقيم وقت العمل المحسوبة.",
            ["Absence Report"] = "تقرير الغياب",
            ["Registered company absences and their reviewed payroll impact."] = "حالات غياب الموظفين وتأثيرها على المرتب بعد المراجعة.",
            ["Payroll Impact"] = "التأثير على المرتب",
            ["NotApplicable"] = "بدون خصم",
            ["PendingReview"] = "يحتاج مراجعة",
            ["Excluded"] = "مستبعد من الخصم",
            ["Registered company absences."] = "حالات الغياب المسجلة بالشركة.",
            ["Leave Report"] = "تقرير الإجازات",
            ["Leave requests and decisions."] = "طلبات الإجازة والقرارات المتعلقة بها.",
            ["Late Employees"] = "الموظفون المتأخرون",
            ["Attendance records with calculated late minutes."] = "سجلات الحضور التي تحتوي على دقائق تأخير محسوبة.",
            ["Overtime Report"] = "تقرير الوقت الإضافي",
            ["Attendance records with approved calculated overtime."] = "سجلات الحضور التي تحتوي على وقت إضافي محسوب ومعتمد.",
            ["Expiring Contracts"] = "العقود المنتهية أو القريبة من الانتهاء",
            ["Contracts that have expired recently or are approaching their end date."] = "العقود المنتهية حديثًا أو التي يقترب تاريخ انتهائها.",
            ["Expiring Documents"] = "المستندات المنتهية أو القريبة من الانتهاء",
            ["Employee documents that have expired recently or will expire soon."] = "مستندات الموظفين المنتهية حديثًا أو التي ستنتهي قريبًا.",
            ["Employees by Department"] = "الموظفون حسب القسم",
            ["Employee totals grouped by department."] = "إجمالي الموظفين مجمعًا حسب القسم.",
            ["Employees by Branch"] = "الموظفون حسب الفرع",
            ["Employee totals grouped by branch."] = "إجمالي الموظفين مجمعًا حسب الفرع.",
            ["Delegations Report"] = "تقرير التفويضات",
            ["Administrative delegations and their effective periods."] = "التفويضات الإدارية وفترات سريانها.",

            // Report column headers and commonly presented values.
            ["Employee ID"] = "الرقم الوظيفي",
            ["Employee Name"] = "اسم الموظف",
            ["Full Name Arabic"] = "الاسم الكامل بالعربية",
            ["Full Name English"] = "الاسم الكامل بالإنجليزية",
            ["National ID"] = "الرقم القومي",
            ["Department"] = "القسم",
            ["Position"] = "المسمى الوظيفي",
            ["Branch"] = "الفرع",
            ["Direct Manager"] = "المدير المباشر",
            ["Hire Date"] = "تاريخ التعيين",
            ["Employment Type"] = "نوع التوظيف",
            ["Status"] = "الحالة",
            ["Date"] = "التاريخ",
            ["Check In"] = "وقت الدخول",
            ["Check Out"] = "وقت الخروج",
            ["Working Hours"] = "ساعات العمل",
            ["Late Minutes"] = "دقائق التأخير",
            ["Early Leave Minutes"] = "دقائق الانصراف المبكر",
            ["Overtime Minutes"] = "دقائق الوقت الإضافي",
            ["Source"] = "المصدر",
            ["Absence Date"] = "تاريخ الغياب",
            ["Type"] = "النوع",
            ["Reason"] = "السبب",
            ["Request Date"] = "تاريخ الطلب",
            ["Start Date"] = "تاريخ البداية",
            ["End Date"] = "تاريخ النهاية",
            ["Number of Days"] = "عدد الأيام",
            ["Decision Date"] = "تاريخ القرار",
            ["Contract Type"] = "نوع العقد",
            ["Contract Start"] = "بداية العقد",
            ["Contract End"] = "نهاية العقد",
            ["Days Remaining"] = "الأيام المتبقية",
            ["Document Type"] = "نوع المستند",
            ["File Name"] = "اسم الملف",
            ["Issue Date"] = "تاريخ الإصدار",
            ["Expiry Date"] = "تاريخ الانتهاء",
            ["Delegation Number"] = "رقم التفويض",
            ["Delegation Type"] = "نوع التفويض",
            ["Subject"] = "الموضوع",
            ["Authorized Entity"] = "جهة التفويض",
            ["Purpose"] = "الغرض",
            ["Employee Count"] = "عدد الموظفين",
            ["Code"] = "الكود",
            ["Value"] = "القيمة",
            ["Generated Date"] = "تاريخ الإنشاء",
            ["Applied Filters"] = "الفلاتر المطبقة",
            ["Search"] = "البحث", ["Date From"] = "التاريخ من", ["Date To"] = "التاريخ إلى",
            ["Employee"] = "الموظف", ["Arabic Name"] = "الاسم بالعربية", ["English Name"] = "الاسم بالإنجليزية",
            ["Date of Birth"] = "تاريخ الميلاد", ["Gender"] = "النوع", ["Marital Status"] = "الحالة الاجتماعية",
            ["Mobile"] = "رقم الهاتف", ["Email"] = "البريد الإلكتروني", ["City"] = "المدينة",
            ["Probation End"] = "نهاية فترة التجربة", ["Days"] = "عدد الأيام", ["Decision Notes"] = "ملاحظات القرار",
            ["Department Code"] = "كود القسم", ["Branch Code"] = "كود الفرع", ["Total Employees"] = "إجمالي الموظفين",
            ["Active"] = "نشط", ["Inactive"] = "غير نشط", ["Created At"] = "تاريخ الإنشاء", ["Leave Type"] = "نوع الإجازة",
            ["Username or email is already in use."] = "اسم المستخدم أو البريد الإلكتروني مستخدم بالفعل.",
            ["Choose an active department."] = "اختر قسمًا نشطًا.",
            ["One or more roles are invalid."] = "يوجد دور غير صالح.",
            ["Administrator access must be granted separately through the protected access review."] = "صلاحية مدير النظام تُمنح فقط من مراجعة الوصول المحمية.",
            ["Type GRANT ADMIN ACCESS exactly to confirm administrator access."] = "للتأكيد اكتب GRANT ADMIN ACCESS حرفيًا.",
            ["User was not found."] = "المستخدم غير موجود.",
            ["User account created with a one-time password."] = "تم إنشاء الحساب بكلمة مرور لمرة واحدة.",
            ["Temporary password issued by an administrator. The user must change it at next sign-in."] = "تم إصدار كلمة مرور مؤقتة. يجب على المستخدم تغييرها عند الدخول التالي.",
            ["Temporary password must be at least 12 characters and include upper, lower, number, and symbol."] = "كلمة المرور المؤقتة يجب ألا تقل عن ١٢ حرفًا وتشمل حرفًا كبيرًا وصغيرًا ورقمًا ورمزًا.",
            ["Password must contain uppercase, lowercase, number, and special character and be at least 10 characters."] = "كلمة المرور يجب ألا تقل عن ١٠ أحرف وتشمل حرفًا كبيرًا وصغيرًا ورقمًا ورمزًا خاصًا.",
            ["New password and confirmation do not match."] = "كلمة المرور الجديدة وتأكيدها غير متطابقين.",
            ["New password must be different from the current password."] = "يجب أن تختلف كلمة المرور الجديدة عن الحالية.",
            ["Current password is incorrect."] = "كلمة المرور الحالية غير صحيحة.",
            ["User account was not found."] = "حساب المستخدم غير موجود.",
            ["This employee is linked to a system user account and cannot be deleted. Unlink the account in Admin, or archive the employee."] = "هذا الموظف مربوط بحساب على النظام ولا يمكن حذفه. ألغِ الربط من الإدارة، أو أرشف الموظف.",
            ["This employee has operational history and cannot be deleted. Archive the record instead."] = "لهذا الموظف سجل تشغيلي ولا يمكن حذفه. أرشف السجل بدلًا من الحذف.",
            ["Only administrators can permanently delete employees."] = "حذف الموظف نهائيًا متاح للأدمن فقط.",
            ["This employee still has related records and could not be deleted."] = "ما زال لهذا الموظف سجلات مرتبطة وتعذر حذفه.",
            ["You cannot delete your own account."] = "لا يمكنك حذف حسابك.",
            ["The last administrator cannot be deleted."] = "لا يمكن حذف آخر مدير نظام.",
            ["This account has operational history and cannot be deleted. Suspend the account instead."] = "لهذا الحساب سجل تشغيلي ولا يمكن حذفه. أوقف الحساب بدلًا من الحذف.",
            ["This master-data record is in use and cannot be deleted. Deactivate it instead."] = "هذا السجل مستخدم ولا يمكن حذفه. أوقف تفعيله بدلًا من الحذف.",
            ["This commission rule has been used in calculations and cannot be deleted. Deactivate it instead."] = "قاعدة العمولة استُخدمت في احتسابات ولا يمكن حذفها. أوقف تفعيلها بدلًا من الحذف.",
            ["Inactive commission rules cannot be reactivated. Create a new rule instead."] = "لا يمكن إعادة تفعيل قاعدة عمولة موقوفة. أنشئ قاعدة جديدة.",
            ["This client already has a collections case and cannot be deleted."] = "لهذا العميل حالة في التحصيل ولا يمكن حذفه.",
            ["This client is already in the collections pipeline and cannot be deleted."] = "هذا العميل داخل مسار التحصيل ولا يمكن حذفه.",
            ["Commission rule was not found."] = "قاعدة العمولة غير موجودة.",
            ["Data-entry client was not found."] = "عميل إدخال البيانات غير موجود."
        };

    private static readonly IReadOnlyDictionary<string, string> ArabicCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Active"] = "نشط", ["Inactive"] = "غير نشط", ["OnLeave"] = "في إجازة",
            ["Suspended"] = "موقوف", ["Terminated"] = "منتهية خدمته", ["Draft"] = "مسودة",
            ["Expired"] = "منتهي", ["Cancelled"] = "ملغي", ["Pending"] = "قيد الانتظار",
            ["Approved"] = "معتمد", ["Rejected"] = "مرفوض", ["Present"] = "حاضر",
            ["Absent"] = "غائب", ["Late"] = "متأخر", ["Leave"] = "إجازة",
            ["Holiday"] = "عطلة", ["Weekend"] = "عطلة أسبوعية", ["Manual"] = "يدوي",
            ["ExcelImport"] = "استيراد Excel", ["DeviceIntegration"] = "تكامل جهاز البصمة",
            ["SystemProcessing"] = "معالجة النظام", ["Uploaded"] = "تم الرفع",
            ["PreviewReady"] = "المعاينة جاهزة", ["Confirmed"] = "مؤكد", ["Failed"] = "فشل",
            ["Valid"] = "صالح", ["Invalid"] = "غير صالح", ["EmployeeNotFound"] = "الموظف غير موجود",
            ["Duplicate"] = "مكرر", ["MissingCheckIn"] = "وقت الدخول مفقود",
            ["MissingCheckOut"] = "وقت الخروج مفقود", ["Excused"] = "بعذر",
            ["Unexcused"] = "بدون عذر", ["Critical"] = "حرج", ["Warning"] = "تحذير",
            ["Info"] = "معلومة", ["Upcoming"] = "قريب", ["Contract"] = "عقد",
            ["Document"] = "مستند", ["Probation"] = "فترة تجربة", ["Birthday"] = "عيد ميلاد",
            ["Expiring Soon"] = "قريب الانتهاء", ["Created"] = "تم الإنشاء", ["Replaced"] = "تم الاستبدال"
        };

    private static readonly IReadOnlyDictionary<string, string> ArabicFields =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Value"] = "القيمة", ["Id"] = "المعرّف", ["EmployeeId"] = "الموظف",
            ["Username"] = "اسم المستخدم", ["Password"] = "كلمة المرور", ["Page"] = "رقم الصفحة",
            ["PageSize"] = "حجم الصفحة", ["Search"] = "البحث", ["SortBy"] = "حقل الترتيب",
            ["SortDirection"] = "اتجاه الترتيب",
            ["EmployeeNumber"] = "الرقم الوظيفي", ["FullName"] = "الاسم الكامل",
            ["FullNameArabic"] = "الاسم بالعربية", ["FullNameEnglish"] = "الاسم بالإنجليزية",
            ["NationalId"] = "الرقم القومي", ["DateOfBirth"] = "تاريخ الميلاد", ["Gender"] = "النوع",
            ["MaritalStatus"] = "الحالة الاجتماعية", ["Status"] = "الحالة", ["IsActive"] = "نشط",
            ["MobileNumber"] = "رقم الهاتف", ["AlternativeMobile"] = "هاتف بديل", ["Email"] = "البريد الإلكتروني",
            ["Address"] = "العنوان", ["City"] = "المدينة", ["Department"] = "القسم",
            ["DepartmentId"] = "القسم", ["DepartmentName"] = "اسم القسم", ["Position"] = "المسمى الوظيفي",
            ["PositionId"] = "المسمى الوظيفي", ["PositionName"] = "اسم المسمى الوظيفي", ["Branch"] = "الفرع",
            ["BranchId"] = "الفرع", ["BranchName"] = "اسم الفرع", ["DirectManager"] = "المدير المباشر",
            ["DirectManagerId"] = "المدير المباشر", ["DirectManagerName"] = "اسم المدير المباشر",
            ["HireDate"] = "تاريخ التعيين", ["EmploymentType"] = "نوع التوظيف", ["EmploymentTypeId"] = "نوع التوظيف",
            ["ContractTypeId"] = "نوع العقد", ["ContractStartDate"] = "تاريخ بداية العقد",
            ["ContractEndDate"] = "تاريخ نهاية العقد", ["ProbationStartDate"] = "بداية فترة التجربة",
            ["ProbationEndDate"] = "نهاية فترة التجربة", ["BasicSalary"] = "الراتب الأساسي",
            ["Allowances"] = "البدلات", ["TotalSalary"] = "إجمالي الراتب", ["BankName"] = "اسم البنك",
            ["BankAccount"] = "الحساب البنكي", ["Iban"] = "رقم IBAN", ["Notes"] = "ملاحظات",
            ["AttendanceDate"] = "تاريخ الحضور", ["CheckIn"] = "وقت الدخول", ["CheckOut"] = "وقت الخروج",
            ["WorkingMinutes"] = "دقائق العمل", ["LateMinutes"] = "دقائق التأخير",
            ["EarlyLeaveMinutes"] = "دقائق الانصراف المبكر", ["OvertimeMinutes"] = "دقائق الوقت الإضافي",
            ["StartDate"] = "تاريخ البداية", ["EndDate"] = "تاريخ النهاية", ["Reason"] = "السبب",
            ["IssueDate"] = "تاريخ الإصدار", ["ExpiryDate"] = "تاريخ الانتهاء", ["FileName"] = "اسم الملف",
            ["DocumentType"] = "نوع المستند", ["DelegationNumber"] = "رقم التفويض", ["Subject"] = "الموضوع",
            ["Purpose"] = "الغرض", ["CreatedAt"] = "تاريخ الإنشاء", ["UpdatedAt"] = "تاريخ التحديث",
            ["TerminationDate"] = "تاريخ إنهاء الخدمة", ["TerminationReason"] = "سبب إنهاء الخدمة"
        };

    private static readonly IReadOnlyDictionary<string, string> ArabicActions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EmployeeCreated"] = "تم إنشاء موظف", ["EmployeeUpdated"] = "تم تحديث موظف",
            ["EmployeePersonalUpdated"] = "تم تحديث البيانات الشخصية", ["EmployeeContactUpdated"] = "تم تحديث بيانات التواصل",
            ["EmployeeEmploymentUpdated"] = "تم تحديث بيانات التوظيف", ["EmployeeStatusChanged"] = "تم تغيير حالة موظف",
            ["EmployeeContractUpdated"] = "تم تحديث عقد موظف", ["EmployeeCompensationUpdated"] = "تم تحديث راتب موظف",
            ["EmployeeEmergencyContactUpdated"] = "تم تحديث جهة اتصال الطوارئ", ["AbsenceCreated"] = "تم تسجيل غياب",
            ["AbsenceUpdated"] = "تم تحديث غياب", ["AbsenceDeleted"] = "تم حذف غياب",
            ["AttendanceAdded"] = "تمت إضافة حضور", ["AttendanceUpdated"] = "تم تحديث حضور",
            ["AttendanceDeleted"] = "تم حذف حضور", ["AttendanceDayProcessed"] = "تمت معالجة يوم حضور",
            ["AttendanceImportUploaded"] = "تم رفع ملف حضور", ["AttendanceImportPreviewed"] = "تمت معاينة ملف حضور",
            ["AttendanceImported"] = "تم استيراد حضور", ["AttendanceImportCancelled"] = "تم إلغاء استيراد حضور",
            ["LeaveCreated"] = "تم إنشاء طلب إجازة", ["LeaveUpdated"] = "تم تحديث طلب إجازة",
            ["LeaveApproved"] = "تم اعتماد طلب إجازة", ["LeaveRejected"] = "تم رفض طلب إجازة",
            ["LeaveCancelled"] = "تم إلغاء طلب إجازة", ["LeaveEntitlementCreated"] = "تم إنشاء استحقاق إجازة",
            ["LeaveEntitlementUpdated"] = "تم تحديث استحقاق إجازة", ["DocumentUploaded"] = "تم رفع مستند",
            ["DocumentUpdated"] = "تم تحديث مستند", ["DocumentReplaced"] = "تم استبدال مستند",
            ["DocumentDeleted"] = "تم حذف مستند", ["DelegationCreated"] = "تم إنشاء تفويض",
            ["DelegationUpdated"] = "تم تحديث تفويض", ["DelegationCancelled"] = "تم إلغاء تفويض",
            ["MasterDataCreated"] = "تم إنشاء بيانات أساسية", ["MasterDataUpdated"] = "تم تحديث بيانات أساسية",
            ["MasterDataActivated"] = "تم تفعيل بيانات أساسية", ["MasterDataDeactivated"] = "تم إلغاء تفعيل بيانات أساسية",
            ["WorkingCalendarUpdated"] = "تم تحديث تقويم العمل", ["CalendarExceptionCreated"] = "تم إنشاء استثناء تقويم",
            ["CalendarExceptionUpdated"] = "تم تحديث استثناء تقويم", ["CalendarExceptionStatusChanged"] = "تم تغيير حالة استثناء تقويم",
            ["CalendarExceptionDeleted"] = "تم حذف استثناء تقويم"
        };

    public static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

    public static string Localize(string? text, bool useGenericArabicFallback = false)
    {
        if (string.IsNullOrWhiteSpace(text) || !IsArabic) return text ?? string.Empty;
        var value = text.Trim();
        if (Arabic.TryGetValue(value, out var translated)) return translated;
        if (ArabicCodes.TryGetValue(value, out translated)) return translated;

        var withoutParameter = ParameterSuffixRegex().Replace(value, string.Empty);
        if (!withoutParameter.Equals(value, StringComparison.Ordinal) && Arabic.TryGetValue(withoutParameter, out translated))
            return translated;

        translated = LocalizeDynamic(value);
        if (translated.Length > 0) return translated;
        if (ContainsArabic(value)) return value;
        return useGenericArabicFallback ? GenericArabicError : value;
    }

    public static IReadOnlyCollection<string> LocalizeErrors(IEnumerable<string>? errors) =>
        errors?.Select(error => Localize(error, true)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

    public static string LocalizeCode(string value) =>
        IsArabic && ArabicCodes.TryGetValue(value, out var translated) ? translated : value;

    public static string LocalizeAction(string action, string? englishFallback = null) =>
        IsArabic && ArabicActions.TryGetValue(action, out var translated) ? translated : englishFallback ?? action;

    public static string LocalizeField(string field)
    {
        if (!IsArabic || string.IsNullOrWhiteSpace(field)) return field;
        if (Arabic.TryGetValue(field, out var exact)) return exact;
        var localized = FieldPartRegex().Replace(field, match =>
            ArabicFields.TryGetValue(match.Value, out var translated) ? translated : match.Value);
        return ContainsLatin(localized) ? "البيان" : localized;
    }

    public static string? LocalizeValue(string? value)
    {
        if (value is null || !IsArabic) return value;
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)) return "نعم";
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)) return "لا";
        if (ArabicCodes.TryGetValue(value, out var translated)) return translated;
        return Arabic.TryGetValue(value, out translated) ? translated : value;
    }

    private static string LocalizeDynamic(string value)
    {
        var match = MappedColumnRegex().Match(value);
        if (match.Success) return $"لم يتم العثور على العمود المرتبط بـ{LocalizeField(match.Groups[1].Value)} في صف العناوين المحدد.";

        match = MasterDataLookupRegex().Match(value);
        if (match.Success) return $"{LocalizeField(match.Groups[1].Value)}: القيمة «{match.Groups[2].Value}» غير موجودة في البيانات الأساسية.";

        match = ImportLimitRegex().Match(value);
        if (match.Success) return $"لا يمكن أن يتجاوز استيراد الحضور {match.Groups[1].Value} {TranslateImportUnit(match.Groups[2].Value)}.";

        match = UploadedFileLimitRegex().Match(value);
        if (match.Success) return $"يتجاوز الملف المرفوع الحد الأقصى البالغ {match.Groups[1].Value} ميجابايت.";

        match = ValueLengthRegex().Match(value);
        if (match.Success) return $"لا يمكن أن تتجاوز القيمة {match.Groups[1].Value} حرفًا.";

        match = ExportLimitRegex().Match(value);
        if (match.Success) return $"يحتوي التصدير على {match.Groups[1].Value} صفًا. عدّل الفلاتر ليصبح العدد {match.Groups[2].Value} صفًا أو أقل.";

        match = ReportRangeRegex().Match(value);
        if (match.Success) return $"لا يمكن أن تتجاوز فترة التقرير {match.Groups[1].Value} يومًا.";

        match = LeaveStateRegex().Match(value);
        if (match.Success)
            return $"لا يمكن {(match.Groups[2].Value.Equals("cancelled", StringComparison.OrdinalIgnoreCase) ? "إلغاء" : "تغيير")} طلب إجازة حالته {LocalizeCode(match.Groups[1].Value)}.";

        match = WorkingDayRegex().Match(value);
        if (match.Success)
        {
            var day = LocalizeDay(match.Groups[1].Value);
            return match.Groups[2].Value.StartsWith("requires", StringComparison.OrdinalIgnoreCase)
                ? $"يتطلب يوم {day} وقت بداية ووقت نهاية."
                : $"يوم {day} غير يوم عمل ولا يمكن أن يحتوي على إعدادات ساعات عمل.";
        }

        match = CurrentShiftRegex().Match(value);
        if (match.Success) return $"لا يمكن معالجة يوم العمل الحالي قبل انتهاء الوردية المجدولة في {match.Groups[1].Value}.";

        match = LeavePunchConflictRegex().Match(value);
        if (match.Success) return $"لا يمكن اعتماد الإجازة لوجود بصمات حضور مسجلة في: {match.Groups[1].Value}. عالج سجلات الحضور أولًا.";

        match = AuditDateRegex().Match(value);
        if (match.Success) return LocalizeAuditDateMessage(match.Groups[1].Value, match.Groups[2].Value);

        match = AuditDelegationRegex().Match(value);
        if (match.Success) return LocalizeDelegationMessage(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);

        match = AuditImportValidatedRegex().Match(value);
        if (match.Success) return $"تم التحقق من {match.Groups[1].Value} مجموعة حضور للموظف/اليوم.";

        match = AuditImportConfirmedRegex().Match(value);
        if (match.Success) return $"تم استيراد {match.Groups[1].Value} سجل حضور من الملف {match.Groups[2].Value}.";

        match = AuditLeaveCreatedRegex().Match(value);
        if (match.Success) return $"تم إنشاء طلب إجازة من {match.Groups[1].Value} إلى {match.Groups[2].Value}.";

        match = AuditEmployeeRegex().Match(value);
        if (match.Success) return $"تم {(match.Groups[1].Value.Equals("Created", StringComparison.OrdinalIgnoreCase) ? "إنشاء" : "تحديث")} الموظف {match.Groups[2].Value}.";

        match = AuditMasterDataRegex().Match(value);
        if (match.Success)
            return $"تم {(match.Groups[1].Value.Equals("Created", StringComparison.OrdinalIgnoreCase) ? "إنشاء" : "تحديث")} {Localize(match.Groups[2].Value)} ضمن {LocalizeMasterCategory(match.Groups[3].Value)}.";

        match = AuditDocumentUploadRegex().Match(value);
        if (match.Success) return $"تم رفع {Localize(match.Groups[1].Value)} للموظف {match.Groups[2].Value}.";

        match = RequiredFieldRegex().Match(value);
        if (match.Success) return $"حقل {LocalizeField(match.Groups[1].Value)} مطلوب.";

        match = MaximumLengthRegex().Match(value);
        if (match.Success) return $"لا يمكن أن يتجاوز حقل {LocalizeField(match.Groups[1].Value)} {match.Groups[2].Value} حرفًا.";

        match = MinimumMaximumLengthRegex().Match(value);
        if (match.Success) return $"يجب أن يكون طول حقل {LocalizeField(match.Groups[1].Value)} بين {match.Groups[2].Value} و{match.Groups[3].Value} أحرف.";

        match = RangeRegex().Match(value);
        if (match.Success) return $"يجب أن تكون قيمة حقل {LocalizeField(match.Groups[1].Value)} بين {match.Groups[2].Value} و{match.Groups[3].Value}.";

        match = InvalidFieldValueRegex().Match(value);
        if (match.Success) return $"القيمة المدخلة لحقل {LocalizeField(match.Groups[1].Value)} غير صحيحة.";

        return string.Empty;
    }

    private static string LocalizeAuditDateMessage(string action, string date) => action.ToLowerInvariant() switch
    {
        "added manual attendance for" => $"تمت إضافة حضور يدوي بتاريخ {date}.",
        "updated attendance for" => $"تم تحديث الحضور بتاريخ {date}.",
        "processed missing attendance records for" => $"تمت معالجة سجلات الحضور المفقودة بتاريخ {date}.",
        "created calendar exception for" => $"تم إنشاء استثناء تقويم بتاريخ {date}.",
        "updated calendar exception for" => $"تم تحديث استثناء التقويم بتاريخ {date}.",
        "set leave entitlement for" => $"تم تعيين استحقاق الإجازة لسنة {date}.",
        _ => string.Empty
    };

    private static string LocalizeDelegationMessage(string action, string number, string employeeNumber)
    {
        if (action.Equals("Created", StringComparison.OrdinalIgnoreCase))
            return $"تم إنشاء التفويض {number} للموظف {employeeNumber}.";
        return action.Equals("Updated", StringComparison.OrdinalIgnoreCase)
            ? $"تم تحديث التفويض {number}."
            : $"تم إلغاء التفويض {number}.";
    }

    private static string TranslateImportUnit(string unit) => unit.ToLowerInvariant() switch
    {
        "columns" => "عمودًا",
        "characters" => "حرفًا في الخلية",
        "data rows" => "صف بيانات",
        _ => "مجموعة موظف/يوم"
    };

    private static string LocalizeMasterCategory(string category) => category.Trim().ToLowerInvariant() switch
    {
        "departments" => "الأقسام", "positions" => "المسميات الوظيفية", "branches" => "الفروع",
        "employment-types" => "أنواع التوظيف", "contract-types" => "أنواع العقود",
        "leave-types" => "أنواع الإجازات", "document-types" => "أنواع المستندات",
        "delegation-types" => "أنواع التفويضات", _ => category
    };

    private static string LocalizeDay(string day) => day.ToLowerInvariant() switch
    {
        "sunday" => "الأحد", "monday" => "الاثنين", "tuesday" => "الثلاثاء",
        "wednesday" => "الأربعاء", "thursday" => "الخميس", "friday" => "الجمعة",
        "saturday" => "السبت", _ => day
    };

    private static bool ContainsArabic(string value) => value.Any(character => character is >= '\u0600' and <= '\u06ff');
    private static bool ContainsLatin(string value) => value.Any(character => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'));

    [GeneratedRegex(@"\s*\(Parameter '[^']+'\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterSuffixRegex();

    [GeneratedRegex(@"^Mapped (.+) column was not found in the selected header row\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MappedColumnRegex();

    [GeneratedRegex(@"^(Department|Position) not found: (.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MasterDataLookupRegex();

    [GeneratedRegex(@"^Attendance imports cannot exceed ([\d,]+) (columns|characters|data rows|employee/day groups)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImportLimitRegex();

    [GeneratedRegex(@"^The uploaded file exceeds the ([\d,]+) MB limit\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UploadedFileLimitRegex();

    [GeneratedRegex(@"^Value cannot exceed ([\d,]+) characters\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueLengthRegex();

    [GeneratedRegex(@"^The export contains ([\d,]+) rows\. Refine the filters to ([\d,]+) rows or fewer\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExportLimitRegex();

    [GeneratedRegex(@"^Report date ranges cannot exceed ([\d,]+) days\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReportRangeRegex();

    [GeneratedRegex(@"^A (\w+) leave request cannot be (changed|cancelled)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeaveStateRegex();

    [GeneratedRegex(@"^(Sunday|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday) (requires start and end times|is non-working and cannot contain working-hour settings)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WorkingDayRegex();

    [GeneratedRegex(@"^The current working day cannot be processed before the scheduled shift ends at (.+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrentShiftRegex();

    [GeneratedRegex(@"^Leave cannot be approved because recorded attendance punches exist on: (.+)\. Resolve those attendance records first\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeavePunchConflictRegex();

    [GeneratedRegex(@"^(Added manual attendance for|Updated attendance for|Processed missing attendance records for|Created calendar exception for|Updated calendar exception for|Set leave entitlement for) ([\d-]+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditDateRegex();

    [GeneratedRegex(@"^(Created|Updated|Cancelled) delegation (\S+)(?: for employee (\S+))?\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditDelegationRegex();

    [GeneratedRegex(@"^Validated ([\d,]+) employee/day attendance groups\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditImportValidatedRegex();

    [GeneratedRegex(@"^Imported ([\d,]+) attendance records from (.+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditImportConfirmedRegex();

    [GeneratedRegex(@"^Created leave request for ([\d-]+) through ([\d-]+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditLeaveCreatedRegex();

    [GeneratedRegex(@"^(Created|Updated) employee (.+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditEmployeeRegex();

    [GeneratedRegex(@"^(Created|Updated) (.+) in (.+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditMasterDataRegex();

    [GeneratedRegex(@"^Uploaded (.+) for employee (.+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuditDocumentUploadRegex();

    [GeneratedRegex(@"^The (?:field )?(.+?) field is required\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RequiredFieldRegex();

    [GeneratedRegex(@"^The field (.+?) must be a string with a maximum length of '?([\d,]+)'?\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaximumLengthRegex();

    [GeneratedRegex(@"^The field (.+?) must be a string with a minimum length of '?([\d,]+)'? and a maximum length of '?([\d,]+)'?\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinimumMaximumLengthRegex();

    [GeneratedRegex(@"^The field (.+?) must be between (.+) and (.+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^The value '.+' is not valid for (.+)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InvalidFieldValueRegex();

    [GeneratedRegex(@"[A-Za-z][A-Za-z0-9]*", RegexOptions.CultureInvariant)]
    private static partial Regex FieldPartRegex();
}
