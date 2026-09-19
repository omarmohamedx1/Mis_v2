using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeAccountLinkingTests
{
    [Fact]
    public void OfficeEmployeesAreNeverLinkable()
    {
        var now = DateTimeOffset.UtcNow;
        var office = new Employee("OFF-1", "Office Staff", Guid.NewGuid(), true, now);
        office.ApplyEmployeeProfile(Guid.NewGuid(), EmployeeOperationalRoles.Office, DateOnly.FromDateTime(now.UtcDateTime), null, null, null, null, now);

        Assert.True(EmployeeAccountLinking.IsOffice(office));
        Assert.False(EmployeeAccountLinking.CanLink(office));
        Assert.Throws<InvalidOperationException>(() => EmployeeAccountLinking.EnsureLinkable(office));
    }

    [Fact]
    public void InactiveOrArchivedEmployeesCannotBeLinked()
    {
        var now = DateTimeOffset.UtcNow;
        var inactive = new Employee("COL-1", "Collector", Guid.NewGuid(), false, now);
        Assert.False(EmployeeAccountLinking.CanLink(inactive));
        Assert.Throws<InvalidOperationException>(() => EmployeeAccountLinking.EnsureLinkable(inactive));
    }

    [Fact]
    public void CollectorAndSupervisorJobsSuggestMatchingSystemRoles()
    {
        Assert.Equal(SystemRoleNames.CollectionsCollector, EmployeeAccountLinking.SuggestedSystemRole(EmployeeOperationalRoles.Collector));
        Assert.Equal(SystemRoleNames.CollectionsSupervisor, EmployeeAccountLinking.SuggestedSystemRole(EmployeeOperationalRoles.Supervisor));
        Assert.Null(EmployeeAccountLinking.SuggestedSystemRole(EmployeeOperationalRoles.Office));
        Assert.Null(EmployeeAccountLinking.SuggestedSystemRole(EmployeeOperationalRoles.Admin));
    }
}
