using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Repositories;

public sealed class HrEmployeeRepository : IHrEmployeeRepository
{
    private readonly ApplicationDbContext _dbContext;
    public HrEmployeeRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public Task<PagedEmployeesDto> GetPagedAsync(int page, int pageSize, string? search, Guid? departmentId, bool? isActive, CancellationToken cancellationToken) =>
        GetPagedCoreAsync(page, pageSize, search, departmentId, isActive, null, null, false, cancellationToken);

    public Task<PagedEmployeesDto> GetPagedByStatusAsync(int page, int pageSize, string? search, Guid? departmentId, string? status, string? operationalRole, bool? isArchived, CancellationToken cancellationToken) =>
        GetPagedCoreAsync(page, pageSize, search, departmentId, null, status, operationalRole, isArchived, cancellationToken);

    private async Task<PagedEmployeesDto> GetPagedCoreAsync(
        int page,
        int pageSize,
        string? search,
        Guid? departmentId,
        bool? isActive,
        string? status,
        string? operationalRole,
        bool? isArchived,
        CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var query = _dbContext.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(employee =>
                EF.Functions.ILike(employee.EmployeeNumber, $"%{term}%") ||
                EF.Functions.ILike(employee.FullName, $"%{term}%") ||
                (employee.FullNameArabic != null && EF.Functions.ILike(employee.FullNameArabic, $"%{term}%")) ||
                (employee.FullNameEnglish != null && EF.Functions.ILike(employee.FullNameEnglish, $"%{term}%")) ||
                (employee.NationalId != null && employee.NationalId == term) ||
                (employee.MobileNumber != null && EF.Functions.ILike(employee.MobileNumber, $"%{term}%")) ||
                (employee.OperationalRole != null && EF.Functions.ILike(employee.OperationalRole, $"%{term}%")) ||
                (employee.Position != null && (EF.Functions.ILike(employee.Position.Name, $"%{term}%") || (employee.Position.NameArabic != null && EF.Functions.ILike(employee.Position.NameArabic, $"%{term}%")))) ||
                _dbContext.Users.Any(user => user.EmployeeId == employee.Id &&
                    _dbContext.CollectionUserAccess.Any(access => access.UserId == user.Id &&
                        (EF.Functions.ILike(access.Organization.NameArabic, $"%{term}%") ||
                         EF.Functions.ILike(access.Organization.NameEnglish, $"%{term}%") ||
                         EF.Functions.ILike(access.Organization.Code, $"%{term}%")))));
        }
        if (departmentId.HasValue) query = query.Where(employee => employee.DepartmentId == departmentId.Value);
        if (isActive.HasValue) query = query.Where(employee => employee.IsActive == isActive.Value);
        else if (!string.IsNullOrWhiteSpace(status)) query = query.Where(employee => employee.Status == status);
        if (!string.IsNullOrWhiteSpace(operationalRole)) query = query.Where(employee => employee.OperationalRole == operationalRole);
        if (isArchived.HasValue) query = query.Where(employee => employee.IsArchived == isArchived.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageItems = await query.OrderBy(employee => employee.EmployeeNumber).ThenBy(employee => employee.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(employee => new
            {
                employee.Id,
                employee.EmployeeNumber,
                FullName = isArabic ? employee.FullNameArabic ?? employee.FullName : employee.FullNameEnglish ?? employee.FullName,
                employee.DepartmentId,
                DepartmentName = isArabic ? employee.Department.NameArabic ?? employee.Department.Name : employee.Department.Name,
                DepartmentCode = employee.Department.Code,
                employee.PositionId,
                PositionName = employee.Position == null ? null : isArabic ? employee.Position.NameArabic ?? employee.Position.Name : employee.Position.Name,
                employee.OperationalRole,
                employee.IsActive,
                employee.Status,
                employee.IsArchived
            })
            .ToListAsync(cancellationToken);

        var employeeIds = pageItems.Select(item => item.Id).ToArray();
        var organizationsByEmployee = await LoadOrganizationsByEmployeeAsync(employeeIds, cancellationToken);

        var items = pageItems.Select(employee => new EmployeeListItemDto(
                employee.Id,
                employee.EmployeeNumber,
                employee.FullName,
                employee.DepartmentId,
                employee.DepartmentName,
                employee.DepartmentCode,
                employee.PositionId,
                employee.PositionName,
                employee.OperationalRole,
                employee.IsActive,
                employee.Status,
                employee.IsArchived,
                organizationsByEmployee.GetValueOrDefault(employee.Id) ?? Array.Empty<EmployeeOrganizationAssignmentDto>()))
            .ToList();
        return new PagedEmployeesDto(items, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    private async Task<Dictionary<Guid, IReadOnlyList<EmployeeOrganizationAssignmentDto>>> LoadOrganizationsByEmployeeAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<EmployeeOrganizationAssignmentDto>>();

        var rows = await (
            from user in _dbContext.Users.AsNoTracking()
            where user.EmployeeId != null && employeeIds.Contains(user.EmployeeId.Value)
            join access in _dbContext.CollectionUserAccess.AsNoTracking() on user.Id equals access.UserId
            join organization in _dbContext.CollectionClientOrganizations.AsNoTracking() on access.OrganizationId equals organization.Id
            select new
            {
                EmployeeId = user.EmployeeId!.Value,
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<EmployeeOrganizationAssignmentDto>)group
                    .GroupBy(row => row.Id)
                    .Select(org => org.First())
                    .OrderBy(org => org.NameArabic)
                    .Select(org => new EmployeeOrganizationAssignmentDto(org.Id, org.Code, org.NameArabic, org.NameEnglish, org.OrganizationType))
                    .ToArray());
    }

    public Task<Employee?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public Task<EmployeeDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        return _dbContext.Employees.AsNoTracking().Where(employee => employee.Id == id)
            .Select(employee => new EmployeeDetailsDto(
                employee.Id,
                employee.EmployeeNumber,
                isArabic ? employee.FullNameArabic ?? employee.FullName : employee.FullNameEnglish ?? employee.FullName,
                employee.NationalId,
                employee.DepartmentId,
                isArabic ? employee.Department.NameArabic ?? employee.Department.Name : employee.Department.Name,
                employee.Department.Code,
                employee.PositionId,
                employee.Position == null ? null : isArabic ? employee.Position.NameArabic ?? employee.Position.Name : employee.Position.Name,
                employee.OperationalRole,
                employee.HireDate,
                employee.FingerprintEnrollmentDate,
                employee.DateOfBirth,
                employee.Address,
                employee.TerminationDate,
                employee.IsActive,
                employee.CreatedAt,
                employee.UpdatedAt,
                employee.Status,
                employee.IsArchived,
                employee.ArchivedAt,
                employee.ArchiveReason,
                employee.MobileNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DepartmentOptionDto>> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        return await _dbContext.Departments.AsNoTracking()
            .OrderBy(department => isArabic ? department.NameArabic ?? department.Name : department.Name)
            .Select(department => new DepartmentOptionDto(
                department.Id,
                isArabic ? department.NameArabic ?? department.Name : department.Name,
                department.Code))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> DepartmentExistsAsync(Guid departmentId, CancellationToken cancellationToken) =>
        _dbContext.Departments.AnyAsync(department => department.Id == departmentId, cancellationToken);

    public Task<bool> PositionExistsAsync(Guid positionId, CancellationToken cancellationToken) =>
        _dbContext.Positions.AnyAsync(position => position.Id == positionId && position.IsActive, cancellationToken);

    public Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = employeeNumber.Trim().ToLower();
        return _dbContext.Employees.AnyAsync(employee => employee.EmployeeNumber.ToLower() == normalized && (!excludingId.HasValue || employee.Id != excludingId.Value), cancellationToken);
    }

    public Task<bool> NationalIdExistsAsync(string nationalId, Guid? excludingId, CancellationToken cancellationToken) =>
        _dbContext.Employees.AnyAsync(employee => employee.NationalId == nationalId && (!excludingId.HasValue || employee.Id != excludingId.Value), cancellationToken);

    public void Add(Employee employee) => _dbContext.Employees.Add(employee);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
