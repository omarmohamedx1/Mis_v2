using MIS.Application.Common;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence.Configurations;

namespace MIS.Infrastructure.Services;

// Existing, explicitly approved accounts only. Never create accounts during receipt posting.
public sealed class ClaimReceiptOptions
{
    public Guid AssetAccountId { get; set; }
    public Guid ClearingAccountId { get; set; }

    internal static void Validate(IReadOnlyCollection<FinanceAccount> accounts, ClaimReceiptOptions options)
    {
        var asset = accounts.SingleOrDefault(x => x.Id == options.AssetAccountId);
        var clearing = accounts.SingleOrDefault(x => x.Id == options.ClearingAccountId);
        if (asset is null || clearing is null || asset.Id == clearing.Id
            || asset.AccountType != "ASSET" || clearing.AccountType != "LIABILITY"
            || accounts.Any(x => x.LegalEntityId != FinanceSeed.LegalEntityId || !x.IsActive || !x.PostingAllowed
                || x.IsCashAccount || FinanceSeed.OperationAccountIds.Contains(x.Id)
                || x.ControlAccountType != null || x.RequiresClient || x.RequiresCollector || x.RequiresBranch))
            throw new HrValidationException("Configure dedicated non-operating claim receipt asset and clearing accounts before confirming a receipt.");
    }
}
