using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeProfileLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Complete_profile_validates_dates_and_operational_role()
    {
        var employee = new Employee("E-200", "Ahmed Ali", Guid.NewGuid(), true, Now);
        Assert.Throws<ArgumentException>(() => employee.ApplyEmployeeProfile(Guid.NewGuid(), "COLLECTOR", new DateOnly(2026, 8, 1), null, null, null, new DateOnly(2026, 7, 31), Now));
        Assert.Throws<ArgumentException>(() => employee.ApplyEmployeeProfile(Guid.NewGuid(), "SECURITY_ADMIN", new DateOnly(2026, 8, 1), null, null, null, null, Now));
        Assert.Throws<ArgumentException>(() => employee.ApplyEmployeeProfile(Guid.NewGuid(), "ADMIN", new DateOnly(2026, 8, 1), null, new DateOnly(2027, 1, 1), null, null, Now));
        employee.ApplyEmployeeProfile(Guid.NewGuid(), "OFFICE", new DateOnly(2026, 8, 1), null, null, null, null, Now);
        Assert.Equal("OFFICE", employee.OperationalRole);
    }

    [Fact]
    public void Work_number_and_package_type_are_optional_and_trimmed()
    {
        var employee = new Employee("E-WN", "Ahmed Ali", Guid.NewGuid(), true, Now);
        employee.UpdateWorkAssignment("  W-12 ", "  Field ", Now);
        Assert.Equal("W-12", employee.WorkNumber);
        Assert.Equal("Field", employee.PackageType);
        employee.UpdateWorkAssignment(" ", null, Now.AddMinutes(1));
        Assert.Null(employee.WorkNumber);
        Assert.Null(employee.PackageType);
    }

    [Fact]
    public void Canonical_name_fills_the_matching_localized_field()
    {
        var arabic = new Employee("E-AR", "محمد علي", Guid.NewGuid(), true, Now);
        Assert.Equal("محمد علي", arabic.FullName);
        Assert.Equal("محمد علي", arabic.FullNameArabic);
        Assert.Null(arabic.FullNameEnglish);

        var english = new Employee("E-EN", "Mona Adel", Guid.NewGuid(), true, Now);
        Assert.Equal("Mona Adel", english.FullNameEnglish);
        Assert.Null(english.FullNameArabic);
        english.UpdateLocalizedNames("منى عادل", "Mona Adel", Now.AddMinutes(1));
        Assert.Equal("منى عادل", english.FullNameArabic);
        Assert.Equal("Mona Adel", english.FullNameEnglish);
    }

    [Fact]
    public void Bilingual_canonical_name_splits_into_arabic_and_english()
    {
        var employee = new Employee("E-BI", "حبيبة محمد Habiba Mohamed", Guid.NewGuid(), true, Now);
        Assert.Equal("حبيبة محمد", employee.FullNameArabic);
        Assert.Equal("Habiba Mohamed", employee.FullNameEnglish);
        Assert.Equal("حبيبة محمد Habiba Mohamed", employee.FullName);
    }

    [Fact]
    public void Canonical_name_fills_arabic_even_when_english_is_also_provided()
    {
        var employee = new Employee("E-BOTH", "أيمن مجدي احمد مرسي", Guid.NewGuid(), true, Now);
        employee.UpdateLocalizedNames(null, "Ayman Magdy", Now.AddMinutes(1));
        Assert.Equal("أيمن مجدي احمد مرسي", employee.FullNameArabic);
        Assert.Equal("Ayman Magdy", employee.FullNameEnglish);
    }

    [Fact]
    public void National_id_requires_exactly_fourteen_digits()
    {
        var employee = new Employee("E-202", "Sara Ali", Guid.NewGuid(), true, Now);
        Assert.Throws<ArgumentException>(() => employee.SetNationalId("2980101123456", Now));
        Assert.Throws<ArgumentException>(() => employee.SetNationalId("2980101123456A", Now));
        employee.SetNationalId("29801011234567", Now);
        Assert.Equal("29801011234567", employee.NationalId);
    }

    [Fact]
    public void Archive_and_restore_preserve_employment_information()
    {
        var employee = new Employee("E-201", "Mona Adel", Guid.NewGuid(), false, Now);
        var endDate = new DateOnly(2026, 8, 15);
        employee.ApplyEmployeeProfile(Guid.NewGuid(), "SUPERVISOR", new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 2), new DateOnly(1990, 5, 3), "Cairo", endDate, Now);
        employee.Archive("Employee left company", Guid.NewGuid(), Now.AddMinutes(1));
        Assert.True(employee.IsArchived);
        Assert.Equal(endDate, employee.TerminationDate);
        employee.Restore(Now.AddMinutes(2));
        Assert.False(employee.IsArchived);
        Assert.Equal(endDate, employee.TerminationDate);
        Assert.Equal("SUPERVISOR", employee.OperationalRole);
    }
}
