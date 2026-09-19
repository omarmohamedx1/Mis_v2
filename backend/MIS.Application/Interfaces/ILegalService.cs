using MIS.Application.DTOs.Legal;

namespace MIS.Application.Interfaces;

public interface ILegalService
{
    Task<LegalDashboardDto> GetDashboardAsync(CancellationToken token);
    Task<IReadOnlyList<LegalOrganizationDto>> ListOrganizationsAsync(CancellationToken token);
    Task<LegalCasePageDto> ListCasesAsync(string? search, Guid? organizationId, string? stage, int page, int pageSize, CancellationToken token);
    Task<LegalCaseDetailsDto> GetCaseAsync(Guid collectionCaseId, CancellationToken token);
    Task<LegalCaseDetailsDto> SaveFileAsync(Guid collectionCaseId, SaveLegalFileRequest request, CancellationToken token);
    Task<LegalCaseDetailsDto> RecordActionAsync(Guid collectionCaseId, RecordLegalActionRequest request, CancellationToken token);
}
