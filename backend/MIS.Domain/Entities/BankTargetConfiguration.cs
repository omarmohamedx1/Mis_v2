namespace MIS.Domain.Entities;

public sealed class BankTargetConfiguration
{
    private BankTargetConfiguration() { }
    public BankTargetConfiguration(Guid organizationId, Guid portfolioId, string portfolioType, DateOnly period,
        string valuesJson, Guid actor, DateTimeOffset now, Guid? copiedFromId = null)
    {
        Id = Guid.NewGuid(); OrganizationId = organizationId; PortfolioId = portfolioId;
        PortfolioType = portfolioType; ReportingPeriod = period; ValuesJson = valuesJson;
        CreatedBy = actor; CreatedAt = now; UpdatedBy = actor; UpdatedAt = now; CopiedFromId = copiedFromId;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public string PortfolioType { get; private set; } = string.Empty;
    public DateOnly ReportingPeriod { get; private set; }
    public string ValuesJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = "DRAFT";
    public Guid Version { get; private set; } = Guid.NewGuid();
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? CopiedFromId { get; private set; }

    public void Edit(string json, Guid actor, DateTimeOffset now)
    {
        RequireMutable(); ValuesJson = json; Status = "DRAFT"; ReviewedBy = null; ReviewedAt = null;
        Touch(actor, now);
    }
    public void Review(Guid actor, DateTimeOffset now)
    {
        RequireMutable(); Status = "REVIEWED"; ReviewedBy = actor; ReviewedAt = now; Touch(actor, now);
    }
    public void Approve(Guid actor, DateTimeOffset now)
    {
        RequireMutable();
        if (Status != "REVIEWED") throw new InvalidOperationException("Review is required before approval.");
        Status = "APPROVED"; ApprovedBy = actor; ApprovedAt = now; Touch(actor, now);
    }
    private void RequireMutable()
    {
        if (Status == "APPROVED") throw new InvalidOperationException("Approved targets are immutable.");
    }
    private void Touch(Guid actor, DateTimeOffset now)
    { UpdatedBy = actor; UpdatedAt = now; Version = Guid.NewGuid(); }
}

public sealed class BankTargetAudit
{
    private BankTargetAudit() { }
    public BankTargetAudit(BankTargetConfiguration configuration, string action, Guid actor, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); ConfigurationId = configuration.Id; Action = action; ActorId = actor;
        OccurredAt = now; Version = configuration.Version; Status = configuration.Status; ValuesJson = configuration.ValuesJson;
    }
    public Guid Id { get; private set; }
    public Guid ConfigurationId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid Version { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string ValuesJson { get; private set; } = string.Empty;
}
