using MIS.Application.DTOs.Collections;

namespace MIS.Application.Interfaces;

public interface ICollectionsImportService
{
    Task<IReadOnlyCollection<PortfolioLookupDto>> GetPortfoliosAsync(Guid? organizationId, CancellationToken cancellationToken);
    Task<CollectionImportBatchDto> UploadAsync(Guid organizationId, Guid portfolioId, string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken);
    Task<PagedResultDto<CollectionImportBatchDto>> GetBatchesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<CollectionImportPreviewDto> GetPreviewAsync(Guid batchId, int page, int pageSize, bool? valid, CancellationToken cancellationToken);
    Task<CollectionImportBatchDto> ConfirmAsync(Guid batchId, ConfirmCollectionImportRequest request, CancellationToken cancellationToken);
    Task<byte[]> ExportErrorsAsync(Guid batchId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid batchId, CancellationToken cancellationToken);
}
