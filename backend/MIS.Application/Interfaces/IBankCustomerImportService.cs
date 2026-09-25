using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IBankCustomerImportService
{
    Task<IReadOnlyCollection<PortfolioLookupDto>> GetPortfoliosAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<BankCustomerImportUpload> UploadAsync(Guid organizationId, HrUploadFile file, CancellationToken cancellationToken);
    Task<BankCustomerImportPreview> PreviewAsync(Guid organizationId, Guid id, BankCustomerImportMapping mapping, CancellationToken cancellationToken);
    Task<BankCustomerImportResult> ConfirmAsync(Guid organizationId, Guid id, Guid previewId, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null);
}
