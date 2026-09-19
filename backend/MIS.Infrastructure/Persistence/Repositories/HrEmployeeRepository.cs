using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;

namespace MIS.Infrastructure.Persistence.Repositories;

public sealed class HrEmployeeRepository : IHrEmployeeRepository
{
    private static readonly string[] MaleNationalIdDigits = ["1", "3", "5", "7", "9"];
    private static readonly string[] FemaleNationalIdDigits = ["0", "2", "4", "6", "8"];

    private readonly ApplicationDbContext _dbContext;
    public HrEmployeeRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public Task<PagedEmployeesDto> GetPagedAsync(int page, int pageSize, string? search, Guid? departmentId, bool? isActive, CancellationToken cancellationToken) =>
        GetPagedCoreAsync(page, pageSize, search, "identity", departmentId, isActive, null, null, false, null, null, null, cancellationToken);

    public Task<PagedEmployeesDto> GetPagedByStatusAsync(int page, int pageSize, string? search, string? searchField, Guid? departmentId, string? status, string? operationalRole, bool? isArchived, Guid? organizationId, string? gender, Guid? positionId, CancellationToken cancellationToken) =>
        GetPagedCoreAsync(page, pageSize, search, searchField, departmentId, null, status, operationalRole, isArchived, organizationId, gender, positionId, cancellationToken);

    private async Task<PagedEmployeesDto> GetPagedCoreAsync(
        int page,
        int pageSize,
        string? search,
        string? searchField,
        Guid? departmentId,
        bool? isActive,
        string? status,
        string? operationalRole,
        bool? isArchived,
        Guid? organizationId,
        string? gender,
        Guid? positionId,
        CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var query = _dbContext.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var field = searchField?.Trim().ToLowerInvariant();
            query = field switch
            {
                "employeenumber" => query.Where(employee => EF.Functions.ILike(employee.EmployeeNumber, $"%{term}%")),
                "name" => query.Where(employee =>
                    EF.Functions.ILike(employee.FullName, $"%{term}%") ||
                    (employee.FullNameArabic != null && EF.Functions.ILike(employee.FullNameArabic, $"%{term}%")) ||
                    (employee.FullNameEnglish != null && EF.Functions.ILike(employee.FullNameEnglish, $"%{term}%"))),
                "nationalid" => query.Where(employee => employee.NationalId != null && EF.Functions.ILike(employee.NationalId, $"%{term}%")),
                "mobile" => query.Where(employee => employee.MobileNumber != null && EF.Functions.ILike(employee.MobileNumber, $"%{term}%")),
                "organization" => FilterByOrganizationSearch(query, term),
                "position" => query.Where(employee => employee.Position != null &&
                    (EF.Functions.ILike(employee.Position.Name, $"%{term}%") ||
                     (employee.Position.NameArabic != null && EF.Functions.ILike(employee.Position.NameArabic, $"%{term}%")))),
                _ => query.Where(employee =>
                EF.Functions.ILike(employee.EmployeeNumber, $"%{term}%") ||
                EF.Functions.ILike(employee.FullName, $"%{term}%") ||
                (employee.FullNameArabic != null && EF.Functions.ILike(employee.FullNameArabic, $"%{term}%")) ||
                (employee.FullNameEnglish != null && EF.Functions.ILike(employee.FullNameEnglish, $"%{term}%")))
            };
        }
        if (departmentId.HasValue)
        {
            var directoryDepartmentIds = await ResolveDirectoryDepartmentIdsAsync(departmentId.Value, cancellationToken);
            query = query.Where(employee => directoryDepartmentIds.Contains(employee.DepartmentId));
        }
        if (organizationId.HasValue)
        {
            var organization = await _dbContext.CollectionClientOrganizations.AsNoTracking()
                .Where(item => item.Id == organizationId.Value)
                .Select(item => new { item.Id, item.Code, item.NameEnglish, item.NameArabic })
                .FirstOrDefaultAsync(cancellationToken);
            if (organization is null)
            {
                query = query.Where(employee => false);
            }
            else
            {
                var linkedDepartmentIds = await ResolveOrganizationDepartmentIdsAsync(organization.Id, cancellationToken);
                var linkedPositionIds = await ResolveOrganizationPositionIdsAsync(
                    organization.Code, organization.NameEnglish, organization.NameArabic, cancellationToken);
                query = FilterByOrganization(
                    query,
                    organization.Id,
                    organization.Code,
                    linkedDepartmentIds,
                    linkedPositionIds);
            }
        }
        if (positionId.HasValue) query = query.Where(employee => employee.PositionId == positionId.Value);
        if (!string.IsNullOrWhiteSpace(gender))
            query = FilterByGender(query, gender);
        if (isActive.HasValue) query = query.Where(employee => employee.IsActive == isActive.Value);
        else if (string.Equals(status, Employee.ActiveStatus, StringComparison.Ordinal))
            query = query.Where(employee => employee.Status == Employee.ActiveStatus || (employee.IsActive && employee.Status != Employee.InactiveStatus && employee.Status != Employee.TerminatedStatus && employee.Status != Employee.SuspendedStatus));
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
                employee.FullName,
                employee.FullNameArabic,
                employee.FullNameEnglish,
                employee.DepartmentId,
                DepartmentName = isArabic ? employee.Department.NameArabic ?? employee.Department.Name : employee.Department.Name,
                DepartmentCode = employee.Department.Code,
                employee.PositionId,
                PositionName = employee.Position == null ? null : isArabic ? employee.Position.NameArabic ?? employee.Position.Name : employee.Position.Name,
                employee.OperationalRole,
                employee.IsActive,
                employee.Status,
                employee.IsArchived,
                employee.NationalId,
                employee.MobileNumber,
                WorkStartDate = employee.HireDate
            })
            .ToListAsync(cancellationToken);

        var employeeIds = pageItems.Select(item => item.Id).ToArray();
        var organizationsByEmployee = await LoadOrganizationsByEmployeeAsync(employeeIds, cancellationToken);

        var items = pageItems.Select(employee =>
            {
                var names = EmployeeName.FillMissing(employee.FullName, employee.FullNameArabic, employee.FullNameEnglish);
                return new EmployeeListItemDto(
                employee.Id,
                employee.EmployeeNumber,
                EmployeeName.Display(isArabic, employee.FullName, names.Arabic, names.English),
                employee.DepartmentId,
                employee.DepartmentName,
                employee.DepartmentCode,
                employee.PositionId,
                employee.PositionName,
                employee.OperationalRole,
                employee.IsActive,
                employee.Status,
                employee.IsArchived,
                organizationsByEmployee.GetValueOrDefault(employee.Id) ?? Array.Empty<EmployeeOrganizationAssignmentDto>(),
                employee.NationalId,
                employee.MobileNumber,
                employee.WorkStartDate,
                names.Arabic,
                names.English);
            })
            .ToList();
        return new PagedEmployeesDto(items, totalCount, page, pageSize, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    private async Task<Guid[]> ResolveDirectoryDepartmentIdsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var departments = await _dbContext.Departments.AsNoTracking()
            .Select(department => new { department.Id, department.Code, department.Name, department.NameArabic })
            .ToListAsync(cancellationToken);
        var organizations = await _dbContext.CollectionClientOrganizations.AsNoTracking()
            .Select(organization => new { organization.Code, organization.NameEnglish, organization.NameArabic })
            .ToListAsync(cancellationToken);
        return EmployeeDirectoryDepartmentFilter.Expand(
            departmentId,
            departments.Select(department => (department.Id, department.Code, department.Name, department.NameArabic)).ToList(),
            organizations.Select(organization => (organization.Code, organization.NameEnglish, organization.NameArabic)).ToList())
            .ToArray();
    }

    private IQueryable<Employee> FilterByGender(IQueryable<Employee> query, string gender)
    {
        var stored = gender.Trim();
        var digits = string.Equals(stored, "Female", StringComparison.OrdinalIgnoreCase)
            ? FemaleNationalIdDigits
            : MaleNationalIdDigits;
        return query.Where(employee =>
            (employee.Gender != null && employee.Gender.ToLower() == stored.ToLower())
            || ((employee.Gender == null || employee.Gender == "")
                && employee.NationalId != null
                && employee.NationalId.Length >= 13
                && digits.Contains(employee.NationalId.Substring(12, 1))));
    }

    private IQueryable<Employee> FilterByOrganization(
        IQueryable<Employee> query,
        Guid organizationId,
        string organizationCode,
        IReadOnlyCollection<Guid> linkedDepartmentIds,
        IReadOnlyCollection<Guid> linkedPositionIds)
    {
        var code = organizationCode.ToUpper();
        return query.Where(employee =>
            linkedDepartmentIds.Contains(employee.DepartmentId)
            || (employee.PositionId.HasValue && linkedPositionIds.Contains(employee.PositionId.Value))
            || _dbContext.EmployeeOrganizationAssignments.Any(assignment =>
                assignment.EmployeeId == employee.Id
                && (assignment.OrganizationId == organizationId || assignment.Organization.Code.ToUpper() == code))
            || _dbContext.Users.Any(user =>
                user.EmployeeId == employee.Id
                && (_dbContext.CollectionUserAccess.Any(access =>
                        access.UserId == user.Id
                        && (access.OrganizationId == organizationId || access.Organization.Code.ToUpper() == code))
                    || _dbContext.UserAccessGrants.Any(grant =>
                        grant.UserId == user.Id
                        && grant.ClientOrganizationId != null
                        && grant.Status == "ACTIVE"
                        && (grant.ClientOrganizationId == organizationId
                            || _dbContext.CollectionClientOrganizations.Any(organization =>
                                organization.Id == grant.ClientOrganizationId && organization.Code.ToUpper() == code)))
                    || _dbContext.CollectionTeamMembers.Any(member =>
                        member.UserId == user.Id
                        && member.IsActive
                        && _dbContext.CollectionCases.Any(collectionCase =>
                            !collectionCase.IsArchived
                            && collectionCase.AssignedTeamId == member.TeamId
                            && (collectionCase.Portfolio.OrganizationId == organizationId
                                || collectionCase.Portfolio.Organization.Code.ToUpper() == code)))
                    || _dbContext.CollectionTeams.Any(team =>
                        team.SupervisorId == user.Id
                        && _dbContext.CollectionCases.Any(collectionCase =>
                            !collectionCase.IsArchived
                            && collectionCase.AssignedTeamId == team.Id
                            && (collectionCase.Portfolio.OrganizationId == organizationId
                                || collectionCase.Portfolio.Organization.Code.ToUpper() == code)))))
            || _dbContext.CollectionCases.Any(collectionCase =>
                !collectionCase.IsArchived
                && collectionCase.AssignedCollectorId != null
                && (collectionCase.Portfolio.OrganizationId == organizationId
                    || collectionCase.Portfolio.Organization.Code.ToUpper() == code)
                && _dbContext.Users.Any(user =>
                    user.Id == collectionCase.AssignedCollectorId && user.EmployeeId == employee.Id)));
    }

    private IQueryable<Employee> FilterByOrganizationSearch(IQueryable<Employee> query, string term)
    {
        var pattern = $"%{term}%";
        return query.Where(employee =>
            EF.Functions.ILike(employee.Department.Code, pattern)
            || EF.Functions.ILike(employee.Department.Name, pattern)
            || (employee.Department.NameArabic != null && EF.Functions.ILike(employee.Department.NameArabic, pattern))
            || (employee.Position != null && (
                EF.Functions.ILike(employee.Position.Code, pattern)
                || EF.Functions.ILike(employee.Position.Name, pattern)
                || (employee.Position.NameArabic != null && EF.Functions.ILike(employee.Position.NameArabic, pattern))))
            || _dbContext.EmployeeOrganizationAssignments.Any(assignment =>
                assignment.EmployeeId == employee.Id
                && (EF.Functions.ILike(assignment.Organization.Code, pattern)
                    || EF.Functions.ILike(assignment.Organization.NameArabic, pattern)
                    || EF.Functions.ILike(assignment.Organization.NameEnglish, pattern)))
            || _dbContext.Users.Any(user =>
                user.EmployeeId == employee.Id
                && _dbContext.CollectionUserAccess.Any(access =>
                    access.UserId == user.Id
                    && (EF.Functions.ILike(access.Organization.Code, pattern)
                        || EF.Functions.ILike(access.Organization.NameArabic, pattern)
                        || EF.Functions.ILike(access.Organization.NameEnglish, pattern))))
            || _dbContext.CollectionCases.Any(collectionCase =>
                !collectionCase.IsArchived
                && collectionCase.AssignedCollectorId != null
                && collectionCase.Portfolio.OrganizationId != Guid.Empty
                && _dbContext.Users.Any(user =>
                    user.Id == collectionCase.AssignedCollectorId && user.EmployeeId == employee.Id)
                && (EF.Functions.ILike(collectionCase.Portfolio.Organization.Code, pattern)
                    || EF.Functions.ILike(collectionCase.Portfolio.Organization.NameArabic, pattern)
                    || EF.Functions.ILike(collectionCase.Portfolio.Organization.NameEnglish, pattern))));
    }

    private async Task<Guid[]> ResolveOrganizationDepartmentIdsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await _dbContext.CollectionClientOrganizations.AsNoTracking()
            .Where(item => item.Id == organizationId)
            .Select(item => new { item.Code, item.NameEnglish, item.NameArabic })
            .FirstOrDefaultAsync(cancellationToken);
        if (organization is null) return [];

        var departments = await _dbContext.Departments.AsNoTracking()
            .Select(department => new { department.Id, department.Code, department.Name, department.NameArabic })
            .ToListAsync(cancellationToken);

        return departments
            .Where(department =>
                HrOrganizationLookup.Mentions(department.Code, organization.Code, organization.NameEnglish, organization.NameArabic)
                || HrOrganizationLookup.Mentions(department.Name, organization.Code, organization.NameEnglish, organization.NameArabic)
                || HrOrganizationLookup.Mentions(department.NameArabic, organization.Code, organization.NameEnglish, organization.NameArabic))
            .Select(department => department.Id)
            .ToArray();
    }

    private async Task<Guid[]> ResolveOrganizationPositionIdsAsync(
        string code,
        string? nameEnglish,
        string? nameArabic,
        CancellationToken cancellationToken)
    {
        var positions = await _dbContext.Positions.AsNoTracking()
            .Select(position => new { position.Id, position.Code, position.Name, position.NameArabic })
            .ToListAsync(cancellationToken);

        return positions
            .Where(position =>
                HrOrganizationLookup.Mentions(position.Code, code, nameEnglish, nameArabic)
                || HrOrganizationLookup.Mentions(position.Name, code, nameEnglish, nameArabic)
                || HrOrganizationLookup.Mentions(position.NameArabic, code, nameEnglish, nameArabic))
            .Select(position => position.Id)
            .ToArray();
    }

    private async Task<Dictionary<Guid, IReadOnlyList<EmployeeOrganizationAssignmentDto>>> LoadOrganizationsByEmployeeAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<EmployeeOrganizationAssignmentDto>>();

        var assignmentRows = await (
            from assignment in _dbContext.EmployeeOrganizationAssignments.AsNoTracking()
            where employeeIds.Contains(assignment.EmployeeId)
            join organization in _dbContext.CollectionClientOrganizations.AsNoTracking() on assignment.OrganizationId equals organization.Id
            select new OrganizationLinkRow(
                assignment.EmployeeId,
                assignment.IsPrimary,
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType)).ToListAsync(cancellationToken);

        var accessRows = await (
            from user in _dbContext.Users.AsNoTracking()
            where user.EmployeeId != null && employeeIds.Contains(user.EmployeeId.Value)
            join access in _dbContext.CollectionUserAccess.AsNoTracking() on user.Id equals access.UserId
            join organization in _dbContext.CollectionClientOrganizations.AsNoTracking() on access.OrganizationId equals organization.Id
            select new OrganizationLinkRow(
                user.EmployeeId!.Value,
                false,
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType)).ToListAsync(cancellationToken);

        var grantRows = await (
            from user in _dbContext.Users.AsNoTracking()
            where user.EmployeeId != null && employeeIds.Contains(user.EmployeeId.Value)
            join grant in _dbContext.UserAccessGrants.AsNoTracking() on user.Id equals grant.UserId
            where grant.ClientOrganizationId != null && grant.Status == "ACTIVE"
            join organization in _dbContext.CollectionClientOrganizations.AsNoTracking() on grant.ClientOrganizationId equals organization.Id
            select new OrganizationLinkRow(
                user.EmployeeId!.Value,
                false,
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType)).ToListAsync(cancellationToken);

        var caseRows = await (
            from collectionCase in _dbContext.CollectionCases.AsNoTracking()
            where !collectionCase.IsArchived && collectionCase.AssignedCollectorId != null
            join user in _dbContext.Users.AsNoTracking() on collectionCase.AssignedCollectorId equals user.Id
            where user.EmployeeId != null && employeeIds.Contains(user.EmployeeId.Value)
            join portfolio in _dbContext.CollectionPortfolios.AsNoTracking() on collectionCase.PortfolioId equals portfolio.Id
            join organization in _dbContext.CollectionClientOrganizations.AsNoTracking() on portfolio.OrganizationId equals organization.Id
            select new OrganizationLinkRow(
                user.EmployeeId!.Value,
                false,
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType)).ToListAsync(cancellationToken);

        var teamRows = await (
            from team in _dbContext.CollectionTeams.AsNoTracking()
            where team.SupervisorId != null
            join user in _dbContext.Users.AsNoTracking() on team.SupervisorId equals user.Id
            where user.EmployeeId != null && employeeIds.Contains(user.EmployeeId.Value)
            join collectionCase in _dbContext.CollectionCases.AsNoTracking() on team.Id equals collectionCase.AssignedTeamId
            where !collectionCase.IsArchived
            join portfolio in _dbContext.CollectionPortfolios.AsNoTracking() on collectionCase.PortfolioId equals portfolio.Id
            join organization in _dbContext.CollectionClientOrganizations.AsNoTracking() on portfolio.OrganizationId equals organization.Id
            select new OrganizationLinkRow(
                user.EmployeeId!.Value,
                false,
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType)).ToListAsync(cancellationToken);

        var employees = await _dbContext.Employees.AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                DepartmentCode = employee.Department.Code,
                DepartmentName = employee.Department.Name,
                DepartmentNameArabic = employee.Department.NameArabic,
                PositionCode = employee.Position == null ? null : employee.Position.Code,
                PositionName = employee.Position == null ? null : employee.Position.Name,
                PositionNameArabic = employee.Position == null ? null : employee.Position.NameArabic
            })
            .ToListAsync(cancellationToken);
        var organizations = await _dbContext.CollectionClientOrganizations.AsNoTracking()
            .Select(organization => new
            {
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType
            })
            .ToListAsync(cancellationToken);
        var inferredRows = employees
            .SelectMany(employee => organizations
                .Where(organization =>
                    HrOrganizationLookup.Mentions(employee.DepartmentCode, organization.Code, organization.NameEnglish, organization.NameArabic)
                    || HrOrganizationLookup.Mentions(employee.DepartmentName, organization.Code, organization.NameEnglish, organization.NameArabic)
                    || HrOrganizationLookup.Mentions(employee.DepartmentNameArabic, organization.Code, organization.NameEnglish, organization.NameArabic)
                    || HrOrganizationLookup.Mentions(employee.PositionCode, organization.Code, organization.NameEnglish, organization.NameArabic)
                    || HrOrganizationLookup.Mentions(employee.PositionName, organization.Code, organization.NameEnglish, organization.NameArabic)
                    || HrOrganizationLookup.Mentions(employee.PositionNameArabic, organization.Code, organization.NameEnglish, organization.NameArabic))
                .Select(organization => new OrganizationLinkRow(
                    employee.Id,
                    false,
                    organization.Id,
                    organization.Code,
                    organization.NameArabic,
                    organization.NameEnglish,
                    organization.OrganizationType)))
            .ToList();

        return assignmentRows
            .Concat(accessRows)
            .Concat(grantRows)
            .Concat(caseRows)
            .Concat(teamRows)
            .Concat(inferredRows)
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<EmployeeOrganizationAssignmentDto>)group
                    .GroupBy(row => row.Id)
                    .Select(org => org.OrderByDescending(item => item.IsPrimary).First())
                    .OrderByDescending(org => org.IsPrimary)
                    .ThenBy(org => org.NameArabic)
                    .Select(org => new EmployeeOrganizationAssignmentDto(org.Id, org.Code, org.NameArabic, org.NameEnglish, org.OrganizationType))
                    .ToArray());
    }

    private sealed record OrganizationLinkRow(
        Guid EmployeeId,
        bool IsPrimary,
        Guid Id,
        string Code,
        string NameArabic,
        string NameEnglish,
        string OrganizationType);

    public Task<Employee?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    public async Task<EmployeeDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var employee = await _dbContext.Employees.AsNoTracking().Where(employee => employee.Id == id)
            .Select(employee => new
            {
                employee.Id,
                employee.EmployeeNumber,
                employee.FullName,
                employee.FullNameArabic,
                employee.FullNameEnglish,
                employee.NationalId,
                employee.DepartmentId,
                DepartmentName = isArabic ? employee.Department.NameArabic ?? employee.Department.Name : employee.Department.Name,
                DepartmentCode = employee.Department.Code,
                employee.PositionId,
                PositionName = employee.Position == null ? null : isArabic ? employee.Position.NameArabic ?? employee.Position.Name : employee.Position.Name,
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
                employee.MobileNumber,
                employee.WorkNumber,
                employee.PackageType
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null) return null;
        var names = EmployeeName.FillMissing(employee.FullName, employee.FullNameArabic, employee.FullNameEnglish);
        var organizations = await LoadOrganizationsByEmployeeAsync(new[] { id }, cancellationToken);
        var salary = await _dbContext.EmployeeCompensations.AsNoTracking()
            .Where(item => item.EmployeeId == id && item.IsCurrent)
            .OrderByDescending(item => item.EffectiveFrom)
            .Select(item => new { item.BasicSalary, item.Allowances })
            .FirstOrDefaultAsync(cancellationToken);
        return new EmployeeDetailsDto(
            employee.Id,
            employee.EmployeeNumber,
            EmployeeName.Display(isArabic, employee.FullName, names.Arabic, names.English),
            employee.NationalId,
            employee.DepartmentId,
            employee.DepartmentName,
            employee.DepartmentCode,
            employee.PositionId,
            employee.PositionName,
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
            employee.MobileNumber,
            names.Arabic,
            names.English,
            organizations.GetValueOrDefault(id) ?? Array.Empty<EmployeeOrganizationAssignmentDto>(),
            salary?.BasicSalary,
            salary?.Allowances,
            employee.WorkNumber,
            employee.PackageType);
    }

    public async Task<IReadOnlyCollection<DepartmentOptionDto>> GetDepartmentsAsync(CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        return await _dbContext.Departments.AsNoTracking()
            .Where(department => department.IsActive && DepartmentCodes.OperationalUnits.Contains(department.Code))
            .OrderBy(department => isArabic ? department.NameArabic ?? department.Name : department.Name)
            .Select(department => new DepartmentOptionDto(
                department.Id,
                isArabic ? department.NameArabic ?? department.Name : department.Name,
                department.Code))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmployeeOrganizationAssignmentDto>> GetOrganizationsAsync(CancellationToken cancellationToken) =>
        await _dbContext.CollectionClientOrganizations.AsNoTracking()
            .Where(organization => organization.IsActive)
            .OrderBy(organization => ApiTextLocalizer.IsArabic ? organization.NameArabic : organization.NameEnglish)
            .Select(organization => new EmployeeOrganizationAssignmentDto(
                organization.Id,
                organization.Code,
                organization.NameArabic,
                organization.NameEnglish,
                organization.OrganizationType))
            .ToArrayAsync(cancellationToken);

    public async Task<bool> OrganizationsExistAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken)
    {
        var distinctIds = organizationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinctIds.Length != organizationIds.Distinct().Count()) return false;
        return await _dbContext.CollectionClientOrganizations.CountAsync(
            organization => organization.IsActive && distinctIds.Contains(organization.Id), cancellationToken) == distinctIds.Length;
    }

    public async Task ReplaceOrganizationAssignmentsAsync(Guid employeeId, IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.EmployeeOrganizationAssignments
            .Where(assignment => assignment.EmployeeId == employeeId)
            .ToArrayAsync(cancellationToken);
        _dbContext.EmployeeOrganizationAssignments.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        foreach (var organizationId in organizationIds.Distinct())
            _dbContext.EmployeeOrganizationAssignments.Add(new EmployeeOrganizationAssignment(
                employeeId, organizationId, isPrimary: organizationId == organizationIds.FirstOrDefault(), now));
    }

    public Task<bool> DepartmentExistsAsync(Guid departmentId, CancellationToken cancellationToken) =>
        _dbContext.Departments.AnyAsync(department => department.Id == departmentId && department.IsActive, cancellationToken);

    public Task<bool> PositionExistsAsync(Guid positionId, CancellationToken cancellationToken) =>
        _dbContext.Positions.AnyAsync(position => position.Id == positionId && position.IsActive, cancellationToken);

    public Task<PositionLookupDto?> GetPositionAsync(Guid positionId, CancellationToken cancellationToken) =>
        _dbContext.Positions.AsNoTracking()
            .Where(position => position.Id == positionId)
            .Select(position => new PositionLookupDto(position.Id, position.Code, position.Name, position.NameArabic, position.DepartmentId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> EmployeeNumberExistsAsync(string employeeNumber, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalized = employeeNumber.Trim().ToLower();
        return _dbContext.Employees.AnyAsync(employee => employee.EmployeeNumber.ToLower() == normalized && (!excludingId.HasValue || employee.Id != excludingId.Value), cancellationToken);
    }

    public Task<bool> NationalIdExistsAsync(string nationalId, Guid? excludingId, CancellationToken cancellationToken) =>
        _dbContext.Employees.AnyAsync(employee => employee.NationalId == nationalId && (!excludingId.HasValue || employee.Id != excludingId.Value), cancellationToken);

    public void Add(Employee employee) => _dbContext.Employees.Add(employee);

    public async Task UpsertCurrentSalaryAsync(Guid employeeId, decimal basicSalary, decimal allowances, DateOnly effectiveFrom, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await _dbContext.EmployeeCompensations
            .Where(item => item.EmployeeId == employeeId && item.IsCurrent)
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            _dbContext.EmployeeCompensations.Add(new EmployeeCompensation(
                employeeId, basicSalary, allowances, effectiveFrom, null, true, null, null, null, null, now));
            return;
        }

        current.Update(
            basicSalary,
            allowances,
            current.EffectiveFrom,
            null,
            true,
            current.BankName,
            current.BankAccountNumber,
            current.Iban,
            current.Notes,
            now);
    }

    public async Task DeleteUnusedAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new HrNotFoundException("Employee was not found.");

        if (await _dbContext.Users.AnyAsync(item => item.EmployeeId == id, cancellationToken))
            throw new HrConflictException("This employee is linked to a system user account and cannot be deleted. Unlink the account in Admin, or archive the employee.");

        var hasHistory =
            await _dbContext.AttendanceRecords.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.EmployeeAbsences.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.LeaveRequests.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.EmployeeDocuments.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.SocialInsuranceRecords.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.HrExcuseMissions.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.EmployeeDelegations.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.AccountingEmployeePayrolls.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.AccountingTransportationClaims.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.AccountingCollectorCommissions.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.AccountingSupervisorCommissions.AnyAsync(item => item.EmployeeId == id, cancellationToken)
            || await _dbContext.AttendanceImportRows.AnyAsync(item => item.EmployeeId == id, cancellationToken);

        if (hasHistory)
            throw new HrConflictException("This employee has operational history and cannot be deleted. Archive the record instead.");

        await _dbContext.Employees.Where(item => item.DirectManagerId == id)
            .ExecuteUpdateAsync(calls => calls.SetProperty(item => item.DirectManagerId, (Guid?)null), cancellationToken);
        await _dbContext.HrAuditLogs.Where(item => item.EmployeeId == id)
            .ExecuteUpdateAsync(calls => calls.SetProperty(item => item.EmployeeId, (Guid?)null), cancellationToken);

        _dbContext.EmployeeEmergencyContacts.RemoveRange(
            await _dbContext.EmployeeEmergencyContacts.Where(item => item.EmployeeId == id).ToListAsync(cancellationToken));
        _dbContext.EmployeeCompensations.RemoveRange(
            await _dbContext.EmployeeCompensations.Where(item => item.EmployeeId == id).ToListAsync(cancellationToken));
        _dbContext.EmployeeContracts.RemoveRange(
            await _dbContext.EmployeeContracts.Where(item => item.EmployeeId == id).ToListAsync(cancellationToken));
        _dbContext.EmployeeLeaveEntitlements.RemoveRange(
            await _dbContext.EmployeeLeaveEntitlements.Where(item => item.EmployeeId == id).ToListAsync(cancellationToken));

        _dbContext.Employees.Remove(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePermanentlyAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await GetTrackedByIdAsync(id, cancellationToken)
            ?? throw new HrNotFoundException("Employee was not found.");

        var now = DateTimeOffset.UtcNow;
        foreach (var user in await _dbContext.Users.Where(item => item.EmployeeId == id).ToListAsync(cancellationToken))
            user.UnlinkEmployee(now);

        await _dbContext.Employees.Where(item => item.DirectManagerId == id)
            .ExecuteUpdateAsync(calls => calls.SetProperty(item => item.DirectManagerId, (Guid?)null), cancellationToken);
        await _dbContext.HrAuditLogs.Where(item => item.EmployeeId == id)
            .ExecuteUpdateAsync(calls => calls.SetProperty(item => item.EmployeeId, (Guid?)null), cancellationToken);
        await _dbContext.AttendanceImportRows.Where(item => item.EmployeeId == id)
            .ExecuteUpdateAsync(calls => calls.SetProperty(item => item.EmployeeId, (Guid?)null), cancellationToken);
        await _dbContext.AccountingCollectorCommissions.Where(item => item.EmployeeId == id)
            .ExecuteUpdateAsync(calls => calls.SetProperty(item => item.EmployeeId, (Guid?)null), cancellationToken);
        await _dbContext.AccountingSupervisorCommissions.Where(item => item.EmployeeId == id)
            .ExecuteUpdateAsync(calls => calls.SetProperty(item => item.EmployeeId, (Guid?)null), cancellationToken);

        var missionIds = await _dbContext.HrExcuseMissions.Where(item => item.EmployeeId == id).Select(item => item.Id).ToListAsync(cancellationToken);
        if (missionIds.Count > 0)
            await _dbContext.HrExcuseAttachments.Where(item => missionIds.Contains(item.ExcuseId)).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.HrExcuseMissions.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.LeaveRequests.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.AttendanceRecords.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmployeeAbsences.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmployeeDocuments.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.SocialInsuranceRecords.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmployeeDelegations.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.AccountingEmployeePayrolls.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.AccountingTransportationClaims.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmployeeEmergencyContacts.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmployeeCompensations.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmployeeContracts.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.EmployeeLeaveEntitlements.Where(item => item.EmployeeId == id).ExecuteDeleteAsync(cancellationToken);

        _dbContext.Employees.Remove(employee);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new HrConflictException("This employee still has related records and could not be deleted.");
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
