using MIS.Application.DTOs.Finance;

namespace MIS.Application.Interfaces;

public interface IFinanceClaimsService
{
    Task<FinanceClaimPage> ListAsync(FinanceClaimFilter filter, CancellationToken token);
    Task<FinanceClaimDetails> DetailsAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<FinanceClaimOrganization>> OrganizationsAsync(CancellationToken token);
    Task<FinanceClaimListItem> CreateAsync(SaveFinanceClaimRequest request, CancellationToken token);
    Task<FinanceClaimListItem> UpdateAsync(Guid id, SaveFinanceClaimRequest request, CancellationToken token);
    Task<FinanceClaimDetails> ConfirmReceiptAsync(Guid id, ConfirmFinanceClaimReceiptRequest request, CancellationToken token);
    Task<FinanceClaimDetails> PostHistoricalReceiptAsync(Guid id, CancellationToken token);
    Task<FinanceClaimDetails> ReverseReceiptAsync(Guid id, ReverseFinanceClaimReceiptRequest request, CancellationToken token);
    Task<FinanceClaimDetails> CancelAsync(Guid id, string? reason, CancellationToken token);
    Task<FinanceClaimImportPreview> PreviewImportAsync(string fileName, Stream stream, CancellationToken token);
    Task<FinanceClaimImportResult> ConfirmImportAsync(FinanceClaimImportConfirmRequest request, CancellationToken token);
    Task<byte[]> ExportAsync(FinanceClaimFilter filter, CancellationToken token);
}
