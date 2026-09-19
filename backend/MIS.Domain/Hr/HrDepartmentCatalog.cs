using MIS.Domain.Constants;

namespace MIS.Domain.Hr;

public static class HrDepartmentCatalog
{
    public static bool IsClientOrLegacyDepartment(
        string code,
        string name,
        string? nameArabic,
        IReadOnlyList<(string Code, string NameEnglish, string NameArabic)> organizations)
    {
        if (DepartmentCodes.IsOperationalUnit(code)) return false;
        if (DepartmentCodes.IsLegacyTitleUnit(code)) return true;
        if (organizations.Any(organization =>
            string.Equals(organization.Code, code, StringComparison.OrdinalIgnoreCase)))
            return true;
        return EmployeeDirectoryDepartmentFilter.IsClientNamedDepartment(code, name, nameArabic, organizations);
    }
}
