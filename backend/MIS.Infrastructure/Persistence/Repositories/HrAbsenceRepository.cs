using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Repositories;

public sealed class HrAbsenceRepository : IHrAbsenceRepository
{
    private readonly ApplicationDbContext _dbContext;
    public HrAbsenceRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<PagedAbsencesDto> GetPagedAsync(int page, int pageSize, string? search, Guid? departmentId, Guid? organizationId, Guid? employeeId, DateOnly? date, string? status, CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var query = _dbContext.EmployeeAbsences.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Employee.EmployeeNumber, pattern) ||
                EF.Functions.ILike(x.Employee.FullName, pattern) ||
                (x.Employee.FullNameArabic != null && EF.Functions.ILike(x.Employee.FullNameArabic, pattern)) ||
                (x.Employee.FullNameEnglish != null && EF.Functions.ILike(x.Employee.FullNameEnglish, pattern)));
        }
        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
        if (departmentId.HasValue) query = query.Where(x => x.Employee.DepartmentId == departmentId.Value);
        if (organizationId.HasValue)
            query = query.Where(x => _dbContext.EmployeeOrganizationAssignments.Any(assignment =>
                assignment.EmployeeId == x.EmployeeId && assignment.OrganizationId == organizationId.Value));
        if (date.HasValue) query = query.Where(x => x.AbsenceDate == date.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.AbsenceDate).ThenBy(x => x.Employee.EmployeeNumber).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AbsenceListItemDto(
                x.Id,
                x.EmployeeId,
                x.Employee.EmployeeNumber,
                isArabic ? x.Employee.FullNameArabic ?? x.Employee.FullName : x.Employee.FullNameEnglish ?? x.Employee.FullName,
                x.Employee.DepartmentId,
                isArabic ? x.Employee.Department.NameArabic ?? x.Employee.Department.Name : x.Employee.Department.Name,
                x.AbsenceDate,
                x.Type,
                x.Status,
                x.SuggestedDeductionAmount,
                x.ApprovedDeductionAmount,
                x.PayrollImpactStatus)).ToListAsync(cancellationToken);
        return new PagedAbsencesDto(items, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public Task<EmployeeAbsence?> GetTrackedAsync(Guid id, CancellationToken cancellationToken) => _dbContext.EmployeeAbsences.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<AbsenceDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        return _dbContext.EmployeeAbsences.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AbsenceDetailsDto(
                x.Id,
                x.EmployeeId,
                x.Employee.EmployeeNumber,
                isArabic ? x.Employee.FullNameArabic ?? x.Employee.FullName : x.Employee.FullNameEnglish ?? x.Employee.FullName,
                x.Employee.DepartmentId,
                isArabic ? x.Employee.Department.NameArabic ?? x.Employee.Department.Name : x.Employee.Department.Name,
                x.AbsenceDate,
                x.Type,
                x.Reason,
                x.Status,
                x.Notes,
                x.AttendanceSource,
                x.SuggestedDeductionAmount,
                x.ApprovedDeductionAmount,
                x.PayrollImpactStatus,
                x.PayrollNotes,
                x.PayrollReviewedByUser == null ? null : x.PayrollReviewedByUser.Username,
                x.PayrollReviewedAt,
                x.CreatedAt,
                x.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
    public Task<bool> EmployeeExistsAsync(Guid employeeId, CancellationToken cancellationToken) => _dbContext.Employees.AnyAsync(x => x.Id == employeeId, cancellationToken);
    public Task<bool> EmployeeEligibleOnDateAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        _dbContext.Employees.AnyAsync(x =>
            x.Id == employeeId &&
            (!x.HireDate.HasValue || x.HireDate.Value <= date) &&
            (!x.TerminationDate.HasValue || x.TerminationDate.Value >= date), cancellationToken);
    public Task<bool> AbsenceExistsAsync(Guid employeeId, DateOnly date, Guid? excludingId, CancellationToken cancellationToken) =>
        _dbContext.EmployeeAbsences.AnyAsync(x =>
            x.EmployeeId == employeeId && x.AbsenceDate == date && x.Id != excludingId, cancellationToken);
    public Task<bool> HasApprovedLeaveAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        _dbContext.LeaveRequests.AnyAsync(x =>
            x.EmployeeId == employeeId && x.Status == LeaveRequestStatuses.Approved &&
            x.StartDate <= date && x.EndDate >= date, cancellationToken);
    public Task<bool> HasConflictingAttendanceAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        _dbContext.AttendanceRecords.AnyAsync(x =>
            x.EmployeeId == employeeId && x.AttendanceDate == date && !x.IsDeleted &&
            (x.Status != AttendanceValues.AbsentStatus || x.CheckIn != null || x.CheckOut != null ||
             _dbContext.AttendancePunches.Any(punch => punch.AttendanceRecordId == x.Id)), cancellationToken);
    public Task<decimal?> GetBasicSalaryOnDateAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        _dbContext.EmployeeCompensations.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date))
            .OrderByDescending(x => x.EffectiveFrom)
            .Select(x => (decimal?)x.BasicSalary)
            .FirstOrDefaultAsync(cancellationToken);
    public void Add(EmployeeAbsence absence) => _dbContext.EmployeeAbsences.Add(absence);
    public void Remove(EmployeeAbsence absence) => _dbContext.EmployeeAbsences.Remove(absence);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
