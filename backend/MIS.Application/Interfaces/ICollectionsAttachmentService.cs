using MIS.Application.DTOs.Collections;

namespace MIS.Application.Interfaces;

public interface ICollectionsAttachmentService
{
    Task<IReadOnlyCollection<CollectionAttachmentDto>> GetCaseAttachmentsAsync(Guid caseId, CancellationToken cancellationToken);
    Task<CollectionAttachmentDto> UploadAsync(Guid caseId, Guid? paymentId, string category, string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken);
    Task<CollectionAttachmentDownloadDto> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid attachmentId, CancellationToken cancellationToken);
}
