using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeOperationalRolesTests
{
    [Theory]
    [InlineData("COLLECTOR", "محصل", "Collector", "COLLECTOR")]
    [InlineData("SUPERVISOR", "مشرف تحصيل", "Collections Supervisor", "SUPERVISOR")]
    [InlineData("OFFICE_BOY", "عامل خدمات", "Office Boy", "OFFICE")]
    [InlineData("ACCOUNTANT", "محاسب", "Accountant", "ADMIN")]
    public void Infers_system_class_from_job_title(string code, string arabic, string english, string expected)
    {
        Assert.Equal(expected, EmployeeOperationalRoles.InferFromPosition(code, english, arabic));
    }

    [Fact]
    public void Empty_requested_role_uses_job_title()
    {
        Assert.True(EmployeeOperationalRoles.TryResolve(null, "COLLECTOR", "Collector", "محصل", out var role));
        Assert.Equal("COLLECTOR", role);
    }

    [Fact]
    public void Invalid_requested_role_is_rejected()
    {
        Assert.False(EmployeeOperationalRoles.TryResolve("UNKNOWN", "COLLECTOR", "Collector", "محصل", out _));
    }

    [Theory]
    [InlineData("محصل", "COLLECTOR")]
    [InlineData("مشرف", "SUPERVISOR")]
    [InlineData("اوفيس", "OFFICE")]
    [InlineData("أوفيس", "OFFICE")]
    [InlineData("محاسب", "ADMIN")]
    public void Resolves_arabic_role_labels(string requested, string expected)
    {
        Assert.True(EmployeeOperationalRoles.TryResolve(requested, "ACCOUNTANT", "Accountant", "محاسب", out var role));
        Assert.Equal(expected, role);
    }
}
