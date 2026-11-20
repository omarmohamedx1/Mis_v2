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
        if (options.AssetAccountId == Guid.Empty || options.ClearingAccountId == Guid.Empty)
            throw new HrValidationException("Finance:ClaimReceipts:AssetAccountId and Finance:ClaimReceipts:ClearingAccountId must reference approved dedicated accounts. Ask Finance to configure them.");
        var asset = accounts.SingleOrDefault(x => x.Id == options.AssetAccountId);
        var clearing = accounts.SingleOrDefault(x => x.Id == options.ClearingAccountId);
        if (asset is null || clearing is null)
            throw new HrValidationException("A configured claim receipt account does not exist. Ask Finance to verify both account IDs.");
        if (!asset.IsActive || !clearing.IsActive || !asset.PostingAllowed || !clearing.PostingAllowed)
            throw new HrValidationException("Claim receipt accounts must be active and allow posting. Ask Finance to review the configured accounts.");
        if (asset.Id == clearing.Id
            || asset.AccountType != "ASSET" || clearing.AccountType != "LIABILITY"
            || accounts.Any(x => x.LegalEntityId != FinanceSeed.LegalEntityId || !x.IsActive || !x.PostingAllowed
                || x.IsCashAccount || FinanceSeed.OperationAccountIds.Contains(x.Id)
                || x.ControlAccountType != null || x.RequiresClient || x.RequiresCollector || x.RequiresBranch))
            throw new HrValidationException("Configure dedicated non-operating claim receipt asset and clearing accounts before confirming a receipt.");
    }
}
