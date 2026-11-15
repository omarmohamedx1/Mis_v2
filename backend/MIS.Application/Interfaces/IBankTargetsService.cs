using MIS.Application.DTOs.Collections;

namespace MIS.Application.Interfaces;

public interface IBankTargetsService
{
    Task<BankTargetsDto> GetTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId, string period, CancellationToken token);
    Task<BankTargetsDto> SaveTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId, string period, SaveBankTargetsRequest request, CancellationToken token);
    Task<BankTargetsDto> CopyTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId, string period, CancellationToken token);
    Task<BankTargetsDto> ReviewTargetsAsync(Guid organizationId, string portfolioType, Guid portfolioId, string period, BankTargetVersionRequest request, bool approve, CancellationToken token);
}
