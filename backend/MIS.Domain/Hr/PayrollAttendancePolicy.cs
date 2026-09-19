namespace MIS.Domain.Hr;

public sealed record NetSalaryDeduction(
    string Code,
    int Units,
    decimal Amount,
    string ReasonEnglish,
    string ReasonArabic);

public sealed record NetSalaryComputation(
    decimal BasicSalary,
    decimal Allowances,
    decimal GrossSalary,
    decimal DailyRate,
    int AbsenceDays,
    int FridayVisitDays,
    int OffsetAbsenceDays,
    int ChargeableAbsenceDays,
    int LateMinutes,
    int LateRemainderMinutes,
    int LateDeductionDays,
    IReadOnlyList<NetSalaryDeduction> Deductions,
    decimal TotalDeductions,
    decimal NetSalary);

public static class PayrollAttendancePolicy
{
    public const decimal MonthDivisor = 30m;
    public const int LateGraceMinutes = 15;
    public const int LateMinutesPerDeductedDay = 60;

    public static bool IsWorkingDay(DayOfWeek day) =>
        day is DayOfWeek.Sunday or DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday;

    public static bool IsFriday(DayOfWeek day) => day == DayOfWeek.Friday;

    public static decimal DailyRate(decimal basicSalary) =>
        decimal.Round(Math.Max(0, basicSalary) / MonthDivisor, 2, MidpointRounding.AwayFromZero);

    public static int LateDeductionDaysForDay(int lateMinutes) =>
        Math.Max(0, lateMinutes) >= LateMinutesPerDeductedDay ? 1 : 0;

    public static NetSalaryComputation Compute(
        decimal basicSalary,
        decimal allowances,
        int absenceDays,
        int fridayVisitDays,
        params int[] dailyLateMinutes)
    {
        if (basicSalary < 0 || allowances < 0)
            throw new ArgumentOutOfRangeException(nameof(basicSalary), "Salary components cannot be negative.");

        absenceDays = Math.Max(0, absenceDays);
        fridayVisitDays = Math.Max(0, fridayVisitDays);
        dailyLateMinutes ??= [];

        var lateMinutes = dailyLateMinutes.Sum(item => Math.Max(0, item));
        var lateDeductionDays = dailyLateMinutes.Sum(LateDeductionDaysForDay);
        var lateRemainderMinutes = dailyLateMinutes.Sum(item => item > 0 && item < LateMinutesPerDeductedDay ? item : 0);
        var dailyRate = DailyRate(basicSalary);
        var offsetAbsenceDays = Math.Min(absenceDays, fridayVisitDays);
        var chargeableAbsenceDays = absenceDays - offsetAbsenceDays;
        var lines = new List<NetSalaryDeduction>();

        if (chargeableAbsenceDays > 0)
        {
            lines.Add(new NetSalaryDeduction(
                "ABSENCE",
                chargeableAbsenceDays,
                chargeableAbsenceDays * dailyRate,
                $"{chargeableAbsenceDays} unexcused working-day absence(s) after Friday visit offset.",
                $"{chargeableAbsenceDays} يوم غياب بدون عذر بعد مقاصة زيارة الجمعة."));
        }

        if (offsetAbsenceDays > 0)
        {
            lines.Add(new NetSalaryDeduction(
                "FRIDAY_OFFSET",
                offsetAbsenceDays,
                0,
                $"{offsetAbsenceDays} absence day(s) waived against Friday field visit(s) in the same month.",
                $"تم إسقاط {offsetAbsenceDays} يوم غياب مقابل زيارة ميدانية يوم الجمعة في نفس الشهر."));
        }

        if (lateDeductionDays > 0)
        {
            lines.Add(new NetSalaryDeduction(
                "LATE",
                lateDeductionDays,
                lateDeductionDays * dailyRate,
                $"{lateDeductionDays} working day(s) with {LateMinutesPerDeductedDay}+ late minutes after a {LateGraceMinutes}-minute grace. Extra delay on the same day does not deduct another day.",
                $"خصم {lateDeductionDays} يوم بسبب تأخير {LateMinutesPerDeductedDay} دقيقة أو أكثر في نفس اليوم بعد سماح {LateGraceMinutes} دقيقة. التأخير الأطول في نفس اليوم لا يخصم يومًا إضافيًا."));
        }

        var totalDeductions = lines.Sum(item => item.Amount);
        var gross = basicSalary + allowances;
        return new NetSalaryComputation(
            basicSalary,
            allowances,
            gross,
            dailyRate,
            absenceDays,
            fridayVisitDays,
            offsetAbsenceDays,
            chargeableAbsenceDays,
            lateMinutes,
            lateRemainderMinutes,
            lateDeductionDays,
            lines,
            totalDeductions,
            gross - totalDeductions);
    }
}
