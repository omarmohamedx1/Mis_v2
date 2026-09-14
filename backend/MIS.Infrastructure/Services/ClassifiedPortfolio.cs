using Microsoft.EntityFrameworkCore;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

/// <summary>
/// Finds or creates the single portfolio that carries a classification pair for an organization.
/// Organizations are never duplicated — only the portfolio row is added.
/// </summary>
internal static class ClassifiedPortfolio
{
    public static async Task<CollectionPortfolio> ResolveAsync(ApplicationDbContext db, Guid organizationId,
        string primary, string sub, CancellationToken token)
    {
        var (normalizedPrimary, normalizedSub) = PortfolioClassification.Require(primary, sub);
        var existing = await db.CollectionPortfolios.Include(x => x.Organization)
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId
                && x.PrimaryClassification == normalizedPrimary && x.SubClassification == normalizedSub, token);
        if (existing is not null) return existing;

        var display = PortfolioClassification.Display(normalizedPrimary);
        var subDisplay = PortfolioClassification.Display(normalizedSub);
        var portfolio = new CollectionPortfolio(organizationId, PortfolioClassification.Code(normalizedPrimary, normalizedSub),
            $"محفظة {display} - {subDisplay}", $"{display} - {subDisplay}", "EGP", DateTimeOffset.UtcNow,
            normalizedPrimary, normalizedSub);
        db.CollectionPortfolios.Add(portfolio);
        await db.SaveChangesAsync(token);
        return await db.CollectionPortfolios.Include(x => x.Organization).SingleAsync(x => x.Id == portfolio.Id, token);
    }

    /// <summary>
    /// Falls back to the portfolio product (LOAN, VISA, AUTO) when the imported row carries no product type.
    /// CORP portfolios classify by segment rather than product, so they are left untouched.
    /// </summary>
    public static string? ProductTypeOrDefault(CollectionPortfolio portfolio, string? productType)
    {
        if (!string.IsNullOrWhiteSpace(productType)) return productType;
        if (portfolio.PrimaryClassification is not (CollectionsValues.PrimaryClassifications.Act or CollectionsValues.PrimaryClassifications.Wo)) return productType;
        return portfolio.SubClassification is { } product && CollectionsValues.SubClassifications.Products.Contains(product) ? product : productType;
    }
}
