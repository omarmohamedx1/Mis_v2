using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class HrOrganizationLookupTests
{
    [Fact]
    public void Bank_named_department_matches_that_organization_only()
    {
        var alexDepartmentId = Guid.NewGuid();
        var collectionsDepartmentId = Guid.NewGuid();
        var departments = new (Guid Id, string Code, string Name, string? NameArabic)[]
        {
            (collectionsDepartmentId, "COLLECTIONS", "Collections", "التحصيل"),
            (alexDepartmentId, "ALEXBANK", "AlexBank", "بنك الإسكندرية")
        };

        var matched = departments
            .Where(department =>
                HrOrganizationLookup.Matches(department.Code, "ALEXBANK", "AlexBank", "بنك الإسكندرية")
                || HrOrganizationLookup.Matches(department.Name, "ALEXBANK", "AlexBank", "بنك الإسكندرية")
                || HrOrganizationLookup.Matches(department.NameArabic, "ALEXBANK", "AlexBank", "بنك الإسكندرية"))
            .Select(department => department.Id)
            .ToArray();

        Assert.Contains(alexDepartmentId, matched);
        Assert.DoesNotContain(collectionsDepartmentId, matched);
    }

    [Fact]
    public void Position_title_mentioning_bank_matches_that_organization()
    {
        Assert.True(HrOrganizationLookup.Mentions("محصل HSBC", "HSBC", "HSBC Egypt", "HSBC مصر"));
        Assert.True(HrOrganizationLookup.Mentions("HSBC Collector", "HSBC", "HSBC Egypt", "HSBC مصر"));
        Assert.False(HrOrganizationLookup.Mentions("Collector", "HSBC", "HSBC Egypt", "HSBC مصر"));
        Assert.False(HrOrganizationLookup.Mentions("محصل HSBC", "ALEXBANK", "AlexBank", "بنك الإسكندرية"));
    }
}
