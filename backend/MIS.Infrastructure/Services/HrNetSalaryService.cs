using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrNetSalaryService(ApplicationDbContext db, ICurrentUserContext user) : IHrNetSalaryService
{
    private static readonly string[] LockedPayroll =
    [
        AccountingValues.PayrollStatuses.Approved,
        AccountingValues.PayrollStatuses.Paid,
        AccountingValues.PayrollStatuses.Cancelled
    ];

    public async Task<HrPayrollSheetDto> GetSheetAsync(int year, int month, CancellationToken cancellationToken)
    {
        EnsureMonth(year, month);
        var start = new DateOnly(year, month, 1);
        var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var cairo = ResolveCairo();
        var isArabic = ApiTextLocalizer.IsArabic;

        var employees = await db.Employees.AsNoTracking()
            .Include(item => item.Department)
            .Include(item => item.Position)
            .Where(item => !item.IsArchived && item.Status != Employee.TerminatedStatus)
            .OrderBy(item => item.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var compensations = await db.EmployeeCompensations.AsNoTracking()
            .Where(item => item.IsCurrent || (item.EffectiveFrom <= end && (item.EffectiveTo == null || item.EffectiveTo >= start)))
            .OrderByDescending(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var absenceDates = await db.EmployeeAbsences.AsNoTracking()
            .Where(item => item.Status == AbsenceValues.UnexcusedStatus && item.AbsenceDate >= start && item.AbsenceDate <= end)
            .Select(item => new { item.EmployeeId, item.AbsenceDate })
            .ToListAsync(cancellationToken);

        var attendance = await db.AttendanceRecords.AsNoTracking()
            .Where(item => !item.IsDeleted && item.AttendanceDate >= start && item.AttendanceDate <= end)
            .Select(item => new { item.EmployeeId, item.AttendanceDate, item.Status, item.LateMinutes })
            .ToListAsync(cancellationToken);

        var visits = await db.CollectionFieldVisits.AsNoTracking()
            .Where(item => item.Collector.EmployeeId != null
                && item.Status != CollectionsValues.VisitStatuses.Cancelled
                && item.Status != CollectionsValues.VisitStatuses.Missed
                && item.ScheduledAt >= ToUtc(start.AddDays(-1), cairo)
                && item.ScheduledAt < ToUtc(end.AddDays(2), cairo))
            .Select(item => new { EmployeeId = item.Collector.EmployeeId!.Value, item.ScheduledAt, item.CheckedInAt, item.Status })
            .ToListAsync(cancellationToken);

        var fridayMissions = await db.HrExcuseMissions.AsNoTracking()
            .Where(item => item.EmployeeId != null
                && item.Status == "Approved"
                && item.Type == "FieldVisitMission"
                && item.Date >= start && item.Date <= end)
            .Select(item => new { EmployeeId = item.EmployeeId!.Value, item.Date })
            .ToListAsync(cancellationToken);

        var payrollRows = await db.AccountingEmployeePayrolls.AsNoTracking()
            .Where(item => item.Period.Year == year && item.Period.Month == month && item.Status != AccountingValues.PayrollStatuses.Cancelled)
            .Select(item => new { item.Id, item.EmployeeId, item.Status })
            .ToListAsync(cancellationToken);

        var rows = new List<HrPayrollEmployeeDto>(employees.Count);
        foreach (var employee in employees)
        {
            var compensation = compensations.FirstOrDefault(item => item.EmployeeId == employee.Id && item.IsCurrent)
                ?? compensations.FirstOrDefault(item => item.EmployeeId == employee.Id);
            var basic = compensation?.BasicSalary ?? 0;
            var allowances = compensation?.Allowances ?? 0;

            var absences = absenceDates.Where(item => item.EmployeeId == employee.Id && PayrollAttendancePolicy.IsWorkingDay(item.AbsenceDate.DayOfWeek))
                .Select(item => item.AbsenceDate);
            var attendanceAbsences = attendance.Where(item => item.EmployeeId == employee.Id
                    && item.Status == AttendanceValues.AbsentStatus
                    && PayrollAttendancePolicy.IsWorkingDay(item.AttendanceDate.DayOfWeek))
                .Select(item => item.AttendanceDate);
            var absenceDays = absences.Concat(attendanceAbsences).Distinct().Count();

            var dailyLateMinutes = attendance
                .Where(item => item.EmployeeId == employee.Id && PayrollAttendancePolicy.IsWorkingDay(item.AttendanceDate.DayOfWeek))
                .GroupBy(item => item.AttendanceDate)
                .Select(group => group.Sum(item => item.LateMinutes))
                .ToArray();

            var fridayVisitDays = visits
                .Where(item => item.EmployeeId == employee.Id && IsCompletedVisit(item.Status, item.CheckedInAt))
                .Select(item => ToCairoDate(item.CheckedInAt ?? item.ScheduledAt, cairo))
                .Where(date => date >= start && date <= end && PayrollAttendancePolicy.IsFriday(date.DayOfWeek))
                .Concat(fridayMissions.Where(item => item.EmployeeId == employee.Id && PayrollAttendancePolicy.IsFriday(item.Date.DayOfWeek)).Select(item => item.Date))
                .Distinct()
                .Count();

            var computed = PayrollAttendancePolicy.Compute(basic, allowances, absenceDays, fridayVisitDays, dailyLateMinutes);
            var payroll = payrollRows.FirstOrDefault(item => item.EmployeeId == employee.Id);
            rows.Add(new HrPayrollEmployeeDto(
                employee.Id,
                employee.EmployeeNumber,
                EmployeeName.Display(isArabic, employee.FullName, employee.FullNameArabic, employee.FullNameEnglish),
                isArabic ? employee.Department.NameArabic ?? employee.Department.Name : employee.Department.Name,
                employee.Position is null ? null : isArabic ? employee.Position.NameArabic ?? employee.Position.Name : employee.Position.Name,
                computed.BasicSalary,
                computed.Allowances,
                computed.GrossSalary,
                computed.DailyRate,
                computed.AbsenceDays,
                computed.FridayVisitDays,
                computed.OffsetAbsenceDays,
                computed.ChargeableAbsenceDays,
                computed.LateMinutes,
                computed.LateRemainderMinutes,
                computed.LateDeductionDays,
                computed.Deductions.Select(item => new HrPayrollDeductionDto(
                    item.Code, item.Units, item.Amount, isArabic ? item.ReasonArabic : item.ReasonEnglish)).ToArray(),
                computed.TotalDeductions,
                computed.NetSalary,
                compensation is not null,
                payroll?.Id,
                payroll?.Status));
        }

        await ApplyToAccountingAsync(year, month, rows, cancellationToken);
        var posted = await db.AccountingEmployeePayrolls.AsNoTracking()
            .Where(item => item.Period.Year == year && item.Period.Month == month && item.Status != AccountingValues.PayrollStatuses.Cancelled)
            .Select(item => new { item.Id, item.EmployeeId, item.Status })
            .ToListAsync(cancellationToken);
        return new HrPayrollSheetDto(
            year,
            month,
            PayrollAttendancePolicy.LateGraceMinutes,
            PayrollAttendancePolicy.LateMinutesPerDeductedDay,
            PayrollAttendancePolicy.MonthDivisor,
            rows.Count,
            rows.Sum(item => item.GrossSalary),
            rows.Sum(item => item.TotalDeductions),
            rows.Sum(item => item.NetSalary),
            rows.Select(row =>
            {
                var payroll = posted.FirstOrDefault(item => item.EmployeeId == row.EmployeeId);
                return row with { AccountingPayrollId = payroll?.Id, AccountingStatus = payroll?.Status };
            }).ToArray());
    }

    private async Task ApplyToAccountingAsync(int year, int month, IReadOnlyList<HrPayrollEmployeeDto> rows, CancellationToken cancellationToken)
    {
        if (user.UserId == Guid.Empty)
            return;

        var now = DateTimeOffset.UtcNow;
        var period = await db.AccountingPayrollPeriods.SingleOrDefaultAsync(item => item.Year == year && item.Month == month, cancellationToken);
        if (period is null)
        {
            period = new AccountingPayrollPeriod(year, month, user.UserId, now, "HR attendance deductions");
            db.AccountingPayrollPeriods.Add(period);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (period.Status is AccountingValues.PayrollStatuses.Approved or AccountingValues.PayrollStatuses.Paid)
            return;

        var existing = await db.AccountingEmployeePayrolls
            .Where(item => item.PeriodId == period.Id)
            .ToListAsync(cancellationToken);
        var existingByEmployee = existing.ToDictionary(item => item.EmployeeId);
        var monthEnd = period.MonthEnd;
        var compensations = await db.EmployeeCompensations
            .Where(item => item.EffectiveFrom <= monthEnd && (item.EffectiveTo == null || item.EffectiveTo >= monthEnd))
            .OrderByDescending(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var row in rows.Where(item => item.HasCompensation))
        {
            if (!existingByEmployee.TryGetValue(row.EmployeeId, out var payroll))
            {
                var compensation = compensations.FirstOrDefault(item => item.EmployeeId == row.EmployeeId);
                payroll = new AccountingEmployeePayroll(
                    period.Id, row.EmployeeId, row.EmployeeNumber, row.EmployeeName, row.DepartmentName, row.PositionName,
                    row.BasicSalary, row.Allowances, compensation?.Id, now);
                db.AccountingEmployeePayrolls.Add(payroll);
                existingByEmployee[row.EmployeeId] = payroll;
                changed = true;
            }

            if (LockedPayroll.Contains(payroll.Status))
                continue;

            var notes = MergeNotes(payroll.Notes, row);
            if (payroll.Deductions == row.TotalDeductions && payroll.Notes == notes)
                continue;

            payroll.ApplyComponents(
                payroll.Transportation,
                payroll.Commissions,
                payroll.Bonuses,
                row.TotalDeductions,
                payroll.OtherAdjustments,
                notes,
                now);
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsCompletedVisit(string status, DateTimeOffset? checkedInAt) =>
        checkedInAt.HasValue
        || status is CollectionsValues.VisitStatuses.Completed or CollectionsValues.VisitStatuses.InProgress;

    private static string MergeNotes(string? current, HrPayrollEmployeeDto row)
    {
        var summary = $"[HR-DEDUCTIONS] {row.ChargeableAbsenceDays} absence day(s), {row.LateDeductionDays} late day(s), Friday offset {row.OffsetAbsenceDays}.";
        if (string.IsNullOrWhiteSpace(current))
            return summary;
        if (current.StartsWith("[HR-DEDUCTIONS]", StringComparison.Ordinal))
        {
            var restStart = current.IndexOf('\n');
            var rest = restStart >= 0 ? current[(restStart + 1)..].Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(rest) ? summary : $"{summary}{Environment.NewLine}{rest}";
        }
        return $"{summary}{Environment.NewLine}{current.Trim()}";
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeZoneInfo cairo) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), cairo), TimeSpan.Zero);

    private static DateOnly ToCairoDate(DateTimeOffset value, TimeZoneInfo cairo) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, cairo).DateTime);

    private static TimeZoneInfo ResolveCairo()
    {
        foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    private static void EnsureMonth(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw new HrValidationException("A valid payroll year and month are required.");
    }
}
