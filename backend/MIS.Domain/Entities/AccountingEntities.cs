using MIS.Domain.Constants;

namespace MIS.Domain.Entities;

public sealed class AccountingPayrollPeriod
{
    private AccountingPayrollPeriod() { }

    public AccountingPayrollPeriod(int year, int month, Guid createdByUserId, DateTimeOffset createdAt, string? notes = null)
    {
        if (year is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator is required.", nameof(createdByUserId));
        Id = Guid.NewGuid(); Year = year; Month = month; Status = AccountingValues.PayrollStatuses.Draft;
        CreatedByUserId = createdByUserId; CreatedAt = createdAt; Notes = Normalize(notes);
    }

    public Guid Id { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string Status { get; private set; } = AccountingValues.PayrollStatuses.Draft;
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public User CreatedByUser { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public User? ApprovedByUser { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public ICollection<AccountingEmployeePayroll> Payrolls { get; private set; } = new List<AccountingEmployeePayroll>();

    public DateOnly MonthEnd => new(Year, Month, DateTime.DaysInMonth(Year, Month));
    public void SetNotes(string? notes) => Notes = Normalize(notes);
    public void MarkApproved(Guid userId, DateTimeOffset at) { Status = AccountingValues.PayrollStatuses.Approved; ApprovedByUserId = userId; ApprovedAt = at; }
    public void MarkPaid(DateTimeOffset at) { Status = AccountingValues.PayrollStatuses.Paid; PaidAt = at; }
    public void Cancel() => Status = AccountingValues.PayrollStatuses.Cancelled;
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AccountingEmployeePayroll
{
    private AccountingEmployeePayroll() { }

    public AccountingEmployeePayroll(
        Guid periodId, Guid employeeId, string employeeNumber, string employeeName, string? departmentName, string? positionName,
        decimal basicSalary, decimal allowances, Guid? compensationSnapshotId, DateTimeOffset createdAt)
    {
        if (periodId == Guid.Empty || employeeId == Guid.Empty) throw new ArgumentException("Period and employee are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeName);
        if (basicSalary < 0 || allowances < 0) throw new ArgumentOutOfRangeException(nameof(basicSalary));
        Id = Guid.NewGuid(); PeriodId = periodId; EmployeeId = employeeId;
        EmployeeNumber = employeeNumber.Trim(); EmployeeName = employeeName.Trim();
        DepartmentName = NullIfEmpty(departmentName); PositionName = NullIfEmpty(positionName);
        BasicSalary = basicSalary; Allowances = allowances; CompensationSnapshotId = compensationSnapshotId;
        Status = AccountingValues.PayrollStatuses.Draft; CreatedAt = createdAt; RecalculateNet();
    }

    public Guid Id { get; private set; }
    public Guid PeriodId { get; private set; }
    public AccountingPayrollPeriod Period { get; private set; } = null!;
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public string EmployeeNumber { get; private set; } = string.Empty;
    public string EmployeeName { get; private set; } = string.Empty;
    public string? DepartmentName { get; private set; }
    public string? PositionName { get; private set; }
    public Guid? CompensationSnapshotId { get; private set; }
    public EmployeeCompensation? CompensationSnapshot { get; private set; }
    public decimal BasicSalary { get; private set; }
    public decimal Allowances { get; private set; }
    public decimal Transportation { get; private set; }
    public decimal Commissions { get; private set; }
    public decimal Bonuses { get; private set; }
    public decimal Deductions { get; private set; }
    public decimal OtherAdjustments { get; private set; }
    public decimal NetSalary { get; private set; }
    public string Status { get; private set; } = AccountingValues.PayrollStatuses.Draft;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void ApplyComponents(decimal transportation, decimal commissions, decimal bonuses, decimal deductions, decimal otherAdjustments, string? notes, DateTimeOffset now)
    {
        EnsureEditable();
        if (transportation < 0 || commissions < 0 || bonuses < 0 || deductions < 0) throw new ArgumentOutOfRangeException(nameof(deductions), "Component amounts cannot be negative except OtherAdjustments.");
        Transportation = transportation; Commissions = commissions; Bonuses = bonuses; Deductions = deductions;
        OtherAdjustments = otherAdjustments; Notes = NullIfEmpty(notes); UpdatedAt = now; RecalculateNet();
    }

    public void SubmitForReview(DateTimeOffset now) { EnsureEditable(); Status = AccountingValues.PayrollStatuses.PendingReview; UpdatedAt = now; }
    public void Approve(DateTimeOffset now) { if (Status is not (AccountingValues.PayrollStatuses.Draft or AccountingValues.PayrollStatuses.PendingReview)) throw new InvalidOperationException("Only draft/pending payroll can be approved."); Status = AccountingValues.PayrollStatuses.Approved; UpdatedAt = now; }
    public void MarkPaid(DateTimeOffset now) { if (Status != AccountingValues.PayrollStatuses.Approved) throw new InvalidOperationException("Only approved payroll can be marked paid."); Status = AccountingValues.PayrollStatuses.Paid; UpdatedAt = now; }
    public void Cancel(DateTimeOffset now) { if (Status == AccountingValues.PayrollStatuses.Paid) throw new InvalidOperationException("Paid payroll cannot be cancelled."); Status = AccountingValues.PayrollStatuses.Cancelled; UpdatedAt = now; }
    public void ApplyPostApprovalAdjustment(decimal adjustments, string? notes, DateTimeOffset now)
    {
        if (Status is not (AccountingValues.PayrollStatuses.Approved or AccountingValues.PayrollStatuses.Paid))
            throw new InvalidOperationException("Explicit adjustments apply only to approved or paid payroll.");
        OtherAdjustments += adjustments; Notes = NullIfEmpty(notes); UpdatedAt = now; RecalculateNet();
    }

    private void RecalculateNet() => NetSalary = BasicSalary + Allowances + Transportation + Commissions + Bonuses + OtherAdjustments - Deductions;
    private void EnsureEditable()
    {
        if (!AccountingValues.PayrollStatuses.Editable.Contains(Status))
            throw new InvalidOperationException("Approved or paid payroll cannot be silently edited.");
    }
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AccountingTransportationClaim
{
    private AccountingTransportationClaim() { }

    public AccountingTransportationClaim(Guid employeeId, DateOnly claimDate, decimal amount, string purpose, Guid submittedByUserId, DateTimeOffset createdAt, Guid? fieldVisitId = null, Guid? caseId = null, string? notes = null)
    {
        if (employeeId == Guid.Empty || submittedByUserId == Guid.Empty) throw new ArgumentException("Employee and submitter are required.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        Id = Guid.NewGuid(); EmployeeId = employeeId; ClaimDate = claimDate; Amount = amount; Purpose = purpose.Trim();
        FieldVisitId = fieldVisitId; CaseId = caseId; Notes = NullIfEmpty(notes); Status = AccountingValues.TransportationStatuses.Pending;
        SubmittedByUserId = submittedByUserId; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public DateOnly ClaimDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? FieldVisitId { get; private set; }
    public FieldVisit? FieldVisit { get; private set; }
    public Guid? CaseId { get; private set; }
    public CollectionCase? Case { get; private set; }
    public string Status { get; private set; } = AccountingValues.TransportationStatuses.Pending;
    public string? AttachmentFileName { get; private set; }
    public string? AttachmentContentType { get; private set; }
    public string? AttachmentStorageKey { get; private set; }
    public long? AttachmentLength { get; private set; }
    public string? AttachmentSha256 { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public User SubmittedByUser { get; private set; } = null!;
    public Guid? ApprovedByUserId { get; private set; }
    public User? ApprovedByUser { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(DateOnly claimDate, decimal amount, string purpose, string? notes, Guid? fieldVisitId, Guid? caseId, DateTimeOffset now)
    {
        EnsurePending();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ClaimDate = claimDate; Amount = amount; Purpose = purpose.Trim(); Notes = NullIfEmpty(notes);
        FieldVisitId = fieldVisitId; CaseId = caseId; UpdatedAt = now;
    }

    public void SetAttachment(string fileName, string contentType, string storageKey, long length, string sha256, DateTimeOffset now)
    {
        EnsurePending();
        AttachmentFileName = fileName; AttachmentContentType = contentType; AttachmentStorageKey = storageKey;
        AttachmentLength = length; AttachmentSha256 = sha256; UpdatedAt = now;
    }

    public void ClearAttachment(DateTimeOffset now) { EnsurePending(); AttachmentFileName = AttachmentContentType = AttachmentStorageKey = AttachmentSha256 = null; AttachmentLength = null; UpdatedAt = now; }
    public void Approve(Guid userId, DateTimeOffset now) { EnsurePending(); Status = AccountingValues.TransportationStatuses.Approved; ApprovedByUserId = userId; UpdatedAt = now; }
    public void Reject(Guid userId, DateTimeOffset now) { EnsurePending(); Status = AccountingValues.TransportationStatuses.Rejected; ApprovedByUserId = userId; UpdatedAt = now; }
    public void MarkPaid(DateTimeOffset now) { if (Status != AccountingValues.TransportationStatuses.Approved) throw new InvalidOperationException("Only approved claims can be paid."); Status = AccountingValues.TransportationStatuses.Paid; UpdatedAt = now; }
    public void Cancel(DateTimeOffset now) { if (Status == AccountingValues.TransportationStatuses.Paid) throw new InvalidOperationException("Paid claims cannot be cancelled."); Status = AccountingValues.TransportationStatuses.Cancelled; UpdatedAt = now; }
    private void EnsurePending() { if (Status != AccountingValues.TransportationStatuses.Pending) throw new InvalidOperationException("Only pending claims can be edited."); }
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AccountingCommissionRule
{
    private AccountingCommissionRule() { }

    public AccountingCommissionRule(string code, string nameArabic, string nameEnglish, string scope, string basis, decimal? percentage, decimal? fixedAmount, DateOnly effectiveFrom, Guid createdByUserId, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish);
        scope = scope.Trim().ToUpperInvariant(); basis = basis.Trim().ToUpperInvariant();
        if (scope is not (AccountingValues.CommissionScopes.Collector or AccountingValues.CommissionScopes.Supervisor)) throw new ArgumentException("Invalid scope.");
        if (basis is not (AccountingValues.CommissionBases.PercentOfCollected or AccountingValues.CommissionBases.FixedAmount or AccountingValues.CommissionBases.Tiered)) throw new ArgumentException("Invalid basis.");
        if (basis == AccountingValues.CommissionBases.PercentOfCollected && (percentage is null or < 0 or > 100)) throw new ArgumentOutOfRangeException(nameof(percentage));
        if (basis == AccountingValues.CommissionBases.FixedAmount && (fixedAmount is null or < 0)) throw new ArgumentOutOfRangeException(nameof(fixedAmount));
        Id = Guid.NewGuid(); Code = code.Trim().ToUpperInvariant(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim();
        Scope = scope; Basis = basis; Percentage = percentage; FixedAmount = fixedAmount; EffectiveFrom = effectiveFrom;
        Version = 1; IsActive = true; CreatedByUserId = createdByUserId; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public string Scope { get; private set; } = string.Empty;
    public string Basis { get; private set; } = string.Empty;
    public decimal? Percentage { get; private set; }
    public decimal? FixedAmount { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public decimal Calculate(decimal eligibleAmount) => Basis switch
    {
        AccountingValues.CommissionBases.PercentOfCollected => Math.Round(eligibleAmount * (Percentage ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero),
        AccountingValues.CommissionBases.FixedAmount => FixedAmount ?? 0m,
        _ => throw new InvalidOperationException("Tiered rules require explicit tier evaluation.")
    };

    public void Deactivate(DateOnly endDate) { IsActive = false; EffectiveTo = endDate; }
}

public sealed class AccountingCollectorCommission
{
    private AccountingCollectorCommission() { }

    public AccountingCollectorCommission(int year, int month, Guid collectorUserId, Guid? employeeId, int assignedCasesCount, decimal collectedAmount, decimal eligibleAmount, Guid ruleId, int ruleVersion, decimal rateApplied, decimal commissionAmount, string snapshotJson, DateTimeOffset createdAt)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (collectorUserId == Guid.Empty || ruleId == Guid.Empty) throw new ArgumentException("Collector and rule are required.");
        if (collectedAmount < 0 || eligibleAmount < 0 || commissionAmount < 0) throw new ArgumentOutOfRangeException(nameof(collectedAmount));
        Id = Guid.NewGuid(); PeriodYear = year; PeriodMonth = month; CollectorUserId = collectorUserId; EmployeeId = employeeId;
        AssignedCasesCount = assignedCasesCount; CollectedAmount = collectedAmount; EligibleAmount = eligibleAmount;
        CommissionRuleId = ruleId; RuleVersion = ruleVersion; RateApplied = rateApplied; CommissionAmount = commissionAmount;
        Adjustments = 0; FinalCommission = commissionAmount; CalculationSnapshotJson = snapshotJson; Status = AccountingValues.CommissionStatuses.Draft; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public Guid CollectorUserId { get; private set; }
    public User CollectorUser { get; private set; } = null!;
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }
    public int AssignedCasesCount { get; private set; }
    public decimal CollectedAmount { get; private set; }
    public decimal EligibleAmount { get; private set; }
    public Guid CommissionRuleId { get; private set; }
    public AccountingCommissionRule CommissionRule { get; private set; } = null!;
    public int RuleVersion { get; private set; }
    public decimal RateApplied { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal Adjustments { get; private set; }
    public decimal FinalCommission { get; private set; }
    public string CalculationSnapshotJson { get; private set; } = "[]";
    public string Status { get; private set; } = AccountingValues.CommissionStatuses.Draft;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void RefreshDraft(int cases, decimal collected, decimal eligible, Guid ruleId, int version, decimal rate, decimal commission, string snapshot, DateTimeOffset now)
    {
        EnsureEditable();
        AssignedCasesCount = cases; CollectedAmount = collected; EligibleAmount = eligible; CommissionRuleId = ruleId;
        RuleVersion = version; RateApplied = rate; CommissionAmount = commission; CalculationSnapshotJson = snapshot;
        FinalCommission = commission + Adjustments; UpdatedAt = now;
    }

    public void Adjust(decimal adjustments, string? notes, DateTimeOffset now)
    {
        EnsureEditable();
        Adjustments = adjustments; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        FinalCommission = CommissionAmount + Adjustments; UpdatedAt = now;
    }

    public void Submit(DateTimeOffset now) { EnsureEditable(); Status = AccountingValues.CommissionStatuses.PendingReview; UpdatedAt = now; }
    public void Approve(DateTimeOffset now) { if (Status is not (AccountingValues.CommissionStatuses.Draft or AccountingValues.CommissionStatuses.PendingReview)) throw new InvalidOperationException("Invalid status."); Status = AccountingValues.CommissionStatuses.Approved; UpdatedAt = now; }
    public void MarkPaid(DateTimeOffset now) { if (Status != AccountingValues.CommissionStatuses.Approved) throw new InvalidOperationException("Only approved commissions can be paid."); Status = AccountingValues.CommissionStatuses.Paid; UpdatedAt = now; }
    public void Cancel(DateTimeOffset now) { if (Status == AccountingValues.CommissionStatuses.Paid) throw new InvalidOperationException("Paid commission cannot be cancelled."); Status = AccountingValues.CommissionStatuses.Cancelled; UpdatedAt = now; }
    private void EnsureEditable() { if (!AccountingValues.CommissionStatuses.Editable.Contains(Status)) throw new InvalidOperationException("Approved/paid commissions cannot be silently recalculated."); }
}

public sealed class AccountingSupervisorCommission
{
    private AccountingSupervisorCommission() { }

    public AccountingSupervisorCommission(int year, int month, Guid supervisorUserId, Guid? employeeId, string? teamSummary, int collectorCount, decimal teamCollectedAmount, decimal eligibleAmount, Guid ruleId, int ruleVersion, decimal rateApplied, decimal commissionAmount, string snapshotJson, DateTimeOffset createdAt)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (supervisorUserId == Guid.Empty || ruleId == Guid.Empty) throw new ArgumentException("Supervisor and rule are required.");
        Id = Guid.NewGuid(); PeriodYear = year; PeriodMonth = month; SupervisorUserId = supervisorUserId; EmployeeId = employeeId;
        TeamSummary = string.IsNullOrWhiteSpace(teamSummary) ? null : teamSummary.Trim(); CollectorCount = collectorCount;
        TeamCollectedAmount = teamCollectedAmount; EligibleAmount = eligibleAmount; CommissionRuleId = ruleId; RuleVersion = ruleVersion;
        RateApplied = rateApplied; CommissionAmount = commissionAmount; Adjustments = 0; FinalCommission = commissionAmount;
        CalculationSnapshotJson = snapshotJson; Status = AccountingValues.CommissionStatuses.Draft; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public Guid SupervisorUserId { get; private set; }
    public User SupervisorUser { get; private set; } = null!;
    public Guid? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }
    public string? TeamSummary { get; private set; }
    public int CollectorCount { get; private set; }
    public decimal TeamCollectedAmount { get; private set; }
    public decimal EligibleAmount { get; private set; }
    public Guid CommissionRuleId { get; private set; }
    public AccountingCommissionRule CommissionRule { get; private set; } = null!;
    public int RuleVersion { get; private set; }
    public decimal RateApplied { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal Adjustments { get; private set; }
    public decimal FinalCommission { get; private set; }
    public string CalculationSnapshotJson { get; private set; } = "[]";
    public string Status { get; private set; } = AccountingValues.CommissionStatuses.Draft;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void RefreshDraft(string? teamSummary, int collectors, decimal teamCollected, decimal eligible, Guid ruleId, int version, decimal rate, decimal commission, string snapshot, DateTimeOffset now)
    {
        EnsureEditable();
        TeamSummary = string.IsNullOrWhiteSpace(teamSummary) ? null : teamSummary.Trim(); CollectorCount = collectors;
        TeamCollectedAmount = teamCollected; EligibleAmount = eligible; CommissionRuleId = ruleId; RuleVersion = version;
        RateApplied = rate; CommissionAmount = commission; CalculationSnapshotJson = snapshot; FinalCommission = commission + Adjustments; UpdatedAt = now;
    }

    public void Adjust(decimal adjustments, string? notes, DateTimeOffset now)
    {
        EnsureEditable();
        Adjustments = adjustments; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        FinalCommission = CommissionAmount + Adjustments; UpdatedAt = now;
    }

    public void Submit(DateTimeOffset now) { EnsureEditable(); Status = AccountingValues.CommissionStatuses.PendingReview; UpdatedAt = now; }
    public void Approve(DateTimeOffset now) { if (Status is not (AccountingValues.CommissionStatuses.Draft or AccountingValues.CommissionStatuses.PendingReview)) throw new InvalidOperationException("Invalid status."); Status = AccountingValues.CommissionStatuses.Approved; UpdatedAt = now; }
    public void MarkPaid(DateTimeOffset now) { if (Status != AccountingValues.CommissionStatuses.Approved) throw new InvalidOperationException("Only approved commissions can be paid."); Status = AccountingValues.CommissionStatuses.Paid; UpdatedAt = now; }
    public void Cancel(DateTimeOffset now) { if (Status == AccountingValues.CommissionStatuses.Paid) throw new InvalidOperationException("Paid commission cannot be cancelled."); Status = AccountingValues.CommissionStatuses.Cancelled; UpdatedAt = now; }
    private void EnsureEditable() { if (!AccountingValues.CommissionStatuses.Editable.Contains(Status)) throw new InvalidOperationException("Approved/paid commissions cannot be silently recalculated."); }
}
