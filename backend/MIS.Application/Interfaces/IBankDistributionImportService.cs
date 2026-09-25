using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IBankDistributionImportService
{
    Task<IReadOnlyCollection<DistributionCollectorDto>> GetCollectorsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<BankDistributionImportUpload> UploadAsync(Guid organizationId, HrUploadFile file, CancellationToken cancellationToken);
    Task<BankDistributionImportPreview> PreviewAsync(Guid organizationId, Guid id, BankDistributionImportMapping mapping, CancellationToken cancellationToken);
    Task<BankDistributionImportResult> ConfirmAsync(Guid organizationId, Guid id, Guid previewId, bool reassignExisting, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null);
}
