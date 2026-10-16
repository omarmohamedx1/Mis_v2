using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Accounting;
using MIS.Domain.Constants;

namespace MIS.Infrastructure.Services;

public sealed partial class AccountingBookService
{
    private const int ExportLimit = 100_000;
    internal static byte[] Spreadsheet(IReadOnlyList<(string Name, string[] Headers, List<object?[]> Rows)> sheets, string subtitle, bool arabic)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Author = "MIS"; workbook.Properties.Title = "MIS Accounting";
        foreach (var (name, headers, rows) in sheets)
        {
            if (rows.Count > ExportLimit) throw new HrValidationException("The export exceeds 100000 rows. Narrow the filters and try again.");
            var sheet = workbook.Worksheets.Add(name.Length > 31 ? name[..31] : name);
            sheet.Style.Font.FontName = "Arial"; sheet.Style.Font.FontSize = 11;
            sheet.RightToLeft = arabic;
            sheet.Cell(1, 1).Value = name; sheet.Range(1, 1, 1, headers.Length).Merge().Style.Font.SetBold().Font.SetFontSize(18);
            sheet.Cell(2, 1).Value = subtitle; sheet.Range(2, 1, 2, headers.Length).Merge();
            sheet.Cell(3, 1).Value = (arabic ? "عدد السجلات: " : "Records: ") + rows.Count.ToString(CultureInfo.InvariantCulture);
            sheet.Cell(4, 1).Value = (arabic ? "تاريخ الاستخراج: " : "Exported: ") + TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo").ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            const int header = 6;
            for (var column = 0; column < headers.Length; column++) sheet.Cell(header, column + 1).Value = headers[column];
            var heading = sheet.Range(header, 1, header, headers.Length); heading.Style.Fill.BackgroundColor = XLColor.FromHtml("132E46"); heading.Style.Font.FontColor = XLColor.White; heading.Style.Font.Bold = true;
            for (var row = 0; row < rows.Count; row++)
                for (var column = 0; column < headers.Length; column++)
                {
                    var cell = sheet.Cell(header + row + 1, column + 1); var value = rows[row][column];
                    switch (value)
                    {
                        case null: cell.Value = ""; break;
                        case decimal number: cell.Value = (double)number; cell.Style.NumberFormat.Format = "#,##0.00;[Red](#,##0.00)"; break;
                        case int number: cell.Value = number; break;
                        case bool boolean: cell.Value = arabic ? (boolean ? "نعم" : "لا") : (boolean ? "Yes" : "No"); break;
                        case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "yyyy-mm-dd"; break;
                        default: cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; break;
                    }
                    if (row % 2 == 1) cell.Style.Fill.BackgroundColor = XLColor.FromHtml("F1F5F9");
                }
            sheet.Range(header, 1, header + Math.Max(rows.Count, 1), headers.Length).SetAutoFilter();
            sheet.SheetView.FreezeRows(header); sheet.Columns().Width = 23; sheet.Column(1).Width = 28;
            heading.Style.Font.Bold = true;
            sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape; sheet.PageSetup.FitToPages(1, 0);
        }
        using var output = new MemoryStream(); workbook.SaveAs(output); return output.ToArray();
    }

    public async Task<byte[]> ExportAsync(string report, AccountingExportFilter filter, CancellationToken token)
    {
        EnsureAccess(); var now = Today(); var year = filter.Year ?? now.Year; var month = filter.Month ?? now.Month;
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new HrValidationException("Provide a valid payroll year and month.");
        var from = filter.From ?? new DateOnly((filter.To ?? filter.AsOf)?.Year ?? year, (filter.To ?? filter.AsOf)?.Month ?? month, 1); var to = filter.To ?? filter.AsOf ?? new DateOnly(year, month, DateTime.DaysInMonth(year, month)); CheckDates(from, to);
        var ar = ApiTextLocalizer.IsArabic; string T(string a, string e) => ar ? a : e;
        var sheets = new List<(string Name, string[] Headers, List<object?[]> Rows)>();
        var all = report == "all";
        void Add(string name, string[] headers, IEnumerable<object?[]> rows) => sheets.Add((name, headers, rows.Take(ExportLimit + 1).ToList()));
        var lowerPeriod = from.Year * 12 + from.Month; var upperPeriod = to.Year * 12 + to.Month;
        if (all || report == "summary")
        {
            var summary = await SummaryAsync(from, to, token);
            Add(T("ملخص المصروفات والوارد", "Income and expense summary"), [T("البند", "Metric"), T("القيمة بالجنيه", "Amount EGP")], new List<object?[]>
            {
                new object?[] {T("إجمالي الإيرادات", "Income"), summary.Income}, new object?[] {T("إجمالي المصروفات", "Expenses"), summary.Expenses}, new object?[] {T("صافي النتيجة", "Net result"), summary.Income - summary.Expenses},
                new object?[] {T("إجمالي الوارد النقدي", "Cash inflow"), summary.CashIn}, new object?[] {T("إجمالي الخارج النقدي", "Cash outflow"), summary.CashOut}, new object?[] {T("صافي الحركة النقدية", "Net cash flow"), summary.NetCashFlow},
                new object?[] {T("سلف مصروفة", "Advances issued"), summary.AdvancesIssued}, new object?[] {T("سداد السلف", "Advance repayments"), summary.AdvanceRepayments}, new object?[] {T("رصيد السلف المتبقي", "Outstanding advances"), summary.OutstandingAdvances},
                new object?[] {T("سلف مرحلة", "Carried advances"), summary.CarriedAdvances}, new object?[] {T("حركات تنتظر الترحيل", "Pending posting"), summary.PendingAmount}, new object?[] {T("صرف سابق بدون قيد مرتبط", "Legacy paid records without journals"), summary.LegacyPaidCount}
            });
            Add(T("تفصيل البنود", "Category breakdown"), [T("البند", "Category"), T("المبلغ", "Amount"), T("عدد الحركات", "Count")], summary.Categories.Select(x => new object?[] { CategoryName(x.Category, ar), x.Amount, x.Count }));
            var legacy = await LegacyExpenses(from, to, token);
            if (legacy.Count > 0) Add(T("صرف سابق غير مرحل", "Legacy paid sources"), [T("المصدر", "Source"), T("معرف السجل", "Record ID"), T("البند", "Category"), T("البيان", "Description"), T("المبلغ", "Amount")], legacy.Select(x => new object?[] { x.Kind, x.Id.ToString(), CategoryName(x.Category, ar), x.Description, x.Amount }));
        }
        if (all || report == "cashbook")
        {
            var entries = await Filter(Entries(), new(from, to, filter.Category, filter.Status, filter.Search)).OrderBy(x => x.TransactionDate).ThenBy(x => x.CreatedAt).Take(ExportLimit + 1).ToListAsync(token);
            Add(T("المصروفات والوارد", "Accounting register"), [T("رقم المستند", "Document"), T("التاريخ", "Date"), T("البند", "Category"), T("المبلغ", "Amount"), T("البيان", "Description"), T("المرجع", "Reference"), T("الموظف", "Employee"), T("الحساب", "Account"), T("الحساب المقابل", "Counter account"), T("الحالة", "Status"), T("رقم القيد", "Journal"), T("المصدر", "Source")], entries.Select(x => { var d = Map(x); return new object?[] { d.DocumentNumber, d.TransactionDate, CategoryName(d.Category, ar), d.Amount, d.Description, d.Reference, d.EmployeeName, d.AccountCode + " · " + d.AccountName, d.CounterAccountName, StatusName(d.Status, ar), d.JournalNumber, d.SourceKey }; }));
        }
        if (all || report == "advances")
        {
            var entries = await AdvancesAsync(to, filter.Search, null, token);
            if (filter.Category == "CARRIED_ADVANCE") entries = entries.Where(x => x.Carried).ToArray();
            if (filter.Status == "OPEN") entries = entries.Where(x => x.Remaining > 0).ToArray();
            if (filter.Status == "SETTLED") entries = entries.Where(x => x.Remaining == 0).ToArray();
            Add(T("السلف والمرحلة", "Advances and balances"), [T("المستند", "Document"), T("الموظف", "Employee"), T("تاريخ السلفة", "Date"), T("المبلغ الأصلي", "Original amount"), T("المسدد", "Repaid"), T("المتبقي", "Outstanding"), T("مرحلة", "Carried")], entries.Select(x => new object?[] { x.DocumentNumber, x.EmployeeName, x.Date, x.Amount, x.Repaid, x.Remaining, x.Carried }));
        }
        if (all || report == "recurring")
        {
            var entries = await RecurringAsync(token);
            Add(T("المصاريف الثابتة", "Recurring expenses"), [T("المصروف", "Expense"), T("المبلغ الشهري", "Monthly amount"), T("يوم الاستحقاق", "Due day"), T("البداية", "From"), T("النهاية", "To"), T("نشط", "Active"), T("ملاحظات", "Notes")], entries.Select(x => new object?[] { x.Name, x.Amount, x.DueDay, x.StartsOn, x.EndsOn, x.IsActive, x.Notes }));
        }
        if (all || report == "salaries")
        {
            var q = db.AccountingEmployeePayrolls.AsNoTracking().Where(x => x.Status != "DELETED" && x.Period.Year * 12 + x.Period.Month >= lowerPeriod && x.Period.Year * 12 + x.Period.Month <= upperPeriod);
            if (filter.PeriodId.HasValue) q = db.AccountingEmployeePayrolls.AsNoTracking().Where(x => x.Status != "DELETED" && x.PeriodId == filter.PeriodId);
            if (!string.IsNullOrWhiteSpace(filter.Status)) q = filter.Status == "READY" ? q.Where(x => x.Status == "DRAFT" || x.Status == "PENDING_REVIEW" || x.Status == "PENDING" || x.Status == "APPROVED") : q.Where(x => x.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.Search)) q = q.Where(x => x.EmployeeName.ToLower().Contains(filter.Search.ToLower()) || x.EmployeeNumber.ToLower().Contains(filter.Search.ToLower()));
            var rows = await q.OrderBy(x => x.EmployeeNumber).Select(x => new { x.Period.Year, x.Period.Month, x.EmployeeNumber, x.EmployeeName, x.DepartmentName, x.BasicSalary, x.Allowances, x.Transportation, x.Commissions, x.Bonuses, x.Deductions, x.OtherAdjustments, x.NetSalary, x.Status }).Take(ExportLimit + 1).ToListAsync(token);
            Add(T("المرتبات", "Salaries"), [T("السنة", "Year"), T("الشهر", "Month"), T("كود الموظف", "Employee code"), T("الموظف", "Employee"), T("القسم", "Department"), T("الأساسي", "Basic"), T("البدلات", "Allowances"), T("الانتقالات", "Transportation"), T("العمولات", "Commissions"), T("المكافآت", "Bonuses"), T("الخصومات", "Deductions"), T("تسويات", "Adjustments"), T("الصافي", "Net salary"), T("الحالة", "Status")], rows.Select(x => new object?[] { x.Year, x.Month, x.EmployeeNumber, x.EmployeeName, x.DepartmentName, x.BasicSalary, x.Allowances, x.Transportation, x.Commissions, x.Bonuses, x.Deductions, x.OtherAdjustments, x.NetSalary, StatusName(x.Status, ar) }));
        }
        if (all || report == "transportation")
        {
            var q = db.AccountingTransportationClaims.AsNoTracking().Where(x => x.ClaimDate >= from && x.ClaimDate <= to);
            if (!string.IsNullOrWhiteSpace(filter.Status)) q = filter.Status == "READY" ? q.Where(x => x.Status == "DRAFT" || x.Status == "PENDING_REVIEW" || x.Status == "PENDING" || x.Status == "APPROVED") : q.Where(x => x.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.Search)) q = q.Where(x => x.Employee.FullName.ToLower().Contains(filter.Search.ToLower()) || x.Employee.EmployeeNumber.ToLower().Contains(filter.Search.ToLower()) || x.Purpose.ToLower().Contains(filter.Search.ToLower()));
            var rows = await q.OrderBy(x => x.ClaimDate).Select(x => new { x.Employee.EmployeeNumber, x.Employee.FullName, x.ClaimDate, x.Amount, x.Purpose, x.Notes, x.Status }).Take(ExportLimit + 1).ToListAsync(token);
            Add(T("الانتقالات", "Transportation"), [T("كود الموظف", "Employee code"), T("الموظف", "Employee"), T("التاريخ", "Date"), T("المبلغ", "Amount"), T("الغرض", "Purpose"), T("ملاحظات", "Notes"), T("الحالة", "Status")], rows.Select(x => new object?[] { x.EmployeeNumber, x.FullName, x.ClaimDate, x.Amount, x.Purpose, x.Notes, StatusName(x.Status, ar) }));
        }
        if (all || report == "collector-commissions")
        {
            var q = db.AccountingCollectorCommissions.AsNoTracking().Where(x => x.PeriodYear * 12 + x.PeriodMonth >= lowerPeriod && x.PeriodYear * 12 + x.PeriodMonth <= upperPeriod);
            if (!string.IsNullOrWhiteSpace(filter.Status)) q = filter.Status == "READY" ? q.Where(x => x.Status == "DRAFT" || x.Status == "PENDING_REVIEW" || x.Status == "PENDING" || x.Status == "APPROVED") : q.Where(x => x.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.Search)) q = q.Where(x => x.CollectorUser.FullName.ToLower().Contains(filter.Search.ToLower()) || (x.CollectorUser.Employee != null && x.CollectorUser.Employee.EmployeeNumber.ToLower().Contains(filter.Search.ToLower())));
            var rows = await q.OrderBy(x => x.CollectorUser.FullName).Select(x => new { x.PeriodYear, x.PeriodMonth, x.CollectorUser.FullName, x.AssignedCasesCount, x.CollectedAmount, x.EligibleAmount, Rule = x.CommissionRule.Code, x.RateApplied, x.CommissionAmount, x.Adjustments, x.FinalCommission, x.Status }).Take(ExportLimit + 1).ToListAsync(token);
            Add(T("عمولات المحصلين", "Collector commissions"), CommissionHeaders(ar), rows.Select(x => new object?[] { x.PeriodYear, x.PeriodMonth, x.FullName, x.AssignedCasesCount, x.CollectedAmount, x.EligibleAmount, x.Rule, x.RateApplied, x.CommissionAmount, x.Adjustments, x.FinalCommission, StatusName(x.Status, ar) }));
        }
        if (all || report == "supervisor-commissions")
        {
            var q = db.AccountingSupervisorCommissions.AsNoTracking().Where(x => x.PeriodYear * 12 + x.PeriodMonth >= lowerPeriod && x.PeriodYear * 12 + x.PeriodMonth <= upperPeriod);
            if (!string.IsNullOrWhiteSpace(filter.Status)) q = filter.Status == "READY" ? q.Where(x => x.Status == "DRAFT" || x.Status == "PENDING_REVIEW" || x.Status == "PENDING" || x.Status == "APPROVED") : q.Where(x => x.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.Search)) q = q.Where(x => x.SupervisorUser.FullName.ToLower().Contains(filter.Search.ToLower()) || (x.SupervisorUser.Employee != null && x.SupervisorUser.Employee.EmployeeNumber.ToLower().Contains(filter.Search.ToLower())));
            var rows = await q.OrderBy(x => x.SupervisorUser.FullName).Select(x => new { x.PeriodYear, x.PeriodMonth, x.SupervisorUser.FullName, x.CollectorCount, x.TeamCollectedAmount, x.EligibleAmount, Rule = x.CommissionRule.Code, x.RateApplied, x.CommissionAmount, x.Adjustments, x.FinalCommission, x.Status }).Take(ExportLimit + 1).ToListAsync(token);
            Add(T("عمولات السوبر فايزر", "Supervisor commissions"), CommissionHeaders(ar, true), rows.Select(x => new object?[] { x.PeriodYear, x.PeriodMonth, x.FullName, x.CollectorCount, x.TeamCollectedAmount, x.EligibleAmount, x.Rule, x.RateApplied, x.CommissionAmount, x.Adjustments, x.FinalCommission, StatusName(x.Status, ar) }));
        }
        if (all || report == "commission-rules")
        {
            var query = db.AccountingCommissionRules.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Category)) query = query.Where(x => x.Scope == filter.Category);
            var rows = await query.OrderBy(x => x.Scope).ThenByDescending(x => x.EffectiveFrom).Take(ExportLimit + 1).ToListAsync(token);
            Add(T("قواعد العمولات", "Commission rules"), [T("الكود", "Code"), T("الاسم", "Name"), T("النطاق", "Scope"), T("الأساس", "Basis"), T("النسبة", "Percent"), T("المبلغ الثابت", "Fixed amount"), T("سارية من", "Effective from"), T("حتى", "To"), T("نشطة", "Active"), T("الإصدار", "Version")], rows.Select(x => new object?[] { x.Code, ar ? x.NameArabic : x.NameEnglish, x.Scope, x.Basis, x.Percentage, x.FixedAmount, x.EffectiveFrom, x.EffectiveTo, x.IsActive, x.Version }));
        }
        if (report == "commission-calculation")
        {
            var previews = new List<AccountingCommissionPreviewRowDto>();
            if (upperPeriod - lowerPeriod > 119) throw new HrValidationException("Limit commission calculation exports to ten years.");
            for (var cursor = new DateOnly(from.Year, from.Month, 1); cursor <= to; cursor = cursor.AddMonths(1))
            {
                var preview = await PreviewCommissionsAsync(cursor.Year, cursor.Month, token);
                previews.AddRange(preview.Rows.Where(x => string.IsNullOrWhiteSpace(filter.Category) || x.Scope == filter.Category).Select(x => x with { Name = $"{cursor:yyyy-MM} / {x.Name}" }));
                foreach (var scope in preview.MissingRuleScopes.Where(x => string.IsNullOrWhiteSpace(filter.Category) || x == filter.Category)) previews.Add(new(scope, Guid.Empty, $"{cursor:yyyy-MM} / No effective rule", 0, 0, 0, "MISSING", "", null, null, false));
            }
            Add(T("معاينة حساب العمولات", "Commission preview"), [T("النطاق", "Scope"), T("الاسم", "Name"), T("الحالات", "Cases"), T("التحصيل المعتمد", "Approved collections"), T("العمولة", "Commission"), T("القاعدة", "Rule"), T("الأساس", "Basis"), T("النسبة", "Rate"), T("مقفلة", "Locked")], previews.Select(x => new object?[] { x.Scope, x.Name, x.CaseCount, x.Collected, x.Commission, x.RuleCode, x.Basis, x.Percentage, x.Locked }));
        }
        if (all || report == "accounts")
        {
            var rows = await finance.GetAccountsAsync(token);
            if (!string.IsNullOrWhiteSpace(filter.Category)) rows = rows.Where(x => x.AccountType == filter.Category).ToArray();
            if (!string.IsNullOrWhiteSpace(filter.Search)) rows = rows.Where(x => (x.Code + x.NameArabic + x.NameEnglish).Contains(filter.Search, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (filter.Status is "ACTIVE" or "INACTIVE") rows = rows.Where(x => x.IsActive == (filter.Status == "ACTIVE")).ToArray();
            Add(T("دليل الحسابات", "Chart of accounts"), [T("الكود", "Code"), T("اسم الحساب", "Account"), T("النوع", "Type"), T("الطبيعة", "Normal balance"), T("مدين", "Debit balance"), T("دائن", "Credit balance"), T("حساب حركة", "Posting account"), T("نقدية الشركة", "Company cash"), T("نشط", "Active"), T("الحساب الأب", "Parent ID")], rows.Select(x => new object?[] { x.Code, ar ? x.NameArabic : x.NameEnglish, x.AccountType, x.NormalBalance, Math.Max(x.Balance, 0), Math.Max(-x.Balance, 0), x.PostingAllowed, x.IsCashAccount, x.IsActive, x.ParentId?.ToString() }));
        }
        if (all || report == "journals")
        {
            var q = db.JournalEntryLines.AsNoTracking().Where(x => x.JournalEntry.PostingDate >= from && x.JournalEntry.PostingDate <= to);
            if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(x => x.JournalEntry.Status == filter.Status);
            var rows = await q.OrderBy(x => x.JournalEntry.PostingDate).ThenBy(x => x.JournalEntryId).ThenBy(x => x.LineNumber).Select(x => new { x.JournalEntry.JournalNumber, x.JournalEntry.PostingDate, x.Account.Code, Name = ar ? x.Account.NameArabic : x.Account.NameEnglish, x.Description, x.BaseDebit, x.BaseCredit, x.JournalEntry.Status }).Take(ExportLimit + 1).ToListAsync(token);
            Add(T("القيود اليومية", "Journal lines"), [T("رقم القيد", "Journal"), T("تاريخ الترحيل", "Posting date"), T("كود الحساب", "Code"), T("الحساب", "Account"), T("البيان", "Description"), T("مدين", "Debit"), T("دائن", "Credit"), T("الحالة", "Status")], rows.Select(x => new object?[] { x.JournalNumber, x.PostingDate, x.Code, x.Name, x.Description, x.BaseDebit, x.BaseCredit, StatusName(x.Status, ar) }));
        }
        if (all || report == "trial-balance")
        {
            var result = await finance.GetTrialBalanceAsync(filter.AsOf ?? to, token);
            Add(T("ميزان المراجعة", "Trial balance"), [T("الكود", "Code"), T("الحساب", "Account"), T("النوع", "Type"), T("حركة مدين", "Debit movement"), T("حركة دائن", "Credit movement"), T("الرصيد", "Balance")], result.Rows.Select(x => new object?[] { x.AccountCode, ar ? x.AccountNameArabic : x.AccountNameEnglish, x.AccountType, x.DebitMovement, x.CreditMovement, x.Balance }));
        }
        if (sheets.Count == 0) throw new HrValidationException("Unknown accounting export report.");
        return Spreadsheet(sheets, $"{from:yyyy-MM-dd} — {to:yyyy-MM-dd} · EGP · {filter.Status} · {filter.Search}", ar);
    }
    private static string[] CommissionHeaders(bool ar, bool supervisor = false) => ar
        ? ["السنة", "الشهر", "الاسم", supervisor ? "المحصلون" : "الحالات", "التحصيل المعتمد", "المبلغ المؤهل", "القاعدة", "النسبة", "العمولة", "تسويات", "الصافي", "الحالة"]
        : ["Year", "Month", "Name", supervisor ? "Collectors" : "Cases", "Approved collections", "Eligible", "Rule", "Rate", "Commission", "Adjustments", "Final", "Status"];
    internal static string CategoryName(string category, bool ar) => (category, ar) switch
    {
        ("PETTY", true) => "مصاريف نثرية", ("FIXED", true) => "مصاريف شهرية ثابتة", ("TRANSPORTATION", true) => "انتقالات", ("ADVANCE", true) => "سلف", ("CARRIED_ADVANCE", true) => "سلف مرحلة", ("ADVANCE_REPAYMENT", true) => "سداد السلف", ("SALARY", true) => "المرتبات", ("BONUS", true) => "مكافآت", ("LEGAL", true) => "مصاريف قضائية", ("COLLECTOR_COMMISSION", true) => "عمولات المحصلين", ("SUPERVISOR_COMMISSION", true) => "عمولات السوبر فايزر", ("PAYROLL_COMMISSION", true) => "عمولات ضمن المرتبات", ("INCOME", true) => "الوارد والإيرادات", ("OTHER", true) => "مصاريف أخرى", _ => category.Replace('_', ' ')
    };
    private static string StatusName(string status, bool ar) => status switch
    {
        "DRAFT" or "PENDING_REVIEW" or "PENDING" or "APPROVED" => ar ? "جاهز للصرف" : "Ready to pay",
        "PENDING_APPROVAL" => ar ? "غير مسجل" : "Not recorded",
        "POSTED" => ar ? "مسجل" : "Recorded", "REVERSED" => ar ? "معكوس" : "Reversed",
        "PAID" => ar ? "تم الصرف" : "Paid", "CANCELLED" => ar ? "ملغي" : "Cancelled",
        "REJECTED" => ar ? "مرفوض" : "Rejected", _ => status
    };
}
