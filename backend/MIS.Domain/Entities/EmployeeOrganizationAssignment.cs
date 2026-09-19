namespace MIS.Domain.Entities;

/// <summary>
/// An HR employment assignment to a client organization. This is deliberately
/// independent from CollectionUserAccess: job responsibility must exist even
/// when the employee has no system account or collection-module permission.
/// </summary>
public sealed class EmployeeOrganizationAssignment
{
    private EmployeeOrganizationAssignment() { }

    public EmployeeOrganizationAssignment(Guid employeeId, Guid organizationId, bool isPrimary, DateTimeOffset assignedAt)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee is required.", nameof(employeeId));
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (assignedAt == default) throw new ArgumentException("Assignment timestamp is required.", nameof(assignedAt));

        EmployeeId = employeeId;
        OrganizationId = organizationId;
        IsPrimary = isPrimary;
        AssignedAt = assignedAt;
    }

    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public Guid OrganizationId { get; private set; }
    public ClientOrganization Organization { get; private set; } = null!;
    public bool IsPrimary { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
}
