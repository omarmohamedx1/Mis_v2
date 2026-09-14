using MIS.Application.DTOs.Collections;

namespace MIS.Application.Interfaces;

public interface IBankCustomerService
{
    Task<BankCustomerPageDto> GetAsync(Guid organizationId, BankCustomerQuery query, CancellationToken cancellationToken);
    Task<BankCustomerDetailsDto> GetDetailsAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken);
}
