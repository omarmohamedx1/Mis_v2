namespace MIS.Domain.Entities;

public sealed class CollectionScoreValue
{
    private CollectionScoreValue() { }

    public CollectionScoreValue(
        Guid organizationId,
        Guid portfolioId,
        string organizationType,
        string portfolioType,
        DateOnly reportingPeriod,
        string bucket,
        string category,
        DateTimeOffset modifiedAt,
        Guid modifiedByUserId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PortfolioId = portfolioId;
        OrganizationType = organizationType;
        PortfolioType = portfolioType;
        ReportingPeriod = reportingPeriod;
        Bucket = bucket;
        Category = category;
        ModifiedAt = modifiedAt;
        ModifiedByUserId = modifiedByUserId;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ClientOrganization Organization { get; private set; } = null!;
    public Guid? PortfolioId { get; private set; }
    public CollectionPortfolio? Portfolio { get; private set; }
    public string OrganizationType { get; private set; } = string.Empty;
    public string PortfolioType { get; private set; } = string.Empty;
    public DateOnly ReportingPeriod { get; private set; }
    public string Bucket { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public int? CaseCount { get; private set; }
    public decimal? ActualBalance { get; private set; }
    public decimal? TargetPercent { get; private set; }
    public decimal? TargetBalance { get; private set; }
    public decimal? Missing { get; private set; }
    public decimal? Percent { get; private set; }
    public decimal? Weight { get; private set; }
    public decimal? ActualVsTarget { get; private set; }
    public decimal? Score { get; private set; }
    public Guid ModifiedByUserId { get; private set; }
    public User ModifiedByUser { get; private set; } = null!;
    public DateTimeOffset ModifiedAt { get; private set; }
    public Guid Version { get; private set; }

    public void Update(
        int? caseCount,
        decimal? actualBalance,
        decimal? targetPercent,
        decimal? targetBalance,
        decimal? missing,
        decimal? percent,
        decimal? weight,
        decimal? actualVsTarget,
        decimal? score,
        DateTimeOffset modifiedAt,
        Guid modifiedByUserId)
    {
        CaseCount = caseCount;
        ActualBalance = actualBalance;
        TargetPercent = targetPercent;
        TargetBalance = targetBalance;
        Missing = missing;
        Percent = percent;
        Weight = weight;
        ActualVsTarget = actualVsTarget;
        Score = score;
        ModifiedAt = modifiedAt;
        ModifiedByUserId = modifiedByUserId;
        Version = Guid.NewGuid();
    }
}

public sealed class CollectionScoreAudit
{
    private CollectionScoreAudit() { }

    public CollectionScoreAudit(
        Guid scoreValueId,
        Guid organizationId,
        Guid portfolioId,
        string organizationType,
        string portfolioType,
        DateOnly reportingPeriod,
        string bucket,
        string category,
        Guid modifiedByUserId,
        DateTimeOffset modifiedAt,
        string? beforeJson,
        string afterJson)
    {
        Id = Guid.NewGuid();
        ScoreValueId = scoreValueId;
        OrganizationId = organizationId;
        PortfolioId = portfolioId;
        OrganizationType = organizationType;
        PortfolioType = portfolioType;
        ReportingPeriod = reportingPeriod;
        Bucket = bucket;
        Category = category;
        ModifiedByUserId = modifiedByUserId;
        ModifiedAt = modifiedAt;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
    }

    public Guid Id { get; private set; }
    public Guid ScoreValueId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? PortfolioId { get; private set; }
    public CollectionPortfolio? Portfolio { get; private set; }
    public string OrganizationType { get; private set; } = string.Empty;
    public string PortfolioType { get; private set; } = string.Empty;
    public DateOnly ReportingPeriod { get; private set; }
    public string Bucket { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public Guid ModifiedByUserId { get; private set; }
    public User ModifiedByUser { get; private set; } = null!;
    public DateTimeOffset ModifiedAt { get; private set; }
    public string? BeforeJson { get; private set; }
    public string AfterJson { get; private set; } = "{}";
}
