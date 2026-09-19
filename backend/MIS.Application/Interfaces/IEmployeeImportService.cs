using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IEmployeeImportService
{
    Task<EmployeeImportTemplate> BuildTemplateAsync(CancellationToken cancellationToken);
    Task<EmployeeImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken);
    Task<EmployeeImportPreview> PreviewAsync(Guid id, EmployeeImportMapping mapping, CancellationToken cancellationToken);
    Task<EmployeeImportPreview> ReviseAsync(Guid id, ReviseEmployeeImportRequest request, CancellationToken cancellationToken);
    Task<EmployeeImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmployeeImportHistory>> HistoryAsync(CancellationToken cancellationToken);
    Task DeleteHistoryAsync(Guid id, CancellationToken cancellationToken);
}
