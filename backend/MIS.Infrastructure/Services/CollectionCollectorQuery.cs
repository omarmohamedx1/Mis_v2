using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

/// <summary>
/// Eligible collectors are active operational users in Collections: the collector role,
/// a linked HR collector job, or a Collections-department login that is not a management role.
/// Office accounts and office employees are never treated as collectors.
/// </summary>
public static class CollectionCollectorQuery
{
    public static IQueryable<User> EligibleCollectors(this IQueryable<User> users) =>
        users.Where(x => x.IsActive
            && x.Department.Code != DepartmentCodes.Office
            && (x.EmployeeId == null || (x.Employee!.OperationalRole != EmployeeOperationalRoles.Office && x.Employee.Department.Code != DepartmentCodes.Office))
            && (
                x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector)
                || (x.Employee != null && x.Employee.IsActive && !x.Employee.IsArchived && x.Employee.OperationalRole == EmployeeOperationalRoles.Collector)
                || (x.Department.Code == DepartmentCodes.Collections
                    && (x.Employee == null || x.Employee.OperationalRole != EmployeeOperationalRoles.Supervisor)
                    && !x.UserRoles.Any(r =>
                        r.Role.Name == SystemRoleNames.Admin
                        || r.Role.Name == SystemRoleNames.CollectionsSupervisor
                        || r.Role.Name == SystemRoleNames.CollectionsOperationsManager
                        || r.Role.Name == SystemRoleNames.CollectionsReviewer
                        || r.Role.Name == SystemRoleNames.CollectionsAuditor
                        || r.Role.Name == SystemRoleNames.CollectionsClientViewer
                        || r.Role.Name == SystemRoleNames.HrManager
                        || r.Role.Name == SystemRoleNames.HrOfficer
                        || r.Role.Name == SystemRoleNames.LegalOfficer
                        || r.Role.Name == SystemRoleNames.DataEntry)))).Distinct();

    public static IQueryable<User> CollectorIdentities(this IQueryable<User> users) =>
        users.Where(x =>
            x.Department.Code != DepartmentCodes.Office
            && (x.EmployeeId == null || (x.Employee!.OperationalRole != EmployeeOperationalRoles.Office && x.Employee.Department.Code != DepartmentCodes.Office))
            && (
                x.UserRoles.Any(r => r.Role.Name == SystemRoleNames.CollectionsCollector)
                || (x.Employee != null && x.Employee.OperationalRole == EmployeeOperationalRoles.Collector)
                || x.Department.Code == DepartmentCodes.Collections));

    public static IQueryable<User> ForSupervisorScope(this IQueryable<User> collectors, ApplicationDbContext db, Guid supervisorId, bool global)
    {
        if (global) return collectors;
        return collectors.Where(x =>
            !db.CollectionTeamMembers.Any(m => m.IsActive && m.Team.IsActive && m.Team.SupervisorId == supervisorId)
            || db.CollectionTeamMembers.Any(m => m.UserId == x.Id && m.IsActive && m.Team.IsActive && m.Team.SupervisorId == supervisorId));
    }

    public static IQueryable<CollectorIdentityCandidate> SelectIdentityCandidates(this IQueryable<User> users) =>
        users.Select(x => new CollectorIdentityCandidate(
            x.Id,
            x.FullName,
            x.Username,
            x.Email,
            x.Employee == null ? null : x.Employee.EmployeeNumber,
            x.Employee == null ? null : x.Employee.FullName,
            x.Employee == null ? null : x.Employee.FullNameArabic,
            x.Employee == null ? null : x.Employee.FullNameEnglish));
}
