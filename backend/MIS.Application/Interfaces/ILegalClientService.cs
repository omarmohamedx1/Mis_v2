using MIS.Application.DTOs.Legal;

namespace MIS.Application.Interfaces;

public interface ILegalClientService
{
    Task<LegalClientDashboardDto> GetDashboardAsync(CancellationToken token);
    Task<LegalClientPageDto> ListAsync(string? search, int page, int pageSize, CancellationToken token);
    Task<LegalClientDetailsDto> GetAsync(Guid customerId, CancellationToken token);
    Task<LegalClientDetailsDto> UpdateAsync(Guid customerId, UpdateLegalClientRequest request, CancellationToken token);
    Task<LegalClientWorkbookDto> InspectImportAsync(string fileName, long length, Stream content, CancellationToken token);
    Task<LegalClientImportPreviewDto> PreviewImportAsync(string fileName, long length, Stream content, Guid organizationId, CancellationToken token, LegalClientColumnMapping? mapping = null);
    Task<LegalClientImportResultDto> ImportAsync(LegalClientImportRequest request, CancellationToken token);
    Task<LegalClientDocumentDto> UploadDocumentAsync(Guid customerId, string fileName, string contentType, long length, Stream content, CancellationToken token);
    Task<LegalClientDocumentDownloadDto> DownloadDocumentAsync(Guid documentId, CancellationToken token);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken token);
}

public sealed record LegalClientDocumentDownloadDto(Stream Content, string ContentType, string FileName);
