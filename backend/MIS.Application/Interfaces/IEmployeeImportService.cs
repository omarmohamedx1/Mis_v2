using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IEmployeeImportService
{
    Task<EmployeeImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken);
    Task<EmployeeImportPreview> PreviewAsync(Guid id, EmployeeImportMapping mapping, CancellationToken cancellationToken);
    Task<EmployeeImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmployeeImportHistory>> HistoryAsync(CancellationToken cancellationToken);
}
