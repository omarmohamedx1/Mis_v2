using System.Data;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrAttendanceService : IHrAttendanceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IWorkingCalendarCalculator _calendar;
    private readonly ICurrentUserContext _currentUser;
    private readonly IHrAuditService _audit;

    public HrAttendanceService(
        ApplicationDbContext dbContext,
        IWorkingCalendarCalculator calendar,
        ICurrentUserContext currentUser,
        IHrAuditService audit)
    {
        _dbContext = dbContext;
        _calendar = calendar;
        _currentUser = currentUser;
        _audit = audit;
    }

    // Explain approved time periods without changing punches, attendance status, or payroll policy.
    // Check the live visit as well so a worker delay cannot expose a stale approval.
    private IQueryable<HrExcuseMission> ApprovedExcuses() => _dbContext.HrExcuseMissions.AsNoTracking().Where(m => m.Status == "Approved" && m.EmployeeId != null &&
        (m.SourceVisitId == null || (m.SourceVisit!.ScheduledAt == m.SourceScheduledAt && m.SourceVisit.CollectorId == m.SourceCollectorId && m.SourceVisit.Collector.EmployeeId == m.EmployeeId && m.SourceVisit.Status != "CANCELLED" && m.SourceVisit.Status != "MISSED" && m.SourceVisit.Status != "FAILED")));
    private static AttendanceExcuseDto MapExcuse(HrExcuseMission m) => new(m.Id, m.Type, m.SourceType, m.Date, m.FromTime, m.ToTime, m.Reason, m.FullDay);

    public async Task<PagedAttendanceRecordsDto> GetPagedAsync(
        AttendanceFilterDto filter,
        CancellationToken cancellationToken)
    {
        ValidatePage(filter.Page, filter.PageSize);
        ValidateDateRange(filter.DateFrom, filter.DateTo);
        var isArabic = ApiTextLocalizer.IsArabic;

        var query = _dbContext.AttendanceRecords.AsNoTracking().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.Employee.EmployeeNumber, pattern) ||
                EF.Functions.ILike(item.Employee.FullName, pattern) ||
                (item.Employee.FullNameArabic != null && EF.Functions.ILike(item.Employee.FullNameArabic, pattern)) ||
                (item.Employee.FullNameEnglish != null && EF.Functions.ILike(item.Employee.FullNameEnglish, pattern)));
        }
        if (filter.EmployeeId.HasValue) query = query.Where(item => item.EmployeeId == filter.EmployeeId.Value);
        if (filter.DepartmentId.HasValue) query = query.Where(item => item.Employee.DepartmentId == filter.DepartmentId.Value);
        if (filter.OrganizationId.HasValue)
            query = query.Where(item => _dbContext.EmployeeOrganizationAssignments.Any(assignment =>
                assignment.EmployeeId == item.EmployeeId && assignment.OrganizationId == filter.OrganizationId.Value));
        if (filter.BranchId.HasValue) query = query.Where(item => item.Employee.BranchId == filter.BranchId.Value);
        if (filter.DateFrom.HasValue) query = query.Where(item => item.AttendanceDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue) query = query.Where(item => item.AttendanceDate <= filter.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = AttendanceValues.NormalizeStatus(filter.Status)
                ?? throw new HrValidationException("Attendance status is invalid.");
            query = query.Where(item => item.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            var source = AttendanceValues.NormalizeSource(filter.Source)
                ?? throw new HrValidationException("Attendance source is invalid.");
            query = query.Where(item => item.Source == source);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySort(query, filter.SortBy, filter.SortDescending);
        var records = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(item => new AttendanceProjection(
                item.Id,
                item.EmployeeId,
                item.Employee.EmployeeNumber,
                isArabic ? item.Employee.FullNameArabic ?? item.Employee.FullName : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                item.Employee.DepartmentId,
                isArabic ? item.Employee.Department.NameArabic ?? item.Employee.Department.Name : item.Employee.Department.Name,
                item.Employee.BranchId,
                item.Employee.Branch == null ? null : isArabic ? item.Employee.Branch.NameArabic ?? item.Employee.Branch.Name : item.Employee.Branch.Name,
                item.AttendanceDate,
                item.CheckIn,
                item.CheckOut,
                item.WorkingMinutes,
                item.LateMinutes,
                item.EarlyLeaveMinutes,
                item.OvertimeMinutes,
                item.Status,
                item.Source,
                item.Notes,
                item.ImportBatchId,
                item.IsManuallyAdjusted,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        var employees = records.Select(r => r.EmployeeId).Distinct().ToArray();
        var dates = records.Select(r => r.AttendanceDate).Distinct().ToArray();
        var excuses = await ApprovedExcuses().Where(m => employees.Contains(m.EmployeeId!.Value) && dates.Contains(m.Date)).ToListAsync(cancellationToken);
        return new PagedAttendanceRecordsDto(
            records.Select(r => MapListItem(r) with { ApprovedExcuses = excuses.Where(m => m.EmployeeId == r.EmployeeId && m.Date == r.AttendanceDate).Select(MapExcuse).ToArray() }).ToArray(),
            totalCount,
            filter.Page,
            filter.PageSize,
            Pages(totalCount, filter.PageSize));
    }

    public async Task<AttendanceDetailsDto> GetDetailsAsync(Guid attendanceId, CancellationToken cancellationToken)
    {
        var projection = await QueryProjection(attendanceId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new HrNotFoundException("Attendance record was not found.");
        var excuses = await ApprovedExcuses().Where(m => m.EmployeeId == projection.EmployeeId && m.Date == projection.AttendanceDate).ToListAsync(cancellationToken);
        return MapDetails(projection) with { ApprovedExcuses = excuses.Select(MapExcuse).ToArray() };
    }

    public async Task<AttendanceDetailsDto> CreateManualAsync(
        CreateManualAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureEmployeeEligibleForDateAsync(request.EmployeeId, request.AttendanceDate, cancellationToken);
        await EnsureDateAvailableAsync(request.EmployeeId, request.AttendanceDate, null, cancellationToken);
        var calculation = await CalculateAsync(
            request.EmployeeId,
            request.AttendanceDate,
            request.CheckIn,
            request.CheckOut,
            request.Status,
            cancellationToken);
        var checkInUtc = ToUtc(request.CheckIn);
        var checkOutUtc = ToUtc(request.CheckOut);
        var now = DateTimeOffset.UtcNow;
        var entity = new AttendanceRecord(
            request.EmployeeId,
            request.AttendanceDate,
            checkInUtc,
            checkOutUtc,
            calculation.WorkingMinutes,
            calculation.LateMinutes,
            calculation.EarlyLeaveMinutes,
            calculation.OvertimeMinutes,
            calculation.Status,
            AttendanceValues.ManualSource,
            request.Notes,
            null,
            true,
            _currentUser.UserId,
            now);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.AttendanceRecords.Add(entity);
        AddManualPunches(entity.Id, checkInUtc, checkOutUtc, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var created = await GetDetailsAsync(entity.Id, cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            "AttendanceAdded",
            nameof(AttendanceRecord),
            entity.Id.ToString(),
            entity.EmployeeId,
            null,
            created,
            $"Added manual attendance for {entity.AttendanceDate:yyyy-MM-dd}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<AttendanceDetailsDto> UpdateManualAsync(
        Guid attendanceId,
        UpdateManualAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.AttendanceRecords.SingleOrDefaultAsync(
                item => item.Id == attendanceId && !item.IsDeleted,
                cancellationToken)
            ?? throw new HrNotFoundException("Attendance record was not found.");
        await EnsureEmployeeEligibleForDateAsync(request.EmployeeId, request.AttendanceDate, cancellationToken);
        await EnsureDateAvailableAsync(request.EmployeeId, request.AttendanceDate, attendanceId, cancellationToken);
        var oldValue = await GetDetailsAsync(attendanceId, cancellationToken);
        var calculation = await CalculateAsync(
            request.EmployeeId,
            request.AttendanceDate,
            request.CheckIn,
            request.CheckOut,
            request.Status,
            cancellationToken);
        var checkInUtc = ToUtc(request.CheckIn);
        var checkOutUtc = ToUtc(request.CheckOut);
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        entity.UpdateSummary(
            request.EmployeeId,
            request.AttendanceDate,
            checkInUtc,
            checkOutUtc,
            calculation.WorkingMinutes,
            calculation.LateMinutes,
            calculation.EarlyLeaveMinutes,
            calculation.OvertimeMinutes,
            calculation.Status,
            request.Notes,
            true,
            _currentUser.UserId,
            now);

        if (entity.Source == AttendanceValues.ManualSource)
        {
            await _dbContext.AttendancePunches
                .Where(item => item.AttendanceRecordId == entity.Id)
                .ExecuteDeleteAsync(cancellationToken);
            AddManualPunches(entity.Id, checkInUtc, checkOutUtc, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var updated = await GetDetailsAsync(entity.Id, cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            "AttendanceUpdated",
            nameof(AttendanceRecord),
            entity.Id.ToString(),
            entity.EmployeeId,
            oldValue,
            updated,
            $"Updated attendance for {entity.AttendanceDate:yyyy-MM-dd}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task DeleteAsync(
        Guid attendanceId,
        DeleteAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.AttendanceRecords.SingleOrDefaultAsync(
                item => item.Id == attendanceId && !item.IsDeleted,
                cancellationToken)
            ?? throw new HrNotFoundException("Attendance record was not found.");
        var oldValue = await GetDetailsAsync(attendanceId, cancellationToken);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        entity.Delete(_currentUser.UserId, reason, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            "AttendanceDeleted",
            nameof(AttendanceRecord),
            entity.Id.ToString(),
            entity.EmployeeId,
            oldValue,
            null,
            reason ?? "Soft-deleted attendance record."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProcessAttendanceDayResultDto> ProcessDayAsync(
        ProcessAttendanceDayRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AttendanceDate == default) throw new HrValidationException("Attendance date is required.");
        var schedule = await _calendar.GetScheduleAsync(request.AttendanceDate, cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        var now = DateTimeOffset.UtcNow;
        var companyNow = TimeZoneInfo.ConvertTime(now, zone);
        var companyToday = DateOnly.FromDateTime(companyNow.DateTime);
        if (request.AttendanceDate > companyToday)
            throw new HrValidationException("A future attendance day cannot be processed.");
        if (request.AttendanceDate == companyToday && schedule.IsWorkingDay)
        {
            if (!schedule.EndTime.HasValue)
                throw new HrValidationException("The current working day cannot be processed because its scheduled end time is not configured.");

            var scheduleEndDate = schedule.StartTime.HasValue && schedule.EndTime.Value <= schedule.StartTime.Value
                ? request.AttendanceDate.AddDays(1)
                : request.AttendanceDate;
            var scheduledEnd = _calendar.ToInstant(scheduleEndDate, schedule.EndTime.Value, schedule.TimeZoneId);
            if (now < scheduledEnd)
            {
                throw new HrConflictException(
                    $"The current working day cannot be processed before the scheduled shift ends at {scheduledEnd:yyyy-MM-dd HH:mm zzz}.");
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var employeeIds = await _dbContext.Employees.AsNoTracking()
            .Where(item =>
                (!item.HireDate.HasValue || item.HireDate.Value <= request.AttendanceDate) &&
                ((item.Status == Employee.ActiveStatus && item.IsActive && !item.TerminationDate.HasValue) ||
                 (item.Status == Employee.TerminatedStatus && item.TerminationDate.HasValue &&
                  item.TerminationDate.Value >= request.AttendanceDate)))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var existing = await _dbContext.AttendanceRecords.AsNoTracking()
            .Where(item => employeeIds.Contains(item.EmployeeId) && item.AttendanceDate == request.AttendanceDate && !item.IsDeleted)
            .Select(item => item.EmployeeId)
            .ToHashSetAsync(cancellationToken);
        var approvedLeaves = await _dbContext.LeaveRequests.AsNoTracking()
            .Where(item => employeeIds.Contains(item.EmployeeId) &&
                           item.Status == LeaveRequestStatuses.Approved &&
                           item.StartDate <= request.AttendanceDate && item.EndDate >= request.AttendanceDate)
            .Select(item => item.EmployeeId)
            .ToHashSetAsync(cancellationToken);

        var absent = 0;
        var onLeave = 0;
        var holiday = 0;
        var weekend = 0;
        var notes = string.IsNullOrWhiteSpace(request.Notes)
            ? schedule.IsWorkingDay
                ? "Created by attendance day processing."
                : "Created by attendance day processing for a configured non-working day."
            : $"Created by attendance day processing. {request.Notes.Trim()}";
        foreach (var employeeId in employeeIds)
        {
            if (existing.Contains(employeeId)) continue;
            string status;
            if (approvedLeaves.Contains(employeeId))
            {
                status = AttendanceValues.LeaveStatus;
                onLeave++;
            }
            else if (schedule.IsWorkingDay)
            {
                status = AttendanceValues.AbsentStatus;
                absent++;
            }
            else if (!string.IsNullOrWhiteSpace(schedule.ExceptionType))
            {
                status = AttendanceValues.HolidayStatus;
                holiday++;
            }
            else
            {
                status = AttendanceValues.WeekendStatus;
                weekend++;
            }

            _dbContext.AttendanceRecords.Add(new AttendanceRecord(
                employeeId,
                request.AttendanceDate,
                null,
                null,
                0,
                0,
                0,
                0,
                status,
                AttendanceValues.SystemProcessingSource,
                notes,
                null,
                false,
                _currentUser.UserId,
                now));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var created = absent + onLeave + holiday + weekend;
        var result = new ProcessAttendanceDayResultDto(
            request.AttendanceDate,
            created,
            absent,
            onLeave,
            holiday,
            weekend,
            existing.Count);
        var executionId = Guid.NewGuid();
        await _audit.WriteAsync(new AuditWriteRequest(
            "AttendanceDayProcessed",
            "AttendanceDay",
            executionId.ToString(),
            null,
            null,
            result,
            $"Processed missing attendance records for {request.AttendanceDate:yyyy-MM-dd}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<AttendanceCalculation> CalculateAsync(
        Guid employeeId,
        DateOnly attendanceDate,
        DateTimeOffset? checkIn,
        DateTimeOffset? checkOut,
        string requestedStatus,
        CancellationToken cancellationToken)
    {
        if (attendanceDate == default) throw new HrValidationException("Attendance date is required.");
        if (checkOut < checkIn) throw new HrValidationException("Check-out cannot be before check-in.");
        if (checkIn.HasValue && checkOut.HasValue && checkOut.Value - checkIn.Value > TimeSpan.FromHours(48))
            throw new HrValidationException("An attendance interval cannot exceed 48 hours.");
        var status = AttendanceValues.NormalizeStatus(requestedStatus)
            ?? throw new HrValidationException("Attendance status is invalid.");

        var isNonAttendanceStatus = status is AttendanceValues.AbsentStatus or AttendanceValues.LeaveStatus or
            AttendanceValues.HolidayStatus or AttendanceValues.WeekendStatus;
        if (isNonAttendanceStatus && (checkIn.HasValue || checkOut.HasValue))
            throw new HrValidationException("Absent, leave, holiday, and weekend attendance cannot contain check-in or check-out punches.");

        if (await HasApprovedLeaveAsync(employeeId, attendanceDate, cancellationToken))
        {
            if (checkIn.HasValue || checkOut.HasValue)
                throw new HrValidationException("The employee has an approved leave on this date; manual attendance punches cannot be recorded until the leave is resolved.");
            return new AttendanceCalculation(0, 0, 0, 0, AttendanceValues.LeaveStatus);
        }

        if (isNonAttendanceStatus)
        {
            return new AttendanceCalculation(0, 0, 0, 0, status);
        }

        var schedule = await _calendar.GetScheduleAsync(attendanceDate, cancellationToken);
        var workingMinutes = checkIn.HasValue && checkOut.HasValue
            ? Math.Max(0, WholeMinutes(checkOut.Value - checkIn.Value) - schedule.BreakMinutes)
            : 0;
        var lateMinutes = 0;
        var earlyMinutes = 0;
        var overtimeMinutes = 0;

        if (schedule.IsWorkingDay && schedule.StartTime.HasValue && schedule.EndTime.HasValue)
        {
            var plannedStart = _calendar.ToInstant(attendanceDate, schedule.StartTime.Value, schedule.TimeZoneId);
            var endDate = schedule.EndTime.Value <= schedule.StartTime.Value ? attendanceDate.AddDays(1) : attendanceDate;
            var plannedEnd = _calendar.ToInstant(endDate, schedule.EndTime.Value, schedule.TimeZoneId);
            if (checkIn > plannedStart.AddMinutes(schedule.LateGraceMinutes))
                lateMinutes = CeilingMinutes(checkIn.Value - plannedStart.AddMinutes(schedule.LateGraceMinutes));
            if (checkOut < plannedEnd.Subtract(TimeSpan.FromMinutes(schedule.EarlyLeaveGraceMinutes)))
                earlyMinutes = CeilingMinutes(plannedEnd.Subtract(TimeSpan.FromMinutes(schedule.EarlyLeaveGraceMinutes)) - checkOut!.Value);
            if (checkOut > plannedEnd)
            {
                var candidate = WholeMinutes(checkOut.Value - plannedEnd);
                overtimeMinutes = candidate >= schedule.MinimumOvertimeMinutes ? candidate : 0;
            }
        }

        status = lateMinutes > 0 ? AttendanceValues.LateStatus : AttendanceValues.PresentStatus;
        return new AttendanceCalculation(workingMinutes, lateMinutes, earlyMinutes, overtimeMinutes, status);
    }

    private Task<bool> HasApprovedLeaveAsync(
        Guid employeeId,
        DateOnly attendanceDate,
        CancellationToken cancellationToken) =>
        _dbContext.LeaveRequests.AsNoTracking().AnyAsync(
            item => item.EmployeeId == employeeId &&
                    item.Status == LeaveRequestStatuses.Approved &&
                    item.StartDate <= attendanceDate &&
                    item.EndDate >= attendanceDate,
            cancellationToken);

    private async Task EnsureEmployeeEligibleForDateAsync(
        Guid employeeId,
        DateOnly attendanceDate,
        CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty || attendanceDate == default)
            throw new HrValidationException("A valid employee is required.");
        var employee = await _dbContext.Employees.AsNoTracking()
            .Where(item => item.Id == employeeId)
            .Select(item => new { item.HireDate, item.TerminationDate })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new HrValidationException("A valid employee is required.");
        if (employee.HireDate.HasValue && attendanceDate < employee.HireDate.Value)
            throw new HrValidationException("Attendance date cannot be before the employee hire date.");
        if (employee.TerminationDate.HasValue && attendanceDate > employee.TerminationDate.Value)
            throw new HrValidationException("Attendance date cannot be after the employee termination date.");

        var schedule = await _calendar.GetScheduleAsync(attendanceDate, cancellationToken);
        var companyToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId)).DateTime);
        if (attendanceDate > companyToday)
            throw new HrValidationException("Attendance cannot be recorded for a future date.");
    }

    private async Task EnsureDateAvailableAsync(
        Guid employeeId,
        DateOnly date,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        if (await _dbContext.AttendanceRecords.AnyAsync(
                item => item.EmployeeId == employeeId && item.AttendanceDate == date && !item.IsDeleted && item.Id != excludingId,
                cancellationToken))
            throw new HrConflictException("Attendance already exists for this employee and date.");
    }

    private void AddManualPunches(Guid attendanceRecordId, DateTimeOffset? checkIn, DateTimeOffset? checkOut, DateTimeOffset createdAt)
    {
        if (checkIn.HasValue)
            _dbContext.AttendancePunches.Add(new AttendancePunch(
                attendanceRecordId, checkIn.Value, AttendanceValues.CheckInPunch, AttendanceValues.ManualSource, null, null, null, createdAt));
        if (checkOut.HasValue)
            _dbContext.AttendancePunches.Add(new AttendancePunch(
                attendanceRecordId, checkOut.Value, AttendanceValues.CheckOutPunch, AttendanceValues.ManualSource, null, null, null, createdAt));
    }

    private static DateTimeOffset? ToUtc(DateTimeOffset? value) => value?.ToUniversalTime();

    private IQueryable<AttendanceProjection> QueryProjection(Guid id)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        return _dbContext.AttendanceRecords.AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(item => new AttendanceProjection(
                item.Id,
                item.EmployeeId,
                item.Employee.EmployeeNumber,
                isArabic ? item.Employee.FullNameArabic ?? item.Employee.FullName : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                item.Employee.DepartmentId,
                isArabic ? item.Employee.Department.NameArabic ?? item.Employee.Department.Name : item.Employee.Department.Name,
                item.Employee.BranchId,
                item.Employee.Branch == null ? null : isArabic ? item.Employee.Branch.NameArabic ?? item.Employee.Branch.Name : item.Employee.Branch.Name,
                item.AttendanceDate,
                item.CheckIn,
                item.CheckOut,
                item.WorkingMinutes,
                item.LateMinutes,
                item.EarlyLeaveMinutes,
                item.OvertimeMinutes,
                item.Status,
                item.Source,
                item.Notes,
                item.ImportBatchId,
                item.IsManuallyAdjusted,
                item.CreatedAt,
                item.UpdatedAt));
    }

    private static IQueryable<AttendanceRecord> ApplySort(
        IQueryable<AttendanceRecord> query,
        string? sortBy,
        bool descending)
    {
        var key = sortBy?.Trim().ToLowerInvariant();
        return (key, descending) switch
        {
            ("employeenumber", false) => query.OrderBy(item => item.Employee.EmployeeNumber).ThenBy(item => item.AttendanceDate),
            ("employeenumber", true) => query.OrderByDescending(item => item.Employee.EmployeeNumber).ThenByDescending(item => item.AttendanceDate),
            ("employeename", false) => query.OrderBy(item => item.Employee.FullName).ThenBy(item => item.AttendanceDate),
            ("employeename", true) => query.OrderByDescending(item => item.Employee.FullName).ThenByDescending(item => item.AttendanceDate),
            ("status", false) => query.OrderBy(item => item.Status).ThenByDescending(item => item.AttendanceDate),
            ("status", true) => query.OrderByDescending(item => item.Status).ThenByDescending(item => item.AttendanceDate),
            ("checkin", false) => query.OrderBy(item => item.CheckIn).ThenBy(item => item.AttendanceDate),
            ("checkin", true) => query.OrderByDescending(item => item.CheckIn).ThenByDescending(item => item.AttendanceDate),
            ("workinghours" or "workingminutes", false) => query.OrderBy(item => item.WorkingMinutes).ThenByDescending(item => item.AttendanceDate),
            ("workinghours" or "workingminutes", true) => query.OrderByDescending(item => item.WorkingMinutes).ThenByDescending(item => item.AttendanceDate),
            ("lateminutes", false) => query.OrderBy(item => item.LateMinutes).ThenByDescending(item => item.AttendanceDate),
            ("lateminutes", true) => query.OrderByDescending(item => item.LateMinutes).ThenByDescending(item => item.AttendanceDate),
            ("overtimeminutes", false) => query.OrderBy(item => item.OvertimeMinutes).ThenByDescending(item => item.AttendanceDate),
            ("overtimeminutes", true) => query.OrderByDescending(item => item.OvertimeMinutes).ThenByDescending(item => item.AttendanceDate),
            (null or "" or "attendancedate", false) => query.OrderBy(item => item.AttendanceDate).ThenBy(item => item.Employee.EmployeeNumber),
            (null or "" or "attendancedate", true) => query.OrderByDescending(item => item.AttendanceDate).ThenBy(item => item.Employee.EmployeeNumber),
            _ => throw new HrValidationException("Attendance sort field is invalid.")
        };
    }

    private static AttendanceListItemDto MapListItem(AttendanceProjection item) => new(
        item.Id, item.EmployeeId, item.EmployeeNumber, item.EmployeeName, item.DepartmentId, item.DepartmentName,
        item.BranchId, item.BranchName, item.AttendanceDate, item.CheckIn, item.CheckOut,
        ToHours(item.WorkingMinutes), item.LateMinutes, item.EarlyLeaveMinutes, item.OvertimeMinutes,
        item.Status, item.Source, item.IsManuallyAdjusted);

    private static AttendanceDetailsDto MapDetails(AttendanceProjection item) => new(
        item.Id, item.EmployeeId, item.EmployeeNumber, item.EmployeeName, item.DepartmentId, item.DepartmentName,
        item.BranchId, item.BranchName, item.AttendanceDate, item.CheckIn, item.CheckOut,
        ToHours(item.WorkingMinutes), item.LateMinutes, item.EarlyLeaveMinutes, item.OvertimeMinutes,
        item.Status, item.Source, item.Notes, item.ImportBatchId, item.IsManuallyAdjusted, item.CreatedAt, item.UpdatedAt);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 200) throw new HrValidationException("Invalid pagination values.");
    }

    private static void ValidateDateRange(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue && to < from) throw new HrValidationException("Date to cannot be before date from.");
    }

    private static int WholeMinutes(TimeSpan span) => Math.Max(0, (int)Math.Floor(span.TotalMinutes));
    private static int CeilingMinutes(TimeSpan span) => Math.Max(0, (int)Math.Ceiling(span.TotalMinutes));
    private static decimal ToHours(int minutes) => decimal.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
    private static int Pages(int count, int size) => count == 0 ? 0 : (int)Math.Ceiling(count / (double)size);

    private sealed record AttendanceCalculation(int WorkingMinutes, int LateMinutes, int EarlyLeaveMinutes, int OvertimeMinutes, string Status);

    private sealed record AttendanceProjection(
        Guid Id,
        Guid EmployeeId,
        string EmployeeNumber,
        string EmployeeName,
        Guid DepartmentId,
        string DepartmentName,
        Guid? BranchId,
        string? BranchName,
        DateOnly AttendanceDate,
        DateTimeOffset? CheckIn,
        DateTimeOffset? CheckOut,
        int WorkingMinutes,
        int LateMinutes,
        int EarlyLeaveMinutes,
        int OvertimeMinutes,
        string Status,
        string Source,
        string? Notes,
        Guid? ImportBatchId,
        bool IsManuallyAdjusted,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);
}
