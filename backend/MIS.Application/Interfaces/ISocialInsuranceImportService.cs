using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface ISocialInsuranceImportService
{
    Task<HrImportFileTemplate> BuildTemplateAsync(CancellationToken cancellationToken);
    Task<SocialInsuranceImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken);
    Task<SocialInsuranceImportPreview> PreviewAsync(Guid id, SocialInsuranceImportMapping mapping, CancellationToken cancellationToken);
    Task<SocialInsuranceImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SocialInsuranceImportHistory>> HistoryAsync(CancellationToken cancellationToken);
}
