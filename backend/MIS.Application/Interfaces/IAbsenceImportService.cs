using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IAbsenceImportService
{
    Task<HrImportFileTemplate> BuildTemplateAsync(CancellationToken cancellationToken);
    Task<AbsenceImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken);
    Task<AbsenceImportPreview> PreviewAsync(Guid id, AbsenceImportMapping mapping, CancellationToken cancellationToken);
    Task<AbsenceImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AbsenceImportHistory>> HistoryAsync(CancellationToken cancellationToken);
}
