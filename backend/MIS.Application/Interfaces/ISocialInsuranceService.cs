using MIS.Application.DTOs.Hr;
namespace MIS.Application.Interfaces;
public interface ISocialInsuranceService
{
    Task<SocialInsurancePageDto> ListAsync(string? search, Guid? departmentId, string? status, Guid? employeeId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyCollection<SocialInsuranceDto>> HistoryAsync(Guid employeeId, CancellationToken ct);
    Task<SocialInsuranceDto> SaveAsync(Guid? id, SaveSocialInsuranceRequest request, CancellationToken ct);
    Task<SocialInsuranceDto> EndAsync(Guid id, EndSocialInsuranceRequest request, CancellationToken ct);
}
