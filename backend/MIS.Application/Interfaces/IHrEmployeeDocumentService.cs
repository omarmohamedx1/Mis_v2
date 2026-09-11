using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IHrEmployeeDocumentService
{
    Task<PagedEmployeeDocumentsDto> GetPagedAsync(EmployeeDocumentFilterDto filter, CancellationToken cancellationToken);
    Task<EmployeeDocumentDetailsDto> GetDetailsAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeeDocumentDetailsDto> CreateAsync(CreateEmployeeDocumentRequest request, HrUploadFile file, CancellationToken cancellationToken);
    Task<EmployeeDocumentDetailsDto> UpdateAsync(Guid id, UpdateEmployeeDocumentRequest request, CancellationToken cancellationToken);
    Task<EmployeeDocumentDetailsDto> ReplaceAsync(Guid id, HrUploadFile file, CancellationToken cancellationToken);
    Task<EmployeeDocumentFile> OpenAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, DeleteEmployeeDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentExpirySummaryDto> GetExpirySummaryAsync(CancellationToken cancellationToken);
    Task<PagedEmployeePersonnelFilesDto> GetPersonnelFilesAsync(PersonnelFileFilterDto filter, CancellationToken cancellationToken);
    Task<EmployeePersonnelFileDto> GetPersonnelFileAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<PersonnelFileSummaryDto> GetPersonnelFileSummaryAsync(CancellationToken cancellationToken);
    Task<EmployeeDocumentDetailsDto> UploadRequiredAsync(Guid employeeId, string documentCode, HrUploadFile file, CancellationToken cancellationToken);
}
