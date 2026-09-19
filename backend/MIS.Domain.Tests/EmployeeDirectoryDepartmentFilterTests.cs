using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeDirectoryDepartmentFilterTests
{
    [Fact]
    public void Collection_manager_unit_includes_collectors_and_client_named_departments()
    {
        var collections = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var alexBank = Guid.NewGuid();
        var admin = Guid.NewGuid();

        var ids = EmployeeDirectoryDepartmentFilter.Expand(
            manager,
            new (Guid Id, string Code, string Name, string? NameArabic)[]
            {
                (collections, "COLLECTIONS", "Collections", "التحصيل"),
                (manager, "COLLECTION_MANAGER_UNIT", "Collection Manager", "إدارة التحصيل"),
                (alexBank, "ALEXBANK", "AlexBank", "بنك الإسكندرية"),
                (admin, "ADMIN", "Administration", "الإدارة")
            },
            new[] { ("ALEXBANK", "AlexBank", "بنك الإسكندرية") });

        Assert.Contains(collections, ids);
        Assert.Contains(manager, ids);
        Assert.Contains(alexBank, ids);
        Assert.DoesNotContain(admin, ids);
    }

    [Fact]
    public void Collections_filter_includes_unrepaired_bank_departments()
    {
        var collections = Guid.NewGuid();
        var attijari = Guid.NewGuid();

        var ids = EmployeeDirectoryDepartmentFilter.Expand(
            collections,
            new (Guid Id, string Code, string Name, string? NameArabic)[]
            {
                (collections, "COLLECTIONS", "Collections", "التحصيل"),
                (attijari, "ATTIJARIWAFA", "Attijariwafa Egypt", "التجاري وفا")
            },
            new[] { ("ATTIJARIWAFA", "Attijariwafa Bank Egypt", "التجاري وفا بنك إيجيبت") });

        Assert.Contains(collections, ids);
        Assert.Contains(attijari, ids);
    }
}
