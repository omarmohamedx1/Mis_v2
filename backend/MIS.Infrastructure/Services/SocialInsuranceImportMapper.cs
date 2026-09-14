using System.Globalization;
using MIS.Application.DTOs.Hr;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Services;

internal static class SocialInsuranceImportMapper
{
    internal static readonly string[] Fields = ["EmployeeNumber", "EmployeeName", "NationalId", "EmployeeId", "SocialInsuranceNumber", "InsuranceStartDate", "InsuranceEndDate", "InsurableSalary", "InsuranceStatus", "InsuranceOffice", "ReferenceNumber", "Notes"];
    internal sealed record EmployeeMatch(Guid Id, string Number, string? NationalId, string Name);
    internal static SocialInsuranceRecord Create(SocialInsuranceDto r)
    {
        if (r.InsuranceStatus != "Ended" && r.InsuranceEndDate != null) throw new ArgumentException("End date requires Ended status. / تاريخ النهاية يتطلب حالة منتهي");
        var record = new SocialInsuranceRecord(r.EmployeeId, r.SocialInsuranceNumber, r.InsuranceStartDate, r.InsurableSalary,
            r.InsuranceStatus == "Ended" ? "Insured" : r.InsuranceStatus, r.InsuranceOffice, r.ReferenceNumber, r.Notes);
        if (r.InsuranceStatus == "Ended") record.End(r.InsuranceEndDate ?? throw new ArgumentException("End date is required. / تاريخ النهاية مطلوب"));
        return record;
    }
    internal static SocialInsuranceImportRow Map(int row, Dictionary<string, string> values, string? format,
        IReadOnlyCollection<EmployeeMatch> employees, HashSet<Guid> activeEmployees, HashSet<string> activeNumbers)
    {
        string V(string key) => values.GetValueOrDefault(key)?.Trim() ?? "";
        var errors = new List<string>();
        DateOnly? Date(string key)
        {
            if (V(key) == "") return null;
            if (EmployeeImportMapper.TryDate(V(key), format, out var date)) return date;
            errors.Add(key + ": Invalid date / تاريخ غير صالح"); return null;
        }
        var matches = employees.Where(e => V("EmployeeNumber") != "" ? string.Equals(e.Number, V("EmployeeNumber"), StringComparison.OrdinalIgnoreCase)
            : V("NationalId") != "" ? e.NationalId == V("NationalId") : Guid.TryParse(V("EmployeeId"), out var id) && e.Id == id).ToArray();
        var employee = matches.Length == 1 ? matches[0] : null;
        if (employee == null) errors.Add("Unmatched Employee / موظف غير موجود");
        else if ((V("NationalId") != "" && employee.NationalId != V("NationalId")) || (V("EmployeeId") != "" && !string.Equals(employee.Id.ToString(), V("EmployeeId"), StringComparison.OrdinalIgnoreCase)))
            errors.Add("Employee identifiers conflict. / معرفات الموظف غير متطابقة");
        var status = V("InsuranceStatus").ToLowerInvariant() switch {
            "insured" or "مؤمن عليه" or "مؤمن" => "Insured", "notinsured" or "not insured" or "غير مؤمن" or "غير مؤمن عليه" => "NotInsured",
            "suspended" or "موقوف" or "معلق" => "Suspended", "ended" or "منتهي" => "Ended", _ => "Invalid" };
        if (!decimal.TryParse(V("InsurableSalary"), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var salary)) errors.Add("Invalid salary; use a decimal number without separators. / الأجر غير صالح؛ استخدم رقماً دون فواصل آلاف");
        var record = new SocialInsuranceDto(Guid.Empty, employee?.Id ?? Guid.Empty, SocialInsuranceRecord.NormalizeNumber(V("SocialInsuranceNumber")), Date("InsuranceStartDate"), Date("InsuranceEndDate"), salary, status, V("InsuranceOffice"), V("ReferenceNumber"), V("Notes"));
        try { Create(record with { EmployeeId = employee?.Id ?? Guid.NewGuid() }); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { errors.Add(ex.Message); }
        var validation = errors.Count > 0 ? "Error" : "Ready";
        if (employee != null && activeEmployees.Contains(employee.Id)) { errors.Add("Existing active insurance record / يوجد سجل تأمين نشط بالفعل"); validation = "Existing"; }
        else if (status != "Ended" && activeNumbers.Contains(record.SocialInsuranceNumber)) { errors.Add("Insurance number already assigned to an active record / الرقم التأميني مستخدم في سجل نشط"); validation = "Existing"; }
        if (validation == "Ready" && V("EmployeeName") != "" && !string.Equals(V("EmployeeName"), employee?.Name, StringComparison.OrdinalIgnoreCase)) {
            validation = "Warning"; errors.Add("Name differs; verify the matched employee. / الاسم مختلف؛ تحقق من الموظف المطابق");
        }
        return new(row, record, employee?.Number ?? V("EmployeeNumber"), employee?.Name ?? "", V("EmployeeName"), validation, errors);
    }
}
