namespace MIS.Domain.Entities;

/// <summary>
/// An explicitly prepared, immutable ACT source/rule snapshot. Separate from manual scores.
/// Creation/approval ingestion is intentionally not exposed by the Phase 1 API.
/// </summary>
public sealed class ActScorePeriod
{
    private ActScorePeriod() { }

    public ActScorePeriod(Guid organizationId, Guid portfolioId, DateOnly period, string currency,
        string rulesJson, string inputJson, Guid preparedBy, DateTimeOffset preparedAt)
    {
        if (organizationId == Guid.Empty || portfolioId == Guid.Empty || preparedBy == Guid.Empty || period.Day != 1)
            throw new ArgumentException("An explicit organization, portfolio, month and preparer are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(rulesJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputJson);
        Id = Guid.NewGuid(); OrganizationId = organizationId; PortfolioId = portfolioId;
        ReportingPeriod = period; Currency = currency; RulesJson = rulesJson; InputJson = inputJson;
        PreparedBy = preparedBy; PreparedAt = preparedAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public DateOnly ReportingPeriod { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string RulesJson { get; private set; } = string.Empty;
    public string InputJson { get; private set; } = string.Empty;
    public Guid PreparedBy { get; private set; }
    public DateTimeOffset PreparedAt { get; private set; }
}
