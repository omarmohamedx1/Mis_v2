namespace MIS.Application.DTOs.Accounting;

public sealed record AccountingDashboardDto(
    int Year,
    int Month,
    decimal TotalPayroll,
    decimal TotalTransportation,
    decimal TotalCollectorCommissions,
    decimal TotalSupervisorCommissions,
    int PendingApprovals);

public sealed record AccountingPayrollPeriodDto(Guid Id, int Year, int Month, string Status, string? Notes, int EmployeeCount, decimal TotalNet, DateTimeOffset CreatedAt);
public sealed record AccountingEmployeePayrollDto(
    Guid Id, Guid PeriodId, Guid EmployeeId, string EmployeeNumber, string EmployeeName, string? DepartmentName, string? PositionName,
    decimal BasicSalary, decimal Allowances, decimal Transportation, decimal Commissions, decimal Bonuses, decimal Deductions,
    decimal OtherAdjustments, decimal NetSalary, string Status, string? Notes, DateTimeOffset CreatedAt);

public sealed record AccountingPayrollPageDto(IReadOnlyList<AccountingEmployeePayrollDto> Items, int Page, int PageSize, int TotalCount);
public sealed record UpdateAccountingPayrollRequest(decimal Transportation, decimal Commissions, decimal Bonuses, decimal Deductions, decimal OtherAdjustments, string? Notes);
public sealed record GeneratePayrollRequest(int Year, int Month, string? Notes);

public sealed record AccountingTransportationDto(
    Guid Id, Guid EmployeeId, string EmployeeNumber, string EmployeeName, string? DepartmentName, string? PositionName,
    DateOnly ClaimDate, decimal Amount, string Purpose, string? Notes, Guid? FieldVisitId, Guid? CaseId, string Status,
    string? AttachmentFileName, DateTimeOffset CreatedAt);

public sealed record AccountingTransportationPageDto(IReadOnlyList<AccountingTransportationDto> Items, int Page, int PageSize, int TotalCount);
public sealed record CreateTransportationRequest(Guid EmployeeId, DateOnly ClaimDate, decimal Amount, string Purpose, string? Notes, Guid? FieldVisitId, Guid? CaseId);
public sealed record UpdateTransportationRequest(DateOnly ClaimDate, decimal Amount, string Purpose, string? Notes, Guid? FieldVisitId, Guid? CaseId);

public sealed record AccountingCommissionRuleDto(Guid Id, string Code, string NameArabic, string NameEnglish, string Scope, string Basis, decimal? Percentage, decimal? FixedAmount, DateOnly EffectiveFrom, bool IsActive, int Version);
public sealed record CreateCommissionRuleRequest(string Code, string NameArabic, string NameEnglish, string Scope, string Basis, decimal? Percentage, decimal? FixedAmount, DateOnly EffectiveFrom);

public sealed record AccountingCollectorCommissionDto(
    Guid Id, int PeriodYear, int PeriodMonth, Guid CollectorUserId, string CollectorName, string? EmployeeNumber,
    int AssignedCasesCount, decimal CollectedAmount, decimal EligibleAmount, string RuleCode, decimal RateApplied,
    decimal CommissionAmount, decimal Adjustments, decimal FinalCommission, string Status, string? Notes, DateTimeOffset CreatedAt);

public sealed record AccountingCollectorCommissionDetailsDto(
    AccountingCollectorCommissionDto Summary,
    IReadOnlyList<AccountingCommissionContributionDto> Contributions);

public sealed record AccountingCommissionContributionDto(Guid PaymentId, Guid CaseId, string? CaseNumber, DateOnly PaymentDate, decimal Amount, string ReferenceNumber);

public sealed record AccountingSupervisorCommissionDto(
    Guid Id, int PeriodYear, int PeriodMonth, Guid SupervisorUserId, string SupervisorName, string? EmployeeNumber,
    string? TeamSummary, int CollectorCount, decimal TeamCollectedAmount, decimal EligibleAmount, string RuleCode,
    decimal RateApplied, decimal CommissionAmount, decimal Adjustments, decimal FinalCommission, string Status, string? Notes, DateTimeOffset CreatedAt);

public sealed record AccountingCommissionPageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record CalculateCommissionsRequest(int Year, int Month);
public sealed record AdjustCommissionRequest(decimal Adjustments, string? Notes);
public sealed record AccountingLookupEmployeeDto(Guid Id, string EmployeeNumber, string FullName, string? DepartmentName, string? PositionName);
