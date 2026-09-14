using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IEmployeeDocumentBulkService
{
    Task<EmployeeDocumentBulkUpload> UploadAsync(IReadOnlyCollection<HrUploadFile> files, CancellationToken cancellationToken);
    Task<EmployeeDocumentBulkUpload> ApplyCorrectionsAsync(Guid id, IReadOnlyCollection<EmployeeDocumentBulkCorrection> corrections, CancellationToken cancellationToken);
    Task<EmployeeDocumentBulkResult> ConfirmAsync(Guid id, IReadOnlyCollection<EmployeeDocumentBulkCorrection> corrections, CancellationToken cancellationToken);
}
