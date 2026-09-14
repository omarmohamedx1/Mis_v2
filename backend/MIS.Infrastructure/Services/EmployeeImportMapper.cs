using System.Globalization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;

namespace MIS.Infrastructure.Services;

internal static class EmployeeImportMapper
{
    internal static readonly string[] Fields = ["MobileNumber", "EmployeeNumber", "FullName", "Gender", "Department", "Position", "OperationalRole", "NationalId", "DateOfBirth", "WorkStartDate", "FingerprintEnrollmentDate", "WorkEndDate", "Address", "Status"];
    internal sealed record Lookup(Guid Id, string Name, string Code, string? Arabic);

    internal static SaveEmployeeRequest Map(Dictionary<string, string> values, string? dateFormat,
        IReadOnlyCollection<Lookup> departments, IReadOnlyCollection<Lookup> positions, List<string> errors)
    {
        string Value(string field) => values.GetValueOrDefault(field)?.Trim() ?? "";
        DateOnly? Date(string field)
        {
            var text = Value(field);
            if (text.Length == 0) return null;
            if (TryDate(text, dateFormat, out var date)) return date;
            errors.Add($"{field}: invalid date.");
            return null;
        }
        Guid LookupId(string field, IReadOnlyCollection<Lookup> options)
        {
            var value = Value(field);
            if (value.Length == 0)
            {
                errors.Add($"{field}: must match one existing lookup value.");
                return Guid.Empty;
            }

            static string Norm(string? text) => string.Join(' ', (text ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var normalized = Norm(value);
            var matches = options.Where(option => new[] { option.Name, option.Code, option.Arabic, option.Id.ToString() }
                .Any(name => string.Equals(Norm(name), normalized, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (matches.Length == 1) return matches[0].Id;
            errors.Add(field is "Department" or "Position"
                ? $"{field} not found: {value}"
                : $"{field}: must match one existing lookup value.");
            return Guid.Empty;
        }
        var gender = Value("Gender").ToLowerInvariant() switch { "" => null, "male" or "m" or "ذكر" => "Male", "female" or "f" or "أنثى" or "انثى" => "Female", _ => "Invalid" };
        if (gender == "Invalid") errors.Add("Gender: invalid value.");
        var status = Value("Status").ToLowerInvariant() switch
        {
            "" => null, "active" or "نشط" => "Active", "inactive" or "غير نشط" => "Inactive",
            "onleave" or "on leave" or "في إجازة" => "OnLeave", "suspended" or "موقوف" => "Suspended",
            "terminated" or "منتهي" => "Terminated", _ => "Invalid"
        };
        if (status == "Invalid") errors.Add("Status: invalid value.");
        var roleRaw = Value("OperationalRole").Trim();
        var roleKey = string.Join(' ', roleRaw.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        // Title/Position is a separate field. Sheets often omit Role; default to ADMIN (not a login User role).
        var role = roleKey switch
        {
            "" => "ADMIN",
            "محصل" or "collector" or "collections collector" => "COLLECTOR",
            "مشرف" or "supervisor" or "collections supervisor" => "SUPERVISOR",
            "إداري" or "اداري" or "admin" or "administrator"
                or "data entry" or "dataentry" or "إدخال البيانات" or "ادخال البيانات"
                or "accounting" or "accountant" or "الحسابات" or "محاسب"
                or "hr" or "hr officer" or "hr manager" or "human resources" or "الموارد البشرية"
                or "legal" or "الشؤون القانونية" => "ADMIN",
            _ => roleRaw.ToUpperInvariant()
        };
        return new SaveEmployeeRequest
        {
            MobileNumber = Value("MobileNumber"), EmployeeNumber = Value("EmployeeNumber"), FullName = Value("FullName"), NationalId = Value("NationalId"),
            Gender = gender, DepartmentId = LookupId("Department", departments), PositionId = LookupId("Position", positions),
            OperationalRole = role, WorkStartDate = Date("WorkStartDate"), DateOfBirth = Date("DateOfBirth"),
            FingerprintEnrollmentDate = Date("FingerprintEnrollmentDate"), WorkEndDate = Date("WorkEndDate"),
            Address = Value("Address"), Status = status, IsActive = status is null or "Active"
        };
    }

    internal static bool TryDate(string value, string? format, out DateOnly date)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            try { if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)) return true; }
            catch (FormatException) { throw new HrValidationException("Invalid date format."); }
        }
        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var native))
        { date = DateOnly.FromDateTime(native); return true; }
        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MMM-yy", "d-MMM-yy", "dd-MMM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)) return true;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and <= 2958465)
        { date = DateOnly.FromDateTime(DateTime.FromOADate(serial)); return true; }
        date = default; return false;
    }
}
