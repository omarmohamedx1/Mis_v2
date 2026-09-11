using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IEmployeeCreationService
{
    Task ValidateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeDetailsDto> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken);
}
