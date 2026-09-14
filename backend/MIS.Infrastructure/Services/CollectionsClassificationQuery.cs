using MIS.Application.Interfaces;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Services;

/// <summary>
/// Narrows collections queries to the portfolio classification carried by the current request.
/// An unscoped context leaves the query untouched so legacy and global pages keep working.
/// </summary>
public static class CollectionsClassificationQuery
{
    public static IQueryable<CollectionCase> Apply(this IQueryable<CollectionCase> query, ICollectionsClassificationContext classification) =>
        classification.HasValue
            ? query.Where(x => x.Portfolio.PrimaryClassification == classification.Primary && x.Portfolio.SubClassification == classification.Sub)
            : query;

    public static IQueryable<CollectionPortfolio> Apply(this IQueryable<CollectionPortfolio> query, ICollectionsClassificationContext classification) =>
        classification.HasValue
            ? query.Where(x => x.PrimaryClassification == classification.Primary && x.SubClassification == classification.Sub)
            : query;
}
