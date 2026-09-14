using MIS.Domain.Constants;
using MIS.Domain.Entities;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class AccountingDomainTests
{
    [Fact]
    public void EmployeePayroll_NetSalary_UsesComponentFormula()
    {
        var payroll = new AccountingEmployeePayroll(
            Guid.NewGuid(), Guid.NewGuid(), "E-100", "Test Employee", "HR", "Officer",
            basicSalary: 5000m, allowances: 500m, compensationSnapshotId: null, createdAt: DateTimeOffset.UtcNow);

        payroll.ApplyComponents(
            transportation: 200m,
            commissions: 150m,
            bonuses: 100m,
            deductions: 50m,
            otherAdjustments: 25m,
            notes: null,
            now: DateTimeOffset.UtcNow);

        Assert.Equal(5925m, payroll.NetSalary);
    }

    [Fact]
    public void EmployeePayroll_Approved_CannotBeSilentlyEdited()
    {
        var payroll = new AccountingEmployeePayroll(
            Guid.NewGuid(), Guid.NewGuid(), "E-101", "Locked Employee", null, null,
            3000m, 0m, null, DateTimeOffset.UtcNow);
        payroll.Approve(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            payroll.ApplyComponents(10m, 0m, 0m, 0m, 0m, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CommissionRule_PercentOfCollected_RoundsAwayFromZero()
    {
        var rule = new AccountingCommissionRule(
            "COL-5", "عمولة 5%", "Collector 5%", AccountingValues.CommissionScopes.Collector,
            AccountingValues.CommissionBases.PercentOfCollected, 5m, null, new DateOnly(2026, 1, 1),
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(12.35m, rule.Calculate(247m));
    }
}
