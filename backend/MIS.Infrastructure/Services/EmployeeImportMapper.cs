using System.Globalization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;

namespace MIS.Infrastructure.Services;

internal static class EmployeeImportMapper
{
    internal static readonly string[] Fields = ["EmployeeNumber", "FullName", "Gender", "Department", "Position", "OperationalRole", "NationalId", "DateOfBirth", "WorkStartDate", "FingerprintEnrollmentDate", "WorkEndDate", "Address", "Status"];
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
            var matches = options.Where(option => value.Length > 0 && new[] { option.Name, option.Code, option.Arabic, option.Id.ToString() }
                .Any(name => string.Equals(name?.Trim(), value, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (matches.Length == 1) return matches[0].Id;
            errors.Add($"{field}: must match one existing lookup value.");
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
        var role = Value("OperationalRole").ToUpperInvariant() switch { "محصل" => "COLLECTOR", "إداري" or "اداري" => "ADMIN", "مشرف" => "SUPERVISOR", var value => value };
        return new SaveEmployeeRequest
        {
            EmployeeNumber = Value("EmployeeNumber"), FullName = Value("FullName"), NationalId = Value("NationalId"),
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
