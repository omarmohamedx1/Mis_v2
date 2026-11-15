namespace MIS.Application.Scoring;

// Decimal ratios use 0..1, weights use score points. No institution-specific defaults.
public sealed record ActScoreScope(Guid OrganizationId, Guid PortfolioId, DateOnly Period, string Currency);
public sealed record ActMovementRule(int Bucket, string Movement, decimal Target, decimal Weight);
public sealed record ActScoreRules(
    ActScoreScope Scope, string Version, Guid ApprovedBy, DateTimeOffset ApprovedAt,
    IReadOnlyList<ActMovementRule> Movements, decimal MoreTarget, decimal ActiveWeight,
    decimal MoreWeight, int MovementDecimalPlaces, bool AllowNegativeStabilityTarget = false,
    bool AllowSignedDues = false, bool AllowEndingBucketOverrides = false);

public sealed record ActEndingBucketOverride(int EndingBucket, string Reason, Guid ApprovedBy, DateTimeOffset ApprovedAt);

public sealed record ActOpeningCase(
    Guid CaseId, ActScoreScope Scope, int Bucket, decimal Balance, decimal StartingDues,
    IReadOnlyList<decimal> Dues, Guid? CollectorId, string SourceReference, ActEndingBucketOverride? EndingOverride = null);

// Payment facts are frozen as of the source cutoff, including approval and reversal times.
public sealed record ActPaymentFact(
    Guid PaymentId, Guid CaseId, ActScoreScope Scope, DateOnly PaymentDate, decimal Amount,
    DateTimeOffset? ApprovedAt, DateTimeOffset? ReversedAt);

public sealed record ActScoreInput(
    ActScoreScope Scope, bool OpeningComplete, bool PaymentsComplete,
    Guid ReconciledBy, DateTimeOffset Cutoff, string SourceVersion,
    IReadOnlyList<ActOpeningCase> Cases, IReadOnlyList<ActPaymentFact> Payments);

public sealed record ActScoreCell(
    string Category, int CaseCount, decimal ActualBalance, decimal? TargetPercent,
    decimal? TargetBalance, decimal? Missing, decimal? Percent, decimal? Weight,
    decimal? ActualVsTarget, decimal? Score, decimal? FinalScore);

public sealed record ActBucketResult(int Bucket, int CaseCount, decimal OpeningBalance,
    decimal StartingDues, decimal Paid, decimal? DisplayScore, decimal? FinalScore,
    IReadOnlyList<ActScoreCell> Cells);

public sealed record ActScoreResult(string Status, IReadOnlyList<string> Issues,
    IReadOnlyList<ActBucketResult> Buckets, decimal? TotalDueAmount, decimal? Paid,
    decimal? BalanceScore, decimal? MoreScore, decimal? ScoreAll,
    decimal? MoreTargetAmount = null, decimal? MoreActualPercent = null,
    decimal? MoreActualVsTarget = null, decimal? ActiveContribution = null, decimal? MoreContribution = null)
{
    public static ActScoreResult Unavailable(params string[] issues) =>
        new("UNAVAILABLE", issues, [], null, null, null, null, null);
}
