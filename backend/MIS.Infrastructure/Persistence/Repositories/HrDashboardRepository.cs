using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Repositories;

public sealed class HrDashboardRepository : IHrDashboardRepository
{
    private const int AlertHorizonDays = 30;
    private const int TrendDays = 30;
    private readonly ApplicationDbContext _dbContext;

    public HrDashboardRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HrDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var today = await GetCompanyDateAsync(cancellationToken);
        var totalEmployees = await _dbContext.Employees.AsNoTracking().CountAsync(item => !item.IsArchived, cancellationToken);
        var activeEmployees = await _dbContext.Employees.AsNoTracking().CountAsync(item => !item.IsArchived && item.IsActive, cancellationToken);
        var inactiveEmployees = totalEmployees - activeEmployees;
        var totalDocuments = await _dbContext.EmployeeDocuments.AsNoTracking().CountAsync(item => !item.IsDeleted && !item.Employee.IsArchived, cancellationToken);
        var attentionThrough = today.AddDays(AlertHorizonDays);
        var documentsRequiringAttention = await _dbContext.EmployeeDocuments.AsNoTracking().CountAsync(
            item => !item.IsDeleted && !item.Employee.IsArchived && item.Employee.IsActive && item.ExpiryDate.HasValue && item.ExpiryDate.Value <= attentionThrough,
            cancellationToken);

        var employeesByDepartment = await _dbContext.Departments.AsNoTracking()
            .Where(department => department.IsActive && DepartmentCodes.OperationalUnits.Contains(department.Code))
            .OrderByDescending(department => _dbContext.Employees.Count(employee => !employee.IsArchived && employee.DepartmentId == department.Id))
            .ThenBy(department => department.Name)
            .Select(department => new DepartmentEmployeeCountDto(
                department.Id,
                isArabic ? department.NameArabic ?? department.Name : department.Name,
                department.Code,
                _dbContext.Employees.Count(employee => !employee.IsArchived && employee.DepartmentId == department.Id)))
            .ToArrayAsync(cancellationToken);

        var assignmentCounts = await _dbContext.EmployeeOrganizationAssignments.AsNoTracking()
            .Where(assignment => !assignment.Employee.IsArchived)
            .GroupBy(assignment => assignment.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var assignmentCountLookup = assignmentCounts.ToDictionary(item => item.OrganizationId, item => item.Count);
        var employeesByOrganization = (await _dbContext.CollectionClientOrganizations.AsNoTracking()
                .Where(organization => organization.IsActive)
                .Select(organization => new { organization.Id, organization.NameArabic, organization.NameEnglish, organization.Code })
                .ToListAsync(cancellationToken))
            .Select(organization => new OrganizationEmployeeCountDto(
                organization.Id,
                isArabic ? organization.NameArabic : organization.NameEnglish,
                organization.Code,
                assignmentCountLookup.GetValueOrDefault(organization.Id)))
            .OrderByDescending(item => item.EmployeeCount)
            .ThenBy(item => item.OrganizationName)
            .ToArray();

        var employeesByBranch = (await _dbContext.Branches.AsNoTracking()
                .OrderByDescending(branch => _dbContext.Employees.Count(employee => !employee.IsArchived && employee.BranchId == branch.Id))
                .ThenBy(branch => branch.Name)
                .Select(branch => new BranchEmployeeCountDto(
                    branch.Id,
                    isArabic ? branch.NameArabic ?? branch.Name : branch.Name,
                    branch.Code,
                    _dbContext.Employees.Count(employee => !employee.IsArchived && employee.BranchId == branch.Id)))
                .ToListAsync(cancellationToken))
            .ToList();
        var unassignedBranchCount = await _dbContext.Employees.AsNoTracking().CountAsync(item => !item.IsArchived && item.BranchId == null, cancellationToken);
        if (unassignedBranchCount > 0)
            employeesByBranch.Add(new BranchEmployeeCountDto(null, ApiTextLocalizer.Localize("Unassigned"), null, unassignedBranchCount));

        var todayAttendance = await BuildTodayAttendanceAsync(today, cancellationToken);
        var alerts = await BuildAlertsAsync(today, cancellationToken);
        var activity = await BuildRecentActivityAsync(cancellationToken);
        var (attendanceTrend, absenceTrend) = await BuildTrendsAsync(today, cancellationToken);

        return new HrDashboardDto(
            totalEmployees,
            activeEmployees,
            todayAttendance.Absent,
            true,
            documentsRequiringAttention,
            true,
            totalDocuments,
            employeesByDepartment,
            employeesByOrganization,
            inactiveEmployees,
            employeesByBranch,
            todayAttendance,
            alerts,
            activity,
            attendanceTrend,
            absenceTrend);
    }

    private async Task<TodayAttendanceSummaryDto> BuildTodayAttendanceAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.AttendanceRecords.AsNoTracking()
            .Where(item => !item.IsDeleted && !item.Employee.IsArchived && item.Employee.IsActive && item.AttendanceDate == today)
            .Select(item => new { item.EmployeeId, item.Status, item.CheckIn, item.CheckOut })
            .ToListAsync(cancellationToken);
        var approvedLeaveEmployees = await _dbContext.LeaveRequests.AsNoTracking()
            .Where(item => !item.Employee.IsArchived && item.Employee.IsActive && item.Status == LeaveRequestStatuses.Approved &&
                           item.StartDate <= today && item.EndDate >= today)
            .Select(item => item.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var absenceEmployees = await _dbContext.EmployeeAbsences.AsNoTracking()
            .Where(item => !item.Employee.IsArchived && item.Employee.IsActive && item.AbsenceDate == today)
            .Select(item => item.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var onLeave = approvedLeaveEmployees
            .Concat(rows.Where(item => item.Status == AttendanceValues.LeaveStatus).Select(item => item.EmployeeId))
            .ToHashSet();
        var absent = absenceEmployees
            .Concat(rows.Where(item => item.Status == AttendanceValues.AbsentStatus).Select(item => item.EmployeeId))
            .Where(employeeId => !onLeave.Contains(employeeId))
            .ToHashSet();

        return new TodayAttendanceSummaryDto(
            rows.Count(item => item.Status == AttendanceValues.PresentStatus),
            absent.Count,
            rows.Count(item => item.Status == AttendanceValues.LateStatus),
            onLeave.Count,
            rows.Count(item => item.CheckIn.HasValue && !item.CheckOut.HasValue &&
                               item.Status is AttendanceValues.PresentStatus or AttendanceValues.LateStatus));
    }

    private async Task<IReadOnlyCollection<HrDashboardAlertDto>> BuildAlertsAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var through = today.AddDays(AlertHorizonDays);
        var recentExpiryFloor = today.AddDays(-30);
        var alerts = new List<HrDashboardAlertDto>();

        var contracts = await _dbContext.EmployeeContracts.AsNoTracking()
            .Where(item => !item.Employee.IsArchived && item.Employee.IsActive && item.ContractEndDate.HasValue &&
                           item.ContractEndDate.Value >= recentExpiryFloor && item.ContractEndDate.Value <= through &&
                           item.Status != EmployeeContract.TerminatedStatus)
            .Select(item => new
            {
                item.Id,
                item.EmployeeId,
                item.Employee.EmployeeNumber,
                EmployeeName = isArabic ? item.Employee.FullNameArabic ?? item.Employee.FullName : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                DueDate = item.ContractEndDate!.Value,
                TypeName = isArabic ? item.ContractType.NameArabic ?? item.ContractType.Name : item.ContractType.Name
            })
            .ToListAsync(cancellationToken);
        alerts.AddRange(contracts.Select(item => CreateAlert(
            "Contract",
            item.Id,
            item.EmployeeId,
            item.EmployeeNumber,
            item.EmployeeName,
            isArabic ? $"عقد {item.TypeName}" : $"{item.TypeName} contract",
            item.DueDate,
            today)));

        var documents = await _dbContext.EmployeeDocuments.AsNoTracking()
            .Where(item => !item.IsDeleted && !item.Employee.IsArchived && item.Employee.IsActive && item.ExpiryDate.HasValue &&
                           item.ExpiryDate.Value >= recentExpiryFloor && item.ExpiryDate.Value <= through)
            .Select(item => new
            {
                item.Id,
                item.EmployeeId,
                item.Employee.EmployeeNumber,
                EmployeeName = isArabic ? item.Employee.FullNameArabic ?? item.Employee.FullName : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                DueDate = item.ExpiryDate!.Value,
                DocumentType = isArabic
                    ? item.DocumentTypeDefinition != null
                        ? item.DocumentTypeDefinition.NameArabic ?? item.DocumentTypeDefinition.Name
                        : item.DocumentType
                    : item.DocumentType
            })
            .ToListAsync(cancellationToken);
        alerts.AddRange(documents.Select(item => CreateAlert(
            "Document",
            item.Id,
            item.EmployeeId,
            item.EmployeeNumber,
            item.EmployeeName,
            isArabic ? ApiTextLocalizer.Localize(item.DocumentType) : item.DocumentType,
            item.DueDate,
            today)));

        var probations = await _dbContext.EmployeeContracts.AsNoTracking()
            .Where(item => !item.Employee.IsArchived && item.Employee.IsActive && item.ProbationEndDate.HasValue &&
                           item.ProbationEndDate.Value >= today.AddDays(-7) && item.ProbationEndDate.Value <= through)
            .Select(item => new
            {
                item.Id,
                item.EmployeeId,
                item.Employee.EmployeeNumber,
                EmployeeName = isArabic ? item.Employee.FullNameArabic ?? item.Employee.FullName : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                DueDate = item.ProbationEndDate!.Value
            })
            .ToListAsync(cancellationToken);
        alerts.AddRange(probations.Select(item => CreateAlert(
            "Probation",
            item.Id,
            item.EmployeeId,
            item.EmployeeNumber,
            item.EmployeeName,
            ApiTextLocalizer.Localize("Probation period"),
            item.DueDate,
            today)));

        return alerts
            .OrderBy(item => item.DaysRemaining < 0 ? 0 : 1)
            .ThenBy(item => item.DaysRemaining)
            .ThenBy(item => item.EmployeeName)
            .Take(50)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<HrDashboardActivityDto>> BuildRecentActivityAsync(
        CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var rows = await _dbContext.HrAuditLogs.AsNoTracking()
            .OrderByDescending(item => item.Timestamp)
            .Take(12)
            .Select(item => new
            {
                item.Id,
                item.Action,
                item.Description,
                Username = item.User == null ? "System" : item.User.Username,
                item.EmployeeId,
                EmployeeName = item.Employee == null
                    ? null
                    : isArabic
                        ? item.Employee.FullNameArabic ?? item.Employee.FullName
                        : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                item.Timestamp
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new HrDashboardActivityDto(
            item.Id,
            item.Action,
            item.Description is null ? BuildActivityMessage(item.Action, item.EmployeeName) : ApiTextLocalizer.Localize(item.Description),
            ApiTextLocalizer.Localize(item.Username),
            item.EmployeeId,
            item.EmployeeName,
            item.Timestamp)).ToArray();
    }

    private async Task<(IReadOnlyCollection<AttendanceTrendPointDto>, IReadOnlyCollection<AbsenceTrendPointDto>)> BuildTrendsAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var from = today.AddDays(-(TrendDays - 1));
        var attendanceCounts = await _dbContext.AttendanceRecords.AsNoTracking()
            .Where(item => !item.IsDeleted && item.AttendanceDate >= from && item.AttendanceDate <= today)
            .GroupBy(item => new { item.AttendanceDate, item.Status })
            .Select(group => new { Date = group.Key.AttendanceDate, group.Key.Status, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var absenceCounts = await _dbContext.EmployeeAbsences.AsNoTracking()
            .Where(item => item.AbsenceDate >= from && item.AbsenceDate <= today)
            .GroupBy(item => item.AbsenceDate)
            .Select(group => new { Date = group.Key, Count = group.Select(item => item.EmployeeId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        var attendanceLookup = attendanceCounts.ToDictionary(item => (item.Date, item.Status), item => item.Count);
        var absenceLookup = absenceCounts.ToDictionary(item => item.Date, item => item.Count);
        var attendanceTrend = new List<AttendanceTrendPointDto>(TrendDays);
        var absenceTrend = new List<AbsenceTrendPointDto>(TrendDays);
        for (var offset = 0; offset < TrendDays; offset++)
        {
            var date = from.AddDays(offset);
            attendanceTrend.Add(new AttendanceTrendPointDto(
                date,
                attendanceLookup.GetValueOrDefault((date, AttendanceValues.PresentStatus)),
                attendanceLookup.GetValueOrDefault((date, AttendanceValues.LateStatus)),
                attendanceLookup.GetValueOrDefault((date, AttendanceValues.AbsentStatus)),
                attendanceLookup.GetValueOrDefault((date, AttendanceValues.LeaveStatus))));
            absenceTrend.Add(new AbsenceTrendPointDto(date, absenceLookup.GetValueOrDefault(date)));
        }

        return (attendanceTrend, absenceTrend);
    }

    private async Task<DateOnly> GetCompanyDateAsync(CancellationToken cancellationToken)
    {
        var timeZoneId = await _dbContext.WorkingCalendars.AsNoTracking()
            .Select(item => item.TimeZoneId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(timeZoneId)) return DateOnly.FromDateTime(DateTime.UtcNow);
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    private static HrDashboardAlertDto CreateAlert(
        string category,
        Guid entityId,
        Guid employeeId,
        string employeeNumber,
        string employeeName,
        string title,
        DateOnly dueDate,
        DateOnly today)
    {
        var days = dueDate.DayNumber - today.DayNumber;
        var severity = days < 0 ? "Expired" : days <= 7 ? "Critical" : days <= 15 ? "Warning" : "Info";
        return new HrDashboardAlertDto(
            category,
            entityId,
            employeeId,
            employeeNumber,
            employeeName,
            title,
            dueDate,
            days,
            severity);
    }

    private static string BuildActivityMessage(string action, string? employeeName)
    {
        var words = string.Concat(action.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
        words = ApiTextLocalizer.LocalizeAction(action, words);
        return employeeName is null ? words : $"{words} — {employeeName}";
    }
}
