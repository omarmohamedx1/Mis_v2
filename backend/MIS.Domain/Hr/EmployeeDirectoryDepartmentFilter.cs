using MIS.Domain.Constants;

namespace MIS.Domain.Hr;

public static class EmployeeDirectoryDepartmentFilter
{
    public static IReadOnlyList<Guid> Expand(
        Guid selectedDepartmentId,
        IReadOnlyList<(Guid Id, string Code, string Name, string? NameArabic)> departments,
        IReadOnlyList<(string Code, string NameEnglish, string NameArabic)> organizations)
    {
        var selected = departments.FirstOrDefault(department => department.Id == selectedDepartmentId);
        if (departments.All(department => department.Id != selectedDepartmentId))
            return new[] { selectedDepartmentId };

        var group = DepartmentCodes.DirectoryGroup(selected.Code)
            ?? (MatchesOrganization(selected.Code, selected.Name, selected.NameArabic, organizations)
                ? DepartmentCodes.Collections
                : null);
        if (group is null) return new[] { selectedDepartmentId };

        var ids = departments
            .Where(department =>
                DepartmentCodes.DirectoryGroup(department.Code) == group
                || (group == DepartmentCodes.Collections
                    && MatchesOrganization(department.Code, department.Name, department.NameArabic, organizations)))
            .Select(department => department.Id)
            .Distinct()
            .ToArray();
        return ids.Length == 0 ? new[] { selectedDepartmentId } : ids;
    }

    public static bool IsClientNamedDepartment(
        string code,
        string name,
        string? nameArabic,
        IReadOnlyList<(string Code, string NameEnglish, string NameArabic)> organizations) =>
        !DepartmentCodes.IsOperationalUnit(code)
        && MatchesOrganization(code, name, nameArabic, organizations);

    private static bool MatchesOrganization(
        string? code,
        string? name,
        string? nameArabic,
        IReadOnlyList<(string Code, string NameEnglish, string NameArabic)> organizations) =>
        organizations.Any(organization =>
            HrOrganizationLookup.Mentions(code, organization.Code, organization.NameEnglish, organization.NameArabic)
            || HrOrganizationLookup.Mentions(name, organization.Code, organization.NameEnglish, organization.NameArabic)
            || HrOrganizationLookup.Mentions(nameArabic, organization.Code, organization.NameEnglish, organization.NameArabic));
}
