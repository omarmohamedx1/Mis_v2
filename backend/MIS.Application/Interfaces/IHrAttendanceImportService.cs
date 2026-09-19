using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IHrAttendanceImportService
{
    Task<HrImportFileTemplate> BuildTemplateAsync(CancellationToken cancellationToken);
    Task<AttendanceImportUploadDto> UploadAsync(AttendanceImportFile file, CancellationToken cancellationToken);

    Task<AttendanceImportBatchDto> BuildPreviewAsync(Guid batchId, AttendanceImportColumnMappingRequest mapping, CancellationToken cancellationToken);

    Task<AttendanceImportBatchDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken);

    Task<PagedAttendanceImportPreviewDto> GetPreviewAsync(Guid batchId, AttendanceImportPreviewFilterDto filter, CancellationToken cancellationToken);

    Task<AttendanceImportConfirmResultDto> ConfirmAsync(Guid batchId, ConfirmAttendanceImportRequest request, CancellationToken cancellationToken);

    Task<AttendanceImportBatchDto> CancelAsync(Guid batchId, CancelAttendanceImportRequest request, CancellationToken cancellationToken);

    Task<PagedAttendanceImportHistoryDto> GetHistoryAsync(AttendanceImportHistoryFilterDto filter, CancellationToken cancellationToken);
}
