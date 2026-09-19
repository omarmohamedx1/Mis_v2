using MIS.Application.DTOs.Hr;
using MIS.Domain.Entities;

namespace MIS.Application.Interfaces;

public interface IHrAbsenceRepository
{
    Task<PagedAbsencesDto> GetPagedAsync(int page, int pageSize, string? search, Guid? departmentId, Guid? organizationId, Guid? employeeId, DateOnly? date, string? status, CancellationToken cancellationToken);
    Task<EmployeeAbsence?> GetTrackedAsync(Guid id, CancellationToken cancellationToken);
    Task<AbsenceDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmployeeExistsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<bool> EmployeeEligibleOnDateAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);
    Task<bool> AbsenceExistsAsync(Guid employeeId, DateOnly date, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> HasApprovedLeaveAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);
    Task<bool> HasConflictingAttendanceAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);
    Task<decimal?> GetBasicSalaryOnDateAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);
    void Add(EmployeeAbsence absence);
    void Remove(EmployeeAbsence absence);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
