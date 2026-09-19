using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class HrDepartmentCatalogTests
{
    private static readonly (string Code, string NameEnglish, string NameArabic)[] Organizations =
    [
        ("ALEXBANK", "AlexBank", "بنك الإسكندرية"),
        ("PREMIUM_CARD", "Premium Card", "بريميوم كارد"),
        ("RAYA", "Raya", "راية"),
        ("ATTIJARIWAFA", "Attijariwafa Egypt", "التجاري وفا بنك إيجيبت"),
        ("MNT_HALAN", "Halan", "حالا")
    ];

    [Theory]
    [InlineData("ALEXBANK", "AlexBank", "بنك الإسكندرية")]
    [InlineData("PREMIUM_CARD", "Premium Card", "بريميوم كارد")]
    [InlineData("RAYA", "RAYA", "راية")]
    [InlineData("LOWER", "Lower", "لوَر")]
    public void Banks_and_legacy_units_are_not_hr_departments(string code, string name, string nameArabic)
    {
        Assert.True(HrDepartmentCatalog.IsClientOrLegacyDepartment(code, name, nameArabic, Organizations));
    }

    [Fact]
    public void Operational_units_remain_hr_departments()
    {
        Assert.False(HrDepartmentCatalog.IsClientOrLegacyDepartment("COLLECTIONS", "Collections", "التحصيل", Organizations));
        Assert.False(HrDepartmentCatalog.IsClientOrLegacyDepartment("OFFICE", "Office", "الأوفيس", Organizations));
    }
}
