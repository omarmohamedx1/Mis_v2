using MIS.Application.DTOs.Hr;
namespace MIS.Application.Interfaces;
public interface IHrExcuseMissionService
{
    IReadOnlyCollection<ExcuseTypeOption> Types();
    Task<ExcusePage> ListAsync(ExcuseFilter filter, CancellationToken ct);
    Task<ExcuseItem> OriginalVisitAsync(Guid visitId, CancellationToken ct);
    Task<ExcuseItem> DetailsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyCollection<ExcuseItem>> NotificationsAsync(CancellationToken ct);
    Task DecideAsync(Guid id, MissionDecisionRequest request, CancellationToken ct);
    Task<Guid> SaveManualAsync(Guid? id, ManualMissionRequest request, CancellationToken ct);
    Task CancelAsync(Guid id, CancelMissionRequest request, CancellationToken ct);
    Task LinkEmployeeAsync(Guid collectorId, Guid employeeId, CancellationToken ct);
    Task UploadAsync(Guid id, Guid? attachmentId, HrUploadFile file, CancellationToken ct);
    Task<(Stream Content, string ContentType, string FileName)> DownloadAsync(Guid id, Guid attachmentId, CancellationToken ct);
    Task DeleteAttachmentAsync(Guid id, Guid attachmentId, CancellationToken ct);
}
