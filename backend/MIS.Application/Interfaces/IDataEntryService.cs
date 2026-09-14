using MIS.Application.DTOs.DataEntry;
using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IDataEntryService
{
    Task<DataEntryDashboardDto> GetDashboardAsync(CancellationToken token);
    Task<IReadOnlyList<DataEntryOrganizationDto>> ListOrganizationsAsync(CancellationToken token);
    Task<IReadOnlyList<DataEntryPortfolioDto>> ListPortfoliosAsync(Guid organizationId, CancellationToken token);

    Task<DataEntryClientPageDto> ListClientsAsync(string? search, int page, int pageSize, CancellationToken token);
    Task<DataEntryClientDetailsDto> GetClientAsync(Guid customerId, CancellationToken token);
    Task<DataEntryClientDetailsDto> CreateManualClientAsync(CreateDataEntryClientRequest request, CancellationToken token);

    Task<DataEntryImportUploadDto> UploadImportAsync(HrUploadFile file, CancellationToken token);
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
}
