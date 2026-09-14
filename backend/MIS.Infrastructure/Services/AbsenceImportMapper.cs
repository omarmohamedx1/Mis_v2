using MIS.Application.DTOs.Hr;
using MIS.Domain.Constants;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Services;

internal static class AbsenceImportMapper
{
    internal static readonly string[] Fields =
    [
        "EmployeeNumber", "EmployeeName", "NationalId", "MobileNumber",
        "AbsenceDate", "AbsenceType", "Reason", "Notes", "Status"
    ];

    internal sealed record EmployeeMatch(Guid Id, string Number, string? NationalId, string? MobileNumber, string Name, string? NameArabic, string? NameEnglish, DateOnly? HireDate, DateOnly? TerminationDate);

    internal static EmployeeAbsence Create(AbsenceImportRecord record, DateTimeOffset now) =>
        new(record.EmployeeId, record.AbsenceDate, record.Reason, record.Status, record.Notes, now);

    internal static AbsenceImportRow Map(
        int row,
        Dictionary<string, string> values,
        string? format,
        IReadOnlyCollection<EmployeeMatch> employees,
        HashSet<(Guid EmployeeId, DateOnly Date)> existingAbsences,
        HashSet<(Guid EmployeeId, DateOnly Date)> approvedLeaves,
        HashSet<(Guid EmployeeId, DateOnly Date)> conflictingAttendance,
        HashSet<(Guid EmployeeId, DateOnly Date)> approvedExcuses,
        HashSet<(Guid EmployeeId, DateOnly Date)> batchKeys,
        DateOnly cairoToday)
    {
        string V(string key) => values.GetValueOrDefault(key)?.Trim() ?? "";
        var errors = new List<string>();
        var existing = false;

        DateOnly? ParseDate(string key)
        {
            if (V(key) == "") return null;
            if (EmployeeImportMapper.TryDate(V(key), format, out var date)) return date;
            errors.Add("Invalid absence date. / تاريخ الغياب غير صالح");
            return null;
        }

        var employee = MatchEmployee(V("EmployeeNumber"), V("NationalId"), V("MobileNumber"), V("EmployeeName"), employees, errors);
        var absenceDate = ParseDate("AbsenceDate");
        if (absenceDate is null && V("AbsenceDate") == "") errors.Add("Absence date is required. / تاريخ الغياب مطلوب");

        var type = NormalizeType(V("AbsenceType"), errors);
        var status = NormalizeStatus(V("Status"), errors);
        var reason = string.IsNullOrWhiteSpace(V("Reason")) ? null : V("Reason");
        var notes = string.IsNullOrWhiteSpace(V("Notes")) ? null : V("Notes");
        if (reason?.Length > 500) errors.Add("Reason is too long. / السبب طويل جداً");
        if (notes?.Length > 2000) errors.Add("Notes are too long. / الملاحظات طويلة جداً");

        if (absenceDate is { } date)
        {
            if (date > cairoToday) errors.Add("Absence cannot be recorded for a future date.");
            if (employee is not null)
            {
                if ((employee.HireDate.HasValue && date < employee.HireDate.Value) ||
                    (employee.TerminationDate.HasValue && date > employee.TerminationDate.Value))
                    errors.Add("Absence date must fall within the employee employment period.");
                var key = (employee.Id, date);
                if (existingAbsences.Contains(key) || batchKeys.Contains(key))
                {
                    errors.Add("Absence already exists / غياب مسجل بالفعل");
                    existing = true;
                }
                if (approvedLeaves.Contains(key))
                    errors.Add("An absence case cannot be recorded on an approved leave date.");
                if (conflictingAttendance.Contains(key))
                    errors.Add("Recorded attendance conflicts with this absence date. Resolve the attendance record first.");
                if (approvedExcuses.Contains(key))
                    errors.Add("An approved excuse or field duty already covers this date. / يوجد عذر أو مأمورية معتمدة في هذا التاريخ");
            }
        }

        var record = new AbsenceImportRecord(
            employee?.Id ?? Guid.Empty,
            absenceDate ?? default,
            type,
            status,
            reason,
            notes);

        if (errors.Count == 0 && employee is not null && absenceDate is not null)
        {
            try { Create(record, DateTimeOffset.UtcNow); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { errors.Add(ex.Message); }
        }

        var validation = errors.Count == 0 ? "Ready" : existing && errors.Count == 1 ? "Existing" : "Error";

        if (validation == "Ready" && V("EmployeeName") != "" && employee is not null && !NameMatches(employee, V("EmployeeName")))
        {
            validation = "Warning";
            errors.Add("Name differs; verify the matched employee. / الاسم مختلف؛ تحقق من الموظف المطابق");
        }

        if (validation is "Ready" or "Warning" && employee is not null && absenceDate is not null)
            batchKeys.Add((employee.Id, absenceDate.Value));

        return new(
            row,
            record,
            employee?.Number ?? V("EmployeeNumber"),
            employee is null ? "" : employee.NameArabic ?? employee.NameEnglish ?? employee.Name,
            employee?.MobileNumber ?? (V("MobileNumber") == "" ? null : V("MobileNumber")),
            V("EmployeeName"),
            validation,
            errors);
    }

    private static EmployeeMatch? MatchEmployee(
        string number,
        string nationalId,
        string mobile,
        string name,
        IReadOnlyCollection<EmployeeMatch> employees,
        List<string> errors)
    {
        if (number != "")
        {
            var matches = employees.Where(e => string.Equals(e.Number, number, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Multiple employees matched. / تطابق أكثر من موظف"); return null; }
            errors.Add("Unmatched Employee / موظف غير موجود");
            return null;
        }

        if (nationalId != "")
        {
            var matches = employees.Where(e => e.NationalId == nationalId).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Multiple employees matched. / تطابق أكثر من موظف"); return null; }
            errors.Add("Unmatched Employee / موظف غير موجود");
            return null;
        }

        if (mobile != "")
        {
            var matches = employees.Where(e =>
                (!string.IsNullOrWhiteSpace(e.MobileNumber) && string.Equals(e.MobileNumber, mobile, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Multiple employees matched. / تطابق أكثر من موظف"); return null; }
            errors.Add("Unmatched Employee / موظف غير موجود");
            return null;
        }

        if (name != "")
        {
            var matches = employees.Where(e => NameMatches(e, name)).ToArray();
            if (matches.Length == 1) return matches[0];
            if (matches.Length > 1) { errors.Add("Multiple employees matched by name. / تطابق أكثر من موظف بالاسم"); return null; }
            errors.Add("Unmatched Employee / موظف غير موجود");
            return null;
        }

        errors.Add("Map an employee identifier. / يجب تعيين معرف الموظف");
        return null;
    }

    private static bool NameMatches(EmployeeMatch employee, string name) =>
        string.Equals(employee.Name, name, StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(employee.NameArabic) && string.Equals(employee.NameArabic, name, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(employee.NameEnglish) && string.Equals(employee.NameEnglish, name, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeType(string value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return AbsenceValues.AbsentType;
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "absent" or "غياب" or "غائب") return AbsenceValues.AbsentType;
        errors.Add("Absence type is invalid.");
        return AbsenceValues.AbsentType;
    }

    private static string NormalizeStatus(string value, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return AbsenceValues.PendingStatus;
        var normalized = value.Trim().ToLowerInvariant() switch
        {
            "pending" or "قيد المراجعة" or "معلق" => AbsenceValues.PendingStatus,
            "excused" or "بعذر" or "معذور" => AbsenceValues.ExcusedStatus,
            "unexcused" or "بدون عذر" or "غير معذور" => AbsenceValues.UnexcusedStatus,
            _ => null
        };
        if (normalized is null)
        {
            errors.Add("Absence status is invalid.");
            return AbsenceValues.PendingStatus;
        }
        return normalized;
    }
}
