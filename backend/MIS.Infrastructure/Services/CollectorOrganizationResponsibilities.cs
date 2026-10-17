using MIS.Domain.Entities;

namespace MIS.Infrastructure.Services;

public sealed record CollectorOrganizationResponsibility(Guid CollectorId, Guid OrganizationId);

public static class CollectorOrganizationResponsibilities
{
    // HR assignments describe responsibility. Legacy account access is a fallback
    // only for collectors without HR assignments; receiving a case never adds a team membership.
    public static IQueryable<CollectorOrganizationResponsibility> Responsibilities(
        this IQueryable<User> collectors,
        IQueryable<EmployeeOrganizationAssignment> employment,
        IQueryable<CollectionUserAccess> access)
    {
        var employeeLinks = from collector in collectors
                            from assignment in employment
                            where collector.EmployeeId == assignment.EmployeeId
                            select new CollectorOrganizationResponsibility(collector.Id, assignment.OrganizationId);
        var accountLinks = from collector in collectors
                           from grant in access
                           where grant.UserId == collector.Id
                               && !employment.Any(assignment => assignment.EmployeeId == collector.EmployeeId)
                           select new CollectorOrganizationResponsibility(collector.Id, grant.OrganizationId);
        return employeeLinks.Concat(accountLinks).Distinct();
    }
}
