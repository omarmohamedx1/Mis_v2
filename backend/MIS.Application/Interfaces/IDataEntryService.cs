using MIS.Application.DTOs.DataEntry;
using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IDataEntryService
{
    Task<DataEntryDashboardDto> GetDashboardAsync(CancellationToken token);
    Task<IReadOnlyList<DataEntryOrganizationDto>> ListOrganizationsAsync(CancellationToken token);
    Task<IReadOnlyList<DataEntryPortfolioDto>> ListPortfoliosAsync(Guid organizationId, CancellationToken token);

    Task<DataEntryClientPageDto> ListClientsAsync(string? search, string? column, string? value, string? presence, int page, int pageSize, CancellationToken token);
    Task<DataEntryClientDetailsDto> GetClientAsync(Guid customerId, CancellationToken token);
    Task<DataEntryClientDetailsDto> CreateManualClientAsync(CreateDataEntryClientRequest request, CancellationToken token);
    Task<DataEntryClientDetailsDto> UpdateClientAsync(Guid customerId, UpdateDataEntryClientRequest request, CancellationToken token);
    Task AddColumnAsync(string name, CancellationToken token);
    Task DeleteColumnAsync(string name, CancellationToken token);
    Task DeleteClientAsync(Guid customerId, CancellationToken token);
    Task<DeleteDataEntryClientsResult> DeleteAllClientsAsync(CancellationToken token);

    Task<DataEntryImportUploadDto> UploadImportAsync(HrUploadFile file, CancellationToken token);
    Task<DataEntrySheetPreviewDto> ReadSheetAsync(Guid uploadId, string? sheetName, CancellationToken token);
    Task<DataEntryImportPreviewDto> PreviewImportAsync(Guid uploadId, DataEntryImportMappingRequest mapping, CancellationToken token);
    Task<DataEntryBatchListItemDto> ConfirmImportAsync(ConfirmDataEntryImportRequest request, CancellationToken token);

    Task<DataEntryBatchPageDto> ListMyBatchesAsync(string? status, int page, int pageSize, CancellationToken token);
    Task<DataEntryBatchPageDto> ListSupervisorBatchesAsync(string? status, int page, int pageSize, CancellationToken token);
    Task<DataEntryBatchDetailsDto> GetBatchAsync(Guid batchId, CancellationToken token);
    Task<DataEntryBatchListItemDto> AcceptBatchAsync(Guid batchId, CancellationToken token);
    Task<DataEntryBatchListItemDto> RejectBatchAsync(Guid batchId, RejectDataEntryBatchRequest request, CancellationToken token);
    Task<DataEntryBatchListItemDto> SendToDistributionAsync(Guid batchId, CancellationToken token);

    Task<IReadOnlyList<DataEntryNotificationDto>> ListNotificationsAsync(CancellationToken token);
    Task MarkNotificationReadAsync(Guid notificationId, CancellationToken token);

    Task<IReadOnlyList<DataEntryDocumentDto>> ListClientDocumentsAsync(Guid customerId, CancellationToken token);
    Task<IReadOnlyList<DataEntryDocumentDto>> ListBatchDocumentsAsync(Guid batchId, CancellationToken token);
    Task<IReadOnlyList<DataEntryDocumentDto>> ListCaseDocumentsAsync(Guid caseId, CancellationToken token);
    Task<DataEntryDocumentDto> UploadClientDocumentAsync(Guid customerId, HrUploadFile file, string? note, CancellationToken token);
    Task<DataEntryDocumentDownloadDto> DownloadDocumentAsync(Guid documentId, CancellationToken token);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken token);
}
