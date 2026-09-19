using MIS.Domain.Constants;
using MIS.Domain.Entities;

namespace MIS.Domain.Hr;

public static class EmployeeAccountLinking
{
    public static bool IsOffice(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        return string.Equals(employee.OperationalRole, EmployeeOperationalRoles.Office, StringComparison.OrdinalIgnoreCase)
            || string.Equals(employee.Department?.Code, DepartmentCodes.Office, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanLink(Employee employee) =>
        employee is { IsActive: true, IsArchived: false } && !IsOffice(employee);

    public static void EnsureLinkable(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        if (!employee.IsActive || employee.IsArchived)
            throw new InvalidOperationException("Only an active employee can be linked to a user account.");
        if (IsOffice(employee))
            throw new InvalidOperationException("Office employees cannot be linked to operational system accounts.");
    }

    public static string? SuggestedSystemRole(string? operationalRole) =>
        operationalRole?.Trim().ToUpperInvariant() switch
        {
            EmployeeOperationalRoles.Collector => SystemRoleNames.CollectionsCollector,
            EmployeeOperationalRoles.Supervisor => SystemRoleNames.CollectionsSupervisor,
            _ => null
        };
}
