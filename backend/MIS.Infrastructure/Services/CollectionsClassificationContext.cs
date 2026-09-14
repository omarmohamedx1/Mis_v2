using MIS.Application.Common;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;

namespace MIS.Infrastructure.Services;

public sealed class CollectionsClassificationContext : ICollectionsClassificationContext
{
    public string? Primary { get; private set; }
    public string? Sub { get; private set; }
    public bool HasValue => Primary is not null && Sub is not null;

    public void Set(string? primary, string? sub)
    {
        Primary = PortfolioClassification.Normalize(primary);
        Sub = PortfolioClassification.Normalize(sub);
    }

    public void RequireValid()
    {
        if (Primary is null && Sub is null) return;
        if (Primary is null || Sub is null)
            throw new HrValidationException("Both the primary and sub portfolio classification are required.");
        if (!CollectionsValues.PrimaryClassifications.All.Contains(Primary))
            throw new HrValidationException("Primary classification must be ACT, W.O, or CORP.");
        if (!PortfolioClassification.IsValid(Primary, Sub))
            throw new HrValidationException($"Sub classification must be one of {string.Join(", ", PortfolioClassification.SubClassificationsFor(Primary))} for {PortfolioClassification.Display(Primary)}.");
    }
}
