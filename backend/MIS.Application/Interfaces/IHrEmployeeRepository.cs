using MIS.Application.DTOs.Hr;
using MIS.Domain.Entities;

namespace MIS.Application.Interfaces;

public interface IHrEmployeeRepository
{
    Task<PagedEmployeesDto> GetPagedAsync(int page, int pageSize, string? search, Guid? departmentId, bool? isActive, CancellationToken cancellationToken);
    Task<PagedEmployeesDto> GetPagedByStatusAsync(int page, int pageSize, string? search, string? searchField, Guid? departmentId, string? status, string? operationalRole, bool? isArchived, Guid? organizationId, string? gender, Guid? positionId, CancellationToken cancellationToken);
    Task<Employee?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeeDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DepartmentOptionDto>> GetDepartmentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmployeeOrganizationAssignmentDto>> GetOrganizationsAsync(CancellationToken cancellationToken);
    Task<bool> OrganizationsExistAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken);
    Task ReplaceOrganizationAssignmentsAsync(Guid employeeId, IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken);
    Task<bool> DepartmentExistsAsync(Guid departmentId, CancellationToken cancellationToken);
    Task<bool> PositionExistsAsync(Guid positionId, CancellationToken cancellationToken);
    Task<PositionLookupDto?> GetPositionAsync(Guid positionId, CancellationToken cancellationToken);
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> NationalIdExistsAsync(string nationalId, Guid? excludingId, CancellationToken cancellationToken);
    void Add(Employee employee);
    Task UpsertCurrentSalaryAsync(Guid employeeId, decimal basicSalary, decimal allowances, DateOnly effectiveFrom, CancellationToken cancellationToken);
    Task DeleteUnusedAsync(Guid id, CancellationToken cancellationToken);
    Task DeletePermanentlyAsync(Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
