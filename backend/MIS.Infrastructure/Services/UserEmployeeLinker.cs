using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

internal static class UserEmployeeLinker
{
    public static async Task LinkAsync(ApplicationDbContext db, User user, Employee employee, DateTimeOffset now, CancellationToken token)
    {
        if (employee.Department is null)
            await db.Entry(employee).Reference(x => x.Department).LoadAsync(token);
        try { EmployeeAccountLinking.EnsureLinkable(employee); }
        catch (InvalidOperationException ex) { throw new HrValidationException(ex.Message); }
        if (await db.Users.AnyAsync(x => x.EmployeeId == employee.Id && x.Id != user.Id, token))
            throw new HrConflictException("This employee is already linked to another account.");
        try { user.LinkEmployee(employee.Id, now); }
        catch (InvalidOperationException ex) { throw new HrValidationException(ex.Message); }
        if (user.DepartmentId != employee.DepartmentId)
            user.UpdateIdentity(user.FullName, user.Email, employee.DepartmentId, now);
        await EnsureSuggestedRoleAsync(db, user, employee, now, token);
    }

    public static void Unlink(User user, DateTimeOffset now) => user.UnlinkEmployee(now);

    private static async Task EnsureSuggestedRoleAsync(ApplicationDbContext db, User user, Employee employee, DateTimeOffset now, CancellationToken token)
    {
        var roleName = EmployeeAccountLinking.SuggestedSystemRole(employee.OperationalRole);
        if (roleName is null) return;
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == roleName, token);
        if (role is null) return;
        if (user.UserRoles.Any(x => x.RoleId == role.Id)) return;
        if (await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, token)) return;
        db.UserRoles.Add(new UserRole(user.Id, role.Id, now));
    }
}
