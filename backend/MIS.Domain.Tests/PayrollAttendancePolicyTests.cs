using MIS.Domain.Hr;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class PayrollAttendancePolicyTests
{
    [Fact]
    public void Friday_visit_clears_one_absence_in_the_same_month()
    {
        var result = PayrollAttendancePolicy.Compute(3000m, 0m, absenceDays: 1, fridayVisitDays: 1, 0);
        Assert.Equal(1, result.OffsetAbsenceDays);
        Assert.Equal(0, result.ChargeableAbsenceDays);
        Assert.Equal(0m, result.TotalDeductions);
        Assert.Equal(3000m, result.NetSalary);
        Assert.Contains(result.Deductions, item => item.Code == "FRIDAY_OFFSET" && item.Amount == 0);
    }

    [Fact]
    public void Extra_absence_beyond_friday_visits_is_deducted()
    {
        var result = PayrollAttendancePolicy.Compute(3000m, 200m, absenceDays: 2, fridayVisitDays: 1, 0);
        Assert.Equal(1, result.ChargeableAbsenceDays);
        Assert.Equal(100m, result.DailyRate);
        Assert.Equal(100m, result.TotalDeductions);
        Assert.Equal(3100m, result.NetSalary);
    }

    [Theory]
    [InlineData(new[] { 0 }, 0)]
    [InlineData(new[] { 15 }, 0)]
    [InlineData(new[] { 59 }, 0)]
    [InlineData(new[] { 40, 40 }, 0)]
    [InlineData(new[] { 60 }, 1)]
    [InlineData(new[] { 180 }, 1)]
    [InlineData(new[] { 60, 60 }, 2)]
    [InlineData(new[] { 60, 180 }, 2)]
    public void Sixty_late_minutes_in_a_day_deduct_one_day_for_that_day(int[] dailyLateMinutes, int expectedDays)
    {
        var result = PayrollAttendancePolicy.Compute(3000m, 0m, 0, 0, dailyLateMinutes);
        Assert.Equal(expectedDays, result.LateDeductionDays);
        Assert.Equal(expectedDays * 100m, result.TotalDeductions);
    }

    [Fact]
    public void Sunday_to_thursday_are_working_days_and_friday_is_the_visit_offset_day()
    {
        Assert.True(PayrollAttendancePolicy.IsWorkingDay(DayOfWeek.Sunday));
        Assert.True(PayrollAttendancePolicy.IsWorkingDay(DayOfWeek.Thursday));
        Assert.False(PayrollAttendancePolicy.IsWorkingDay(DayOfWeek.Friday));
        Assert.False(PayrollAttendancePolicy.IsWorkingDay(DayOfWeek.Saturday));
        Assert.True(PayrollAttendancePolicy.IsFriday(DayOfWeek.Friday));
        Assert.Equal(15, PayrollAttendancePolicy.LateGraceMinutes);
    }
}
