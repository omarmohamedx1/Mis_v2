namespace MIS.Application.DTOs.Hr;

public sealed record HrPayrollDeductionDto(
    string Code,
    int Units,
    decimal Amount,
    string Reason);

public sealed record HrPayrollEmployeeDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string? DepartmentName,
    string? PositionName,
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
    IReadOnlyList<HrPayrollDeductionDto> Deductions,
    decimal TotalDeductions,
    decimal NetSalary,
    bool HasCompensation,
    Guid? AccountingPayrollId,
    string? AccountingStatus);

public sealed record HrPayrollSheetDto(
    int Year,
    int Month,
    int LateGraceMinutes,
    int LateMinutesPerDeductedDay,
    decimal MonthDivisor,
    int EmployeeCount,
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet,
    IReadOnlyList<HrPayrollEmployeeDto> Employees);

public sealed record HrPayrollSyncResultDto(
    int Year,
    int Month,
    Guid PeriodId,
    int UpdatedCount,
    int SkippedLockedCount);
