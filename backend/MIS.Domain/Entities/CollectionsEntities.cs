using MIS.Domain.Constants;

namespace MIS.Domain.Entities;

public sealed class ClientOrganization
{
    private ClientOrganization() { }
    public ClientOrganization(string code, string nameArabic, string nameEnglish, string organizationType, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationType);
        Id = Guid.NewGuid(); Code = code.Trim().ToUpperInvariant(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim();
        OrganizationType = organizationType.Trim().ToUpperInvariant(); IsActive = true; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public string OrganizationType { get; private set; } = string.Empty;
    public string? LogoStorageKey { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string SettingsJson { get; private set; } = "{}";
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public void Update(string nameArabic, string nameEnglish, string organizationType, string? contactEmail, string? contactPhone, string? settingsJson, bool isActive, DateTimeOffset now)
    { ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish); ArgumentException.ThrowIfNullOrWhiteSpace(organizationType); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim(); OrganizationType = organizationType.Trim().ToUpperInvariant(); ContactEmail = Normalize(contactEmail); ContactPhone = Normalize(contactPhone); SettingsJson = JsonText.NormalizeRequired(settingsJson, nameof(settingsJson), "{}"); IsActive = isActive; UpdatedAt = now; }
    public void SetLogo(string storageKey, DateTimeOffset now)
    { ArgumentException.ThrowIfNullOrWhiteSpace(storageKey); LogoStorageKey = storageKey.Trim(); UpdatedAt = now; }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CollectionPortfolio
{
    private CollectionPortfolio() { }
    public CollectionPortfolio(Guid organizationId, string code, string nameArabic, string nameEnglish, string currencyCode, DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish);
        Id = Guid.NewGuid(); OrganizationId = organizationId; Code = code.Trim().ToUpperInvariant(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim();
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "EGP" : currencyCode.Trim().ToUpperInvariant(); IsActive = true; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ClientOrganization Organization { get; private set; } = null!;
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "EGP";
    public decimal? TargetAmount { get; private set; }
    public string SettingsJson { get; private set; } = "{}";
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public void Update(string nameArabic, string nameEnglish, string currencyCode, decimal? targetAmount, string? settingsJson, bool isActive)
    { ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish); if (targetAmount < 0) throw new ArgumentOutOfRangeException(nameof(targetAmount)); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim(); CurrencyCode = currencyCode.Trim().ToUpperInvariant(); TargetAmount = targetAmount; SettingsJson = JsonText.NormalizeRequired(settingsJson, nameof(settingsJson), "{}"); IsActive = isActive; }
}

public sealed class CollectionCustomer
{
    private CollectionCustomer() { }
    public CollectionCustomer(Guid organizationId, string customerCode, string fullNameArabic, string fullNameEnglish, DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(customerCode);
        Id = Guid.NewGuid(); OrganizationId = organizationId; CustomerCode = customerCode.Trim(); FullNameArabic = Normalize(fullNameArabic); FullNameEnglish = Normalize(fullNameEnglish);
        if (FullNameArabic is null && FullNameEnglish is null) throw new ArgumentException("At least one customer name is required."); CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ClientOrganization Organization { get; private set; } = null!;
    public string CustomerCode { get; private set; } = string.Empty;
    public string? FullNameArabic { get; private set; }
    public string? FullNameEnglish { get; private set; }
    public string? NationalId { get; private set; }
    public string? PrimaryPhone { get; private set; }
    public string? AlternatePhone { get; private set; }
    public string? AddressArabic { get; private set; }
    public string? AddressEnglish { get; private set; }
    public string? Governorate { get; private set; }
    public string? Area { get; private set; }
    public string? Employer { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public void ApplyImportedContact(string? nameArabic, string? nameEnglish, string? nationalId, string? primaryPhone)
    { FullNameArabic = Normalize(nameArabic) ?? FullNameArabic; FullNameEnglish = Normalize(nameEnglish) ?? FullNameEnglish; NationalId = Normalize(nationalId) ?? NationalId; PrimaryPhone = Normalize(primaryPhone) ?? PrimaryPhone; }
    public void UpdatePortfolioContact(string? primaryPhone, string? alternatePhone, string? address, bool arabic)
    { PrimaryPhone = Normalize(primaryPhone); AlternatePhone = Normalize(alternatePhone); if (arabic) AddressArabic = Normalize(address); else AddressEnglish = Normalize(address); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DelinquencyBucketDefinition
{
    private DelinquencyBucketDefinition() { }
    public DelinquencyBucketDefinition(Guid organizationId, Guid? portfolioId, string code, string nameArabic, string nameEnglish, int? minimumDays, int? maximumDays, int sortOrder, DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.", nameof(organizationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish);
        if (minimumDays.HasValue && maximumDays.HasValue && minimumDays > maximumDays) throw new ArgumentException("Minimum days cannot exceed maximum days.");
        Id = Guid.NewGuid(); OrganizationId = organizationId; PortfolioId = portfolioId; Code = code.Trim().ToUpperInvariant(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim();
        MinimumDays = minimumDays; MaximumDays = maximumDays; SortOrder = sortOrder; IsActive = true; CreatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ClientOrganization Organization { get; private set; } = null!;
    public Guid? PortfolioId { get; private set; }
    public CollectionPortfolio? Portfolio { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public int? MinimumDays { get; private set; }
    public int? MaximumDays { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public void Update(string nameArabic, string nameEnglish, int? minimumDays, int? maximumDays, int sortOrder, bool isActive)
    { ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic); ArgumentException.ThrowIfNullOrWhiteSpace(nameEnglish); if (minimumDays.HasValue && maximumDays.HasValue && minimumDays > maximumDays) throw new ArgumentException("Minimum days cannot exceed maximum days."); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim(); MinimumDays = minimumDays; MaximumDays = maximumDays; SortOrder = sortOrder; IsActive = isActive; }
}

public sealed class CollectionTeam
{
    private CollectionTeam() { }
    public CollectionTeam(string code, string nameArabic, string nameEnglish, Guid? supervisorId, DateTimeOffset createdAt)
    { Id = Guid.NewGuid(); Code = code.Trim().ToUpperInvariant(); NameArabic = nameArabic.Trim(); NameEnglish = nameEnglish.Trim(); SupervisorId = supervisorId; IsActive = true; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string NameArabic { get; private set; } = string.Empty;
    public string NameEnglish { get; private set; } = string.Empty;
    public Guid? SupervisorId { get; private set; }
    public User? Supervisor { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class CollectionTeamMember
{
    private CollectionTeamMember() { }
    public CollectionTeamMember(Guid teamId, Guid userId, DateTimeOffset createdAt) { Id = Guid.NewGuid(); TeamId = teamId; UserId = userId; IsActive = true; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public CollectionTeam Team { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class CollectionUserAccess
{
    private CollectionUserAccess() { }
    public CollectionUserAccess(Guid userId, Guid organizationId, Guid? portfolioId, DateTimeOffset createdAt) { Id = Guid.NewGuid(); UserId = userId; OrganizationId = organizationId; PortfolioId = portfolioId; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid OrganizationId { get; private set; }
    public ClientOrganization Organization { get; private set; } = null!;
    public Guid? PortfolioId { get; private set; }
    public CollectionPortfolio? Portfolio { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class CollectionCase
{
    private CollectionCase() { }
    public CollectionCase(Guid portfolioId, Guid customerId, string caseNumber, string accountReference, decimal originalAmount, decimal outstandingBalance, decimal overdueBalance, int daysPastDue, Guid bucketId, DateTimeOffset createdAt)
    {
        if (portfolioId == Guid.Empty || customerId == Guid.Empty || bucketId == Guid.Empty) throw new ArgumentException("Portfolio, customer, and bucket are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(caseNumber); ArgumentException.ThrowIfNullOrWhiteSpace(accountReference);
        if (originalAmount < 0 || outstandingBalance < 0 || overdueBalance < 0 || daysPastDue < 0) throw new ArgumentOutOfRangeException(nameof(outstandingBalance), "Financial values and days past due cannot be negative.");
        Id = Guid.NewGuid(); PortfolioId = portfolioId; CustomerId = customerId; CaseNumber = caseNumber.Trim().ToUpperInvariant(); AccountReference = accountReference.Trim();
        OriginalAmount = originalAmount; PrincipalAmount = originalAmount; OutstandingBalance = outstandingBalance; OverdueBalance = overdueBalance; TotalDue = overdueBalance; DaysPastDue = daysPastDue; CurrentBucketId = bucketId;
        Status = CollectionsValues.CaseStatuses.Active; Priority = "NORMAL"; CreatedAt = createdAt; UpdatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid PortfolioId { get; private set; }
    public CollectionPortfolio Portfolio { get; private set; } = null!;
    public Guid CustomerId { get; private set; }
    public CollectionCustomer Customer { get; private set; } = null!;
    public Guid? SourceImportId { get; private set; }
    public BankPortfolioImport? SourceImport { get; private set; }
    public string CaseNumber { get; private set; } = string.Empty;
    public string AccountReference { get; private set; } = string.Empty;
    public string? ContractReference { get; private set; }
    public string? ProductType { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal OutstandingBalance { get; private set; }
    public decimal OverdueBalance { get; private set; }
    public decimal Penalties { get; private set; }
    public decimal Fees { get; private set; }
    public decimal TotalDue { get; private set; }
    public int DaysPastDue { get; private set; }
    public Guid CurrentBucketId { get; private set; }
    public DelinquencyBucketDefinition CurrentBucket { get; private set; } = null!;
    public Guid? AssignedCollectorId { get; private set; }
    public User? AssignedCollector { get; private set; }
    public Guid? AssignedTeamId { get; private set; }
    public CollectionTeam? AssignedTeam { get; private set; }
    public string Status { get; private set; } = CollectionsValues.CaseStatuses.Active;
    public string Priority { get; private set; } = "NORMAL";
    public int PriorityScore { get; private set; }
    public string PriorityExplanation { get; private set; } = string.Empty;
    public DateTimeOffset? NextFollowUpAt { get; private set; }
    public DateTimeOffset? LastContactAt { get; private set; }
    public DateTimeOffset? LastPaymentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public Guid? ArchivedById { get; private set; }
    public User? ArchivedBy { get; private set; }
    public string? ArchiveReason { get; private set; }
    public string? ArchiveNotes { get; private set; }
    public DateTimeOffset? RestoredAt { get; private set; }
    public Guid? RestoredById { get; private set; }
    public User? RestoredBy { get; private set; }
    public string? RestoreReason { get; private set; }

    public void Assign(Guid collectorId, Guid? teamId, DateTimeOffset now) { AssignedCollectorId = collectorId; AssignedTeamId = teamId; UpdatedAt = now; }
    public void Unassign(DateTimeOffset now) { AssignedCollectorId = null; AssignedTeamId = null; UpdatedAt = now; }
    public void SetPriority(int score, string explanation, DateTimeOffset now) { PriorityScore = Math.Clamp(score, 0, 100); Priority = score >= 70 ? "HIGH" : score >= 40 ? "MEDIUM" : "NORMAL"; PriorityExplanation = explanation.Trim(); UpdatedAt = now; }
    public void RecordContact(DateTimeOffset contactedAt, DateTimeOffset? nextFollowUpAt) { LastContactAt = contactedAt; NextFollowUpAt = nextFollowUpAt; UpdatedAt = contactedAt; }
    public void ScheduleNextFollowUp(DateTimeOffset? nextFollowUpAt, DateTimeOffset now) { NextFollowUpAt = nextFollowUpAt; UpdatedAt = now; }
    public void RecordApprovedPayment(decimal amount, DateTimeOffset paymentAt, DateTimeOffset now) { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); OutstandingBalance = Math.Max(0, OutstandingBalance - amount); OverdueBalance = Math.Max(0, OverdueBalance - amount); TotalDue = OverdueBalance + Penalties + Fees; LastPaymentAt = paymentAt; UpdatedAt = now; }
    public void RestoreApprovedPayment(decimal outstandingBefore, decimal overdueBefore, DateTimeOffset now) { if (outstandingBefore < 0 || overdueBefore < 0 || outstandingBefore > OriginalAmount || overdueBefore > OriginalAmount) throw new ArgumentOutOfRangeException(nameof(outstandingBefore), "The payment snapshot is outside the case balance limits."); OutstandingBalance = outstandingBefore; OverdueBalance = overdueBefore; TotalDue = OverdueBalance + Penalties + Fees; UpdatedAt = now; }
    public void ApplyImportedBalances(decimal outstanding, decimal overdue, int daysPastDue, Guid bucketId, DateTimeOffset now)
    { if (outstanding < 0 || overdue < 0 || daysPastDue < 0 || bucketId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(outstanding)); OutstandingBalance = outstanding; OverdueBalance = overdue; TotalDue = overdue + Penalties + Fees; DaysPastDue = daysPastDue; CurrentBucketId = bucketId; UpdatedAt = now; }
    public void ApplyImportedReferences(string? contractReference, string? productType, DateTimeOffset now)
    { ContractReference = string.IsNullOrWhiteSpace(contractReference) ? ContractReference : contractReference.Trim(); ProductType = string.IsNullOrWhiteSpace(productType) ? ProductType : productType.Trim(); UpdatedAt = now; }
    public void LinkImport(Guid importId) { if (importId == Guid.Empty) throw new ArgumentException("Import is required.", nameof(importId)); SourceImportId = importId; }
    public void UpdatePortfolioCase(string status, DateTimeOffset? nextFollowUpAt, DateTimeOffset now)
    { ArgumentException.ThrowIfNullOrWhiteSpace(status); Status = status.Trim().ToUpperInvariant(); NextFollowUpAt = nextFollowUpAt; UpdatedAt = now; }
    public void Archive(string reason, string? notes, Guid archivedById, DateTimeOffset now)
    { if (IsArchived) throw new InvalidOperationException("The case is already archived."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); IsArchived = true; ArchivedAt = now; ArchivedById = archivedById; ArchiveReason = reason.Trim().ToUpperInvariant(); ArchiveNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); UpdatedAt = now; }
    public void Restore(string reason, Guid restoredById, DateTimeOffset now)
    { if (!IsArchived) throw new InvalidOperationException("The case is not archived."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); IsArchived = false; RestoredAt = now; RestoredById = restoredById; RestoreReason = reason.Trim(); AssignedCollectorId = null; AssignedTeamId = null; NextFollowUpAt = null; UpdatedAt = now; }
}

public sealed class CaseBucketHistory
{
    private CaseBucketHistory() { }
    public CaseBucketHistory(Guid caseId, Guid? previousBucketId, Guid newBucketId, string reason, string source, Guid? changedById, DateTimeOffset changedAt)
    { Id = Guid.NewGuid(); CaseId = caseId; PreviousBucketId = previousBucketId; NewBucketId = newBucketId; Reason = reason.Trim(); Source = source.Trim().ToUpperInvariant(); ChangedById = changedById; ChangedAt = changedAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!;
    public Guid? PreviousBucketId { get; private set; } public Guid NewBucketId { get; private set; } public string Reason { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty; public Guid? ChangedById { get; private set; } public User? ChangedBy { get; private set; } public DateTimeOffset ChangedAt { get; private set; }
}

public sealed class CollectionAssignmentHistory
{
    private CollectionAssignmentHistory() { }
    public CollectionAssignmentHistory(Guid caseId, Guid? previousAssigneeId, Guid? assignedToId, Guid assignedById, Guid? teamId, string reason, string source, string? ruleCode, DateTimeOffset assignedAt)
    { Id = Guid.NewGuid(); CaseId = caseId; PreviousAssigneeId = previousAssigneeId; AssignedToId = assignedToId; AssignedById = assignedById; TeamId = teamId; Reason = reason.Trim(); Source = source.Trim().ToUpperInvariant(); RuleCode = string.IsNullOrWhiteSpace(ruleCode) ? null : ruleCode.Trim(); AssignedAt = assignedAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!;
    public Guid? PreviousAssigneeId { get; private set; } public User? PreviousAssignee { get; private set; } public Guid? AssignedToId { get; private set; } public User? AssignedTo { get; private set; }
    public Guid AssignedById { get; private set; } public User AssignedBy { get; private set; } = null!; public Guid? TeamId { get; private set; } public CollectionTeam? Team { get; private set; }
    public string Reason { get; private set; } = string.Empty; public string Source { get; private set; } = string.Empty; public string? RuleCode { get; private set; } public DateTimeOffset AssignedAt { get; private set; }
}

public sealed class CollectionActivity
{
    private CollectionActivity() { }
    public CollectionActivity(Guid caseId, string activityType, string? result, string? notes, string? channel, Guid createdById, DateTimeOffset createdAt, DateTimeOffset? nextFollowUpAt)
    { Id = Guid.NewGuid(); CaseId = caseId; ActivityType = activityType.Trim().ToUpperInvariant(); Result = Normalize(result); Notes = Normalize(notes); Channel = Normalize(channel); CreatedById = createdById; CreatedAt = createdAt; NextFollowUpAt = nextFollowUpAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!; public string ActivityType { get; private set; } = string.Empty;
    public string? Result { get; private set; } public string? Notes { get; private set; } public string? Channel { get; private set; } public Guid CreatedById { get; private set; } public User CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; } public DateTimeOffset? NextFollowUpAt { get; private set; } private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PromiseToPay
{
    private PromiseToPay() { }
    public PromiseToPay(Guid caseId, decimal promisedAmount, DateOnly promiseDate, Guid collectorId, string channel, string? notes, DateTimeOffset createdAt)
    { if (promisedAmount <= 0) throw new ArgumentOutOfRangeException(nameof(promisedAmount)); Id = Guid.NewGuid(); CaseId = caseId; PromisedAmount = promisedAmount; PromiseDate = promiseDate; CollectorId = collectorId; Channel = channel.Trim(); Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); Status = CollectionsValues.PromiseStatuses.Active; CreatedAt = createdAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!; public decimal PromisedAmount { get; private set; }
    public DateOnly PromiseDate { get; private set; } public Guid CollectorId { get; private set; } public User Collector { get; private set; } = null!; public string Channel { get; private set; } = string.Empty;
    public string? Notes { get; private set; } public decimal ActualPaidAmount { get; private set; } public string Status { get; private set; } = string.Empty; public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? FulfilledAt { get; private set; } public DateTimeOffset? EvaluatedAt { get; private set; }
    public void ApplyEvaluation(string status, decimal actualPaidAmount, DateTimeOffset evaluatedAt) { Status = status; ActualPaidAmount = actualPaidAmount; EvaluatedAt = evaluatedAt; if (status == CollectionsValues.PromiseStatuses.Fulfilled) FulfilledAt = evaluatedAt; }
    public void Transition(string status, DateTimeOffset changedAt)
    {
        if (Status != CollectionsValues.PromiseStatuses.Active) throw new InvalidOperationException("Only a pending promise can be changed.");
        if (status is not (CollectionsValues.PromiseStatuses.Fulfilled or CollectionsValues.PromiseStatuses.Broken or CollectionsValues.PromiseStatuses.Cancelled)) throw new ArgumentException("Promise status transition is invalid.", nameof(status));
        Status = status; EvaluatedAt = changedAt; if (status == CollectionsValues.PromiseStatuses.Fulfilled) FulfilledAt = changedAt;
    }
}

public sealed class CollectionPayment
{
    private CollectionPayment() { }
    public CollectionPayment(Guid caseId, decimal amount, DateOnly paymentDate, string method, string referenceNumber, Guid submittedById, string? proofStorageKey, DateTimeOffset submittedAt, string currencyCode = "EGP")
    { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); Id = Guid.NewGuid(); CaseId = caseId; Amount = amount; PaymentDate = paymentDate; Method = method.Trim().ToUpperInvariant(); ReferenceNumber = referenceNumber.Trim(); SubmittedById = submittedById; ProofStorageKey = proofStorageKey; CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "EGP" : currencyCode.Trim().ToUpperInvariant(); Status = CollectionsValues.PaymentStatuses.Submitted; SubmittedAt = submittedAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!; public decimal Amount { get; private set; } public DateOnly PaymentDate { get; private set; }
    public string Method { get; private set; } = string.Empty; public string ReferenceNumber { get; private set; } = string.Empty; public Guid SubmittedById { get; private set; } public User SubmittedBy { get; private set; } = null!;
    public string? ProofStorageKey { get; private set; } public string Status { get; private set; } = string.Empty; public DateTimeOffset SubmittedAt { get; private set; } public Guid? VerifiedById { get; private set; }
    public User? VerifiedBy { get; private set; } public DateTimeOffset? VerifiedAt { get; private set; } public string? RejectionReason { get; private set; }
    public string CurrencyCode { get; private set; } = "EGP";
    public Guid? FinancialJournalEntryId { get; private set; }
    public Guid? FinancialReversalJournalEntryId { get; private set; }
    public void Review(Guid reviewerId, bool approve, string? rejectionReason, bool enforceSeparationOfDuties, DateTimeOffset reviewedAt)
    { if (Status is not (CollectionsValues.PaymentStatuses.Submitted or CollectionsValues.PaymentStatuses.UnderReview)) throw new InvalidOperationException("Only pending payments can be reviewed."); if (enforceSeparationOfDuties && reviewerId == SubmittedById) throw new InvalidOperationException("The maker cannot approve their own payment."); if (!approve && string.IsNullOrWhiteSpace(rejectionReason)) throw new ArgumentException("A rejection reason is required.", nameof(rejectionReason)); VerifiedById = reviewerId; VerifiedAt = reviewedAt; RejectionReason = approve ? null : rejectionReason!.Trim(); Status = approve ? CollectionsValues.PaymentStatuses.Approved : CollectionsValues.PaymentStatuses.Rejected; }
    public void MarkFinanciallyPosted(Guid journalEntryId) { if (Status != CollectionsValues.PaymentStatuses.Approved) throw new InvalidOperationException("Only an approved payment can be financially posted."); if (FinancialJournalEntryId.HasValue && FinancialJournalEntryId != journalEntryId) throw new InvalidOperationException("The payment is already linked to another journal."); FinancialJournalEntryId = journalEntryId; }
    public void MarkFinanciallyReversed(Guid reversalJournalEntryId) { if (!FinancialJournalEntryId.HasValue) throw new InvalidOperationException("An unposted payment cannot be reversed."); if (FinancialReversalJournalEntryId.HasValue) throw new InvalidOperationException("The payment is already reversed."); FinancialReversalJournalEntryId = reversalJournalEntryId; Status = "REVERSED"; }
}

public sealed class FieldVisit
{
    private FieldVisit() { }
    public FieldVisit(Guid caseId, Guid collectorId, DateTimeOffset scheduledAt, string address, string? governorate, string? area, Guid createdById, DateTimeOffset createdAt, string? purpose = null, string? notes = null)
    { if (caseId == Guid.Empty || collectorId == Guid.Empty || createdById == Guid.Empty) throw new ArgumentException("Case, collector, and creator are required."); ArgumentException.ThrowIfNullOrWhiteSpace(address); Id = Guid.NewGuid(); CaseId = caseId; CollectorId = collectorId; ScheduledAt = scheduledAt; Address = address.Trim(); Governorate = Normalize(governorate); Area = Normalize(area); Purpose = Normalize(purpose); Notes = Normalize(notes); CreatedById = createdById; Status = CollectionsValues.VisitStatuses.Scheduled; CreatedAt = createdAt; UpdatedAt = createdAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!; public Guid CollectorId { get; private set; } public User Collector { get; private set; } = null!;
    public DateTimeOffset ScheduledAt { get; private set; } public string Status { get; private set; } = string.Empty; public string Address { get; private set; } = string.Empty; public string? Governorate { get; private set; } public string? Area { get; private set; }
    public decimal? CheckInLatitude { get; private set; } public decimal? CheckInLongitude { get; private set; } public DateTimeOffset? CheckedInAt { get; private set; } public DateTimeOffset? CheckedOutAt { get; private set; }
    public string? Purpose { get; private set; } public string? Result { get; private set; } public string? Notes { get; private set; } public Guid CreatedById { get; private set; } public User CreatedBy { get; private set; } = null!; public DateTimeOffset CreatedAt { get; private set; } public DateTimeOffset UpdatedAt { get; private set; }
    public void Start(DateTimeOffset now) { EnsureActive(); Status = CollectionsValues.VisitStatuses.InProgress; UpdatedAt = now; }
    public void Complete(string result, string? notes, DateTimeOffset completedAt)
    { EnsureActive(); ArgumentException.ThrowIfNullOrWhiteSpace(result); Status = CollectionsValues.VisitStatuses.Completed; Result = result.Trim().ToUpperInvariant(); Notes = Normalize(notes) ?? Notes; CheckedOutAt = completedAt; UpdatedAt = completedAt; }
    public void Reschedule(DateTimeOffset scheduledAt, DateTimeOffset now) { EnsureActive(); ScheduledAt = scheduledAt; Status = CollectionsValues.VisitStatuses.Scheduled; UpdatedAt = now; }
    public void Reassign(Guid collectorId, DateTimeOffset now) { EnsureActive(); if (collectorId == Guid.Empty) throw new ArgumentException("Collector is required.", nameof(collectorId)); CollectorId = collectorId; UpdatedAt = now; }
    public void Cancel(string? notes, DateTimeOffset now) { EnsureActive(); Status = CollectionsValues.VisitStatuses.Cancelled; Notes = Normalize(notes) ?? Notes; UpdatedAt = now; }
    public void MarkMissed(string? notes, DateTimeOffset now) { EnsureActive(); Status = CollectionsValues.VisitStatuses.Missed; Notes = Normalize(notes) ?? Notes; UpdatedAt = now; }
    private void EnsureActive() { if (Status is CollectionsValues.VisitStatuses.Completed or CollectionsValues.VisitStatuses.Cancelled or CollectionsValues.VisitStatuses.Missed) throw new InvalidOperationException("The visit is already final."); }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CollectionDcr
{
    private CollectionDcr() { }
    public CollectionDcr(Guid bankId, Guid caseId, Guid createdByUserId, DateOnly dcrDate, string actionCover, string action,
        string feedback, string? comment, DateOnly? ptpDate, decimal? ptpAmount, DateOnly? paidDate, decimal? paidAmount,
        DateTimeOffset? followUpAt, DateOnly? visitDate, Guid? linkedPtpId, Guid? linkedVisitId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid(); BankId = bankId; CaseId = caseId; CreatedByUserId = createdByUserId; DcrDate = dcrDate;
        ActionCover = actionCover.Trim().ToUpperInvariant().Replace(' ', '_'); Action = action.Trim().ToUpperInvariant().Replace(' ', '_'); Feedback = feedback.Trim();
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(); PtpDate = ptpDate; PtpAmount = ptpAmount;
        PaidDate = paidDate; PaidAmount = paidAmount; FollowUpAt = followUpAt; VisitDate = visitDate;
        LinkedPtpId = linkedPtpId; LinkedVisitId = linkedVisitId; CreatedAt = createdAt; UpdatedAt = createdAt;
    }
    public Guid Id { get; private set; }
    public Guid BankId { get; private set; } public ClientOrganization Bank { get; private set; } = null!;
    public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; } public User CreatedByUser { get; private set; } = null!;
    public DateOnly DcrDate { get; private set; } public string ActionCover { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty; public string Feedback { get; private set; } = string.Empty;
    public string? Comment { get; private set; } public DateOnly? PtpDate { get; private set; } public decimal? PtpAmount { get; private set; }
    public DateOnly? PaidDate { get; private set; } public decimal? PaidAmount { get; private set; }
    public DateTimeOffset? FollowUpAt { get; private set; } public DateOnly? VisitDate { get; private set; }
    public Guid? LinkedPtpId { get; private set; } public PromiseToPay? LinkedPtp { get; private set; }
    public Guid? LinkedVisitId { get; private set; } public FieldVisit? LinkedVisit { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } public DateTimeOffset UpdatedAt { get; private set; }
    public void LinkPtp(Guid id, DateTimeOffset now) { LinkedPtpId = id; UpdatedAt = now; }
}

public sealed class CollectionComplaint
{
    private CollectionComplaint() { }
    public CollectionComplaint(Guid caseId, string reference, string source, string category, string severity, string description, DateTimeOffset receivedAt, DateTimeOffset slaDueAt, Guid ownerId, Guid createdById)
        : this(caseId, reference, source, category, severity, description, receivedAt, slaDueAt, ownerId, createdById, receivedAt) { }
    public CollectionComplaint(Guid caseId, string reference, string source, string category, string severity, string description, DateTimeOffset receivedAt, DateTimeOffset? slaDueAt, Guid? ownerId, Guid createdById, DateTimeOffset createdAt, Guid? id = null)
    { if (caseId == Guid.Empty || createdById == Guid.Empty) throw new ArgumentException("Case and creator are required."); ArgumentException.ThrowIfNullOrWhiteSpace(reference); ArgumentException.ThrowIfNullOrWhiteSpace(category); ArgumentException.ThrowIfNullOrWhiteSpace(description); Id = id ?? Guid.NewGuid(); CaseId = caseId; Reference = reference.Trim().ToUpperInvariant(); Source = string.IsNullOrWhiteSpace(source) ? "INTERNAL" : source.Trim().ToUpperInvariant(); Category = category.Trim().ToUpperInvariant(); Severity = severity.Trim().ToUpperInvariant(); Description = description.Trim(); ReceivedAt = receivedAt; SlaDueAt = slaDueAt; OwnerId = ownerId; CreatedById = createdById; Status = ownerId.HasValue ? CollectionsValues.ComplaintStatuses.InProgress : CollectionsValues.ComplaintStatuses.Open; UpdatedAt = createdAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!; public string Reference { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty; public string Category { get; private set; } = string.Empty; public string Severity { get; private set; } = string.Empty; public string Description { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; } public DateTimeOffset? SlaDueAt { get; private set; } public string Status { get; private set; } = string.Empty; public Guid? OwnerId { get; private set; } public User? Owner { get; private set; }
    public Guid CreatedById { get; private set; } public User CreatedBy { get; private set; } = null!; public string? Resolution { get; private set; } public Guid? ResolvedById { get; private set; } public User? ResolvedBy { get; private set; } public DateTimeOffset? ResolvedAt { get; private set; } public DateTimeOffset? ClosedAt { get; private set; } public string? RejectionReason { get; private set; } public DateTimeOffset UpdatedAt { get; private set; }
    public void Assign(Guid ownerId, DateTimeOffset changedAt) { if (ownerId == Guid.Empty) throw new ArgumentException("Assignee is required.", nameof(ownerId)); if (Status is CollectionsValues.ComplaintStatuses.Resolved or CollectionsValues.ComplaintStatuses.Closed or CollectionsValues.ComplaintStatuses.Rejected) throw new InvalidOperationException("A final complaint cannot be assigned."); OwnerId = ownerId; Status = CollectionsValues.ComplaintStatuses.InProgress; UpdatedAt = changedAt; }
    public void ChangePriority(string priority, DateTimeOffset changedAt) { var value = priority.Trim().ToUpperInvariant(); if (value is not (CollectionsValues.ComplaintPriorities.Low or CollectionsValues.ComplaintPriorities.Medium or CollectionsValues.ComplaintPriorities.High or CollectionsValues.ComplaintPriorities.Critical)) throw new ArgumentException("Complaint priority is invalid.", nameof(priority)); Severity = value; UpdatedAt = changedAt; }
    public void Start(DateTimeOffset changedAt) { if (Status is not (CollectionsValues.ComplaintStatuses.Open or CollectionsValues.ComplaintStatuses.New)) throw new InvalidOperationException("Only an open complaint can be started."); Status = CollectionsValues.ComplaintStatuses.InProgress; UpdatedAt = changedAt; }
    public void Resolve(string resolution, Guid resolvedById, DateTimeOffset changedAt) { if (Status != CollectionsValues.ComplaintStatuses.InProgress) throw new InvalidOperationException("Only an in-progress complaint can be resolved."); ArgumentException.ThrowIfNullOrWhiteSpace(resolution); Resolution = resolution.Trim(); ResolvedById = resolvedById; ResolvedAt = changedAt; Status = CollectionsValues.ComplaintStatuses.Resolved; UpdatedAt = changedAt; }
    public void Close(DateTimeOffset changedAt) { if (Status != CollectionsValues.ComplaintStatuses.Resolved) throw new InvalidOperationException("Only a resolved complaint can be closed."); Status = CollectionsValues.ComplaintStatuses.Closed; ClosedAt = changedAt; UpdatedAt = changedAt; }
    public void Reopen(string reason, DateTimeOffset changedAt) { if (Status is not (CollectionsValues.ComplaintStatuses.Resolved or CollectionsValues.ComplaintStatuses.Closed)) throw new InvalidOperationException("Only a resolved or closed complaint can be reopened."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); Status = CollectionsValues.ComplaintStatuses.InProgress; ClosedAt = null; UpdatedAt = changedAt; }
    public void Reject(string reason, DateTimeOffset changedAt) { if (Status is not (CollectionsValues.ComplaintStatuses.Open or CollectionsValues.ComplaintStatuses.New or CollectionsValues.ComplaintStatuses.InProgress)) throw new InvalidOperationException("This complaint cannot be rejected."); ArgumentException.ThrowIfNullOrWhiteSpace(reason); RejectionReason = reason.Trim(); Status = CollectionsValues.ComplaintStatuses.Rejected; UpdatedAt = changedAt; }
    public void ChangeStatus(string status, string? resolution, DateTimeOffset changedAt)
    {
        var allowed = new[] { CollectionsValues.ComplaintStatuses.Assigned, CollectionsValues.ComplaintStatuses.InProgress, CollectionsValues.ComplaintStatuses.AwaitingInformation, CollectionsValues.ComplaintStatuses.Resolved, CollectionsValues.ComplaintStatuses.Reopened, CollectionsValues.ComplaintStatuses.Closed, CollectionsValues.ComplaintStatuses.Escalated };
        var normalized = status.Trim().ToUpperInvariant(); if (!allowed.Contains(normalized)) throw new ArgumentException("Complaint status is invalid.", nameof(status));
        if ((normalized == CollectionsValues.ComplaintStatuses.Resolved || normalized == CollectionsValues.ComplaintStatuses.Closed) && string.IsNullOrWhiteSpace(resolution)) throw new ArgumentException("Resolution is required.", nameof(resolution));
        Status = normalized; Resolution = string.IsNullOrWhiteSpace(resolution) ? Resolution : resolution.Trim(); ClosedAt = normalized == CollectionsValues.ComplaintStatuses.Closed ? changedAt : null;
    }
}

public sealed class CollectionComplaintNote
{
    private CollectionComplaintNote() { }
    public CollectionComplaintNote(Guid complaintId, string text, Guid createdById, DateTimeOffset createdAt) { if (complaintId == Guid.Empty || createdById == Guid.Empty) throw new ArgumentException("Complaint and author are required."); ArgumentException.ThrowIfNullOrWhiteSpace(text); Id = Guid.NewGuid(); ComplaintId = complaintId; Text = text.Trim(); CreatedById = createdById; CreatedAt = createdAt; }
    public Guid Id { get; private set; } public Guid ComplaintId { get; private set; } public CollectionComplaint Complaint { get; private set; } = null!; public string Text { get; private set; } = string.Empty; public Guid CreatedById { get; private set; } public User CreatedBy { get; private set; } = null!; public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class CollectionAuditLog
{
    private CollectionAuditLog() { }
    public CollectionAuditLog(Guid? userId, string action, string entityType, Guid entityId, Guid? caseId, string? beforeJson, string? afterJson, string? source, DateTimeOffset occurredAt)
    { Id = Guid.NewGuid(); UserId = userId; Action = action.Trim(); EntityType = entityType.Trim(); EntityId = entityId; CaseId = caseId; BeforeJson = beforeJson; AfterJson = afterJson; Source = source; OccurredAt = occurredAt; }
    public Guid Id { get; private set; } public Guid? UserId { get; private set; } public User? User { get; private set; } public string Action { get; private set; } = string.Empty; public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; } public Guid? CaseId { get; private set; } public CollectionCase? Case { get; private set; } public string? BeforeJson { get; private set; } public string? AfterJson { get; private set; }
    public string? Source { get; private set; } public DateTimeOffset OccurredAt { get; private set; }
}

public sealed class CollectionImportBatch
{
    private CollectionImportBatch() { }
    public CollectionImportBatch(Guid organizationId, Guid portfolioId, string fileName, string contentType, long fileSize, string fileHash, string storageKey, Guid uploadedById, DateTimeOffset uploadedAt)
    { Id = Guid.NewGuid(); OrganizationId = organizationId; PortfolioId = portfolioId; FileName = fileName.Trim(); ContentType = contentType.Trim(); FileSize = fileSize; FileHash = fileHash.Trim(); StorageKey = storageKey.Trim(); UploadedById = uploadedById; UploadedAt = uploadedAt; Status = "UPLOADED"; }
    public Guid Id { get; private set; } public Guid OrganizationId { get; private set; } public ClientOrganization Organization { get; private set; } = null!; public Guid PortfolioId { get; private set; } public CollectionPortfolio Portfolio { get; private set; } = null!;
    public string FileName { get; private set; } = string.Empty; public string ContentType { get; private set; } = string.Empty; public long FileSize { get; private set; } public string FileHash { get; private set; } = string.Empty; public string StorageKey { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty; public int TotalRows { get; private set; } public int ValidRows { get; private set; } public int InvalidRows { get; private set; } public int InsertedRows { get; private set; } public int UpdatedRows { get; private set; } public int SkippedRows { get; private set; }
    public Guid UploadedById { get; private set; } public User UploadedBy { get; private set; } = null!; public DateTimeOffset UploadedAt { get; private set; } public DateTimeOffset? PreviewedAt { get; private set; } public DateTimeOffset? ConfirmedAt { get; private set; } public string? FailureReason { get; private set; }
    public void SetPreview(int total, int valid, int invalid, DateTimeOffset at) { if (Status != "UPLOADED") throw new InvalidOperationException("Only uploaded imports can be previewed."); TotalRows = total; ValidRows = valid; InvalidRows = invalid; Status = "PREVIEW_READY"; PreviewedAt = at; }
    public void Confirm(int inserted, int updated, int skipped, DateTimeOffset at) { if (Status != "PREVIEW_READY") throw new InvalidOperationException("Only previewed imports can be confirmed."); InsertedRows = inserted; UpdatedRows = updated; SkippedRows = skipped; Status = "COMPLETED"; ConfirmedAt = at; }
    public void Fail(string reason, DateTimeOffset at) { if (Status == "COMPLETED") throw new InvalidOperationException("A completed import cannot fail."); FailureReason = reason.Trim(); Status = "FAILED"; ConfirmedAt = at; }
}

public sealed class CollectionImportRow
{
    private CollectionImportRow() { }
    public CollectionImportRow(Guid batchId, int rowNumber, string accountReference, string customerCode, string? nameArabic, string? nameEnglish, string? nationalId, string? phone, string? contractReference, string? productType, decimal? outstanding, decimal? overdue, int? daysPastDue, string rawJson, string errorsJson, bool isValid, DateTimeOffset createdAt)
    { Id = Guid.NewGuid(); BatchId = batchId; RowNumber = rowNumber; AccountReference = accountReference; CustomerCode = customerCode; NameArabic = nameArabic; NameEnglish = nameEnglish; NationalId = nationalId; Phone = phone; ContractReference = contractReference; ProductType = productType; OutstandingBalance = outstanding; OverdueBalance = overdue; DaysPastDue = daysPastDue; RawJson = rawJson; ErrorsJson = errorsJson; IsValid = isValid; CreatedAt = createdAt; }
    public Guid Id { get; private set; } public Guid BatchId { get; private set; } public CollectionImportBatch Batch { get; private set; } = null!; public int RowNumber { get; private set; }
    public string AccountReference { get; private set; } = string.Empty; public string CustomerCode { get; private set; } = string.Empty; public string? NameArabic { get; private set; } public string? NameEnglish { get; private set; } public string? NationalId { get; private set; } public string? Phone { get; private set; }
    public string? ContractReference { get; private set; } public string? ProductType { get; private set; } public decimal? OutstandingBalance { get; private set; } public decimal? OverdueBalance { get; private set; } public int? DaysPastDue { get; private set; }
    public string RawJson { get; private set; } = "{}"; public string ErrorsJson { get; private set; } = "[]"; public bool IsValid { get; private set; } public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class CollectionAttachment
{
    private CollectionAttachment() { }
    public CollectionAttachment(Guid caseId, Guid? paymentId, string category, string originalFileName, string contentType, long fileSize, string fileHash, string storageKey, Guid uploadedById, DateTimeOffset uploadedAt)
    { if (caseId == Guid.Empty || uploadedById == Guid.Empty) throw new ArgumentException("Case and uploader are required."); if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize)); var safeName = originalFileName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim(); if (string.IsNullOrWhiteSpace(safeName)) throw new ArgumentException("A safe original file name is required.", nameof(originalFileName)); Id = Guid.NewGuid(); CaseId = caseId; PaymentId = paymentId; Category = category.Trim().ToUpperInvariant(); OriginalFileName = string.Concat(safeName.Where(c => !char.IsControl(c))); ContentType = contentType.Trim(); FileSize = fileSize; FileHash = fileHash.Trim(); StorageKey = storageKey.Trim(); UploadedById = uploadedById; UploadedAt = uploadedAt; }
    public Guid Id { get; private set; } public Guid CaseId { get; private set; } public CollectionCase Case { get; private set; } = null!; public Guid? PaymentId { get; private set; } public CollectionPayment? Payment { get; private set; } public Guid? ComplaintId { get; private set; } public CollectionComplaint? Complaint { get; private set; }
    public string Category { get; private set; } = string.Empty; public string OriginalFileName { get; private set; } = string.Empty; public string ContentType { get; private set; } = string.Empty; public long FileSize { get; private set; } public string FileHash { get; private set; } = string.Empty; public string StorageKey { get; private set; } = string.Empty;
    public Guid UploadedById { get; private set; } public User UploadedBy { get; private set; } = null!; public DateTimeOffset UploadedAt { get; private set; }
    public void LinkComplaint(Guid complaintId) { if (complaintId == Guid.Empty) throw new ArgumentException("Complaint is required.", nameof(complaintId)); ComplaintId = complaintId; }
}
