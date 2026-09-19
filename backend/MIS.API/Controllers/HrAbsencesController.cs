using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/absences")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrAbsencesController : ControllerBase
{
    private const decimal PayrollMonthDivisor = 30m;
    private readonly IHrAbsenceRepository _repository;
    private readonly IHrAuditService _audit;
    private readonly IHrTransactionRunner _transactions;
    private readonly ICurrentUserContext _currentUser;

    public HrAbsencesController(
        IHrAbsenceRepository repository,
        IHrAuditService audit,
        IHrTransactionRunner transactions,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _audit = audit;
        _transactions = transactions;
        _currentUser = currentUser;
    }

    [HttpGet]
        public async Task<ActionResult<PagedAbsencesDto>> GetAbsences([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? departmentId = null, [FromQuery] Guid? organizationId = null, [FromQuery] Guid? employeeId = null, [FromQuery] DateOnly? date = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest(ApiErrorResponse.Failure("Page must be at least 1 and pageSize must be between 1 and 100."));
        if (search?.Length > 160) return BadRequest(ApiErrorResponse.Failure("Search cannot exceed 160 characters."));
        var normalizedStatus = string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : NormalizeStatus(status);
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase) && normalizedStatus is null) return BadRequest(ApiErrorResponse.Failure("Status must be all, pending, excused, or unexcused."));
        var result = await _repository.GetPagedAsync(page, pageSize, search, departmentId, organizationId, employeeId, date, normalizedStatus, cancellationToken);
        return Ok(CanManagePayrollImpact ? result : result with
        {
            Items = result.Items.Select(item => item with
            {
                SuggestedDeductionAmount = 0,
                ApprovedDeductionAmount = null
            }).ToArray()
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AbsenceDetailsDto>> GetAbsence(Guid id, CancellationToken cancellationToken)
    {
        var absence = await _repository.GetDetailsAsync(id, cancellationToken);
        return absence is null ? NotFound(ApiErrorResponse.Failure("Absence record was not found.")) : Ok(ProtectPayrollDetails(absence));
    }

    [HttpPost]
    public async Task<ActionResult<AbsenceDetailsDto>> CreateAbsence(SaveAbsenceRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, null, cancellationToken); if (validation is not null) return validation;
        var absence = new EmployeeAbsence(request.EmployeeId, request.AbsenceDate, request.Reason, NormalizeStatus(request.Status)!, request.Notes, DateTimeOffset.UtcNow);
        await SynchronizePayrollImpactAsync(absence, cancellationToken);
        var created = await _transactions.ExecuteAsync(async token =>
        {
            _repository.Add(absence);
            await _repository.SaveChangesAsync(token);
            var details = await _repository.GetDetailsAsync(absence.Id, token)
                ?? throw new InvalidOperationException("The created absence could not be reloaded.");
            await _audit.WriteAsync(new AuditWriteRequest(
                "AbsenceCreated",
                nameof(EmployeeAbsence),
                absence.Id.ToString(),
                request.EmployeeId,
                null,
                details,
                "Recorded employee absence."), token);
            return details;
        }, cancellationToken);
        return CreatedAtAction(nameof(GetAbsence), new { id = absence.Id }, ProtectPayrollDetails(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AbsenceDetailsDto>> UpdateAbsence(Guid id, SaveAbsenceRequest request, CancellationToken cancellationToken)
    {
        var absence = await _repository.GetTrackedAsync(id, cancellationToken);
        if (absence is null) return NotFound(ApiErrorResponse.Failure("Absence record was not found."));
        var oldValue = await _repository.GetDetailsAsync(id, cancellationToken);
        var validation = await ValidateAsync(request, id, cancellationToken); if (validation is not null) return validation;
        if (absence.PayrollImpactStatus == AbsenceValues.PayrollApproved &&
            (absence.EmployeeId != request.EmployeeId || absence.AbsenceDate != request.AbsenceDate || absence.Status != NormalizeStatus(request.Status)))
            return Conflict(ApiErrorResponse.Failure("Reverse the approved payroll deduction before changing the employee, date, or absence status."));
        absence.Update(request.EmployeeId, request.AbsenceDate, request.Reason, NormalizeStatus(request.Status)!, request.Notes, DateTimeOffset.UtcNow);
        await SynchronizePayrollImpactAsync(absence, cancellationToken);
        var updated = await _transactions.ExecuteAsync(async token =>
        {
            await _repository.SaveChangesAsync(token);
            var details = await _repository.GetDetailsAsync(id, token)
                ?? throw new InvalidOperationException("The updated absence could not be reloaded.");
            await _audit.WriteAsync(new AuditWriteRequest(
                "AbsenceUpdated",
                nameof(EmployeeAbsence),
                id.ToString(),
                request.EmployeeId,
                oldValue,
                details,
                "Updated employee absence."), token);
            return details;
        }, cancellationToken);
        return Ok(ProtectPayrollDetails(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAbsence(Guid id, CancellationToken cancellationToken)
    {
        var absence = await _repository.GetTrackedAsync(id, cancellationToken);
        if (absence is null) return NotFound(ApiErrorResponse.Failure("Absence record was not found."));
        if (absence.PayrollImpactStatus == AbsenceValues.PayrollApproved)
            return Conflict(ApiErrorResponse.Failure("An absence with an approved payroll deduction cannot be deleted. Exclude its payroll impact first."));
        var oldValue = await _repository.GetDetailsAsync(id, cancellationToken);
        await _transactions.ExecuteAsync(async token =>
        {
            _repository.Remove(absence);
            await _repository.SaveChangesAsync(token);
            await _audit.WriteAsync(new AuditWriteRequest(
                "AbsenceDeleted",
                nameof(EmployeeAbsence),
                id.ToString(),
                absence.EmployeeId,
                oldValue,
                null,
                "Deleted employee absence."), token);
        }, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/payroll-impact")]
    [Authorize(Policy = AuthorizationPolicies.HrSensitiveData)]
    public async Task<ActionResult<AbsenceDetailsDto>> ReviewPayrollImpact(Guid id, ReviewAbsencePayrollImpactRequest request, CancellationToken cancellationToken)
    {
        var absence = await _repository.GetTrackedAsync(id, cancellationToken);
        if (absence is null) return NotFound(ApiErrorResponse.Failure("Absence record was not found."));
        var approve = request.Decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var exclude = request.Decision.Equals("Exclude", StringComparison.OrdinalIgnoreCase);
        if (!approve && !exclude) return BadRequest(ApiErrorResponse.Failure("Decision must be Approve or Exclude."));
        if (approve && !request.ApprovedDeductionAmount.HasValue)
            return BadRequest(ApiErrorResponse.Failure("Approved deduction amount is required."));
        if (absence.Status != AbsenceValues.UnexcusedStatus)
            return Conflict(ApiErrorResponse.Failure("Only an unexcused absence can affect payroll."));

        var oldValue = await _repository.GetDetailsAsync(id, cancellationToken);
        absence.ReviewPayrollImpact(approve, request.ApprovedDeductionAmount, request.Notes, _currentUser.UserId, DateTimeOffset.UtcNow);
        var updated = await _transactions.ExecuteAsync(async token =>
        {
            await _repository.SaveChangesAsync(token);
            var details = await _repository.GetDetailsAsync(id, token)
                ?? throw new InvalidOperationException("The reviewed absence could not be reloaded.");
            await _audit.WriteAsync(new AuditWriteRequest(
                approve ? "AbsencePayrollDeductionApproved" : "AbsencePayrollDeductionExcluded",
                nameof(EmployeeAbsence), id.ToString(), absence.EmployeeId, oldValue, details,
                approve ? "Approved the absence payroll deduction." : "Excluded the absence from payroll deductions."), token);
            return details;
        }, cancellationToken);
        return Ok(updated);
    }

    private async Task SynchronizePayrollImpactAsync(EmployeeAbsence absence, CancellationToken cancellationToken)
    {
        decimal? suggested = null;
        if (absence.Status == AbsenceValues.UnexcusedStatus)
        {
            var basicSalary = await _repository.GetBasicSalaryOnDateAsync(absence.EmployeeId, absence.AbsenceDate, cancellationToken);
            suggested = basicSalary / PayrollMonthDivisor;
        }
        absence.SynchronizePayrollImpact(suggested, DateTimeOffset.UtcNow);
    }

    private bool CanManagePayrollImpact => _currentUser.Roles.Contains(SystemRoleNames.HrManager, StringComparer.OrdinalIgnoreCase);

    private AbsenceDetailsDto ProtectPayrollDetails(AbsenceDetailsDto value) => CanManagePayrollImpact ? value : value with
    {
        SuggestedDeductionAmount = 0,
        ApprovedDeductionAmount = null,
        PayrollNotes = null,
        PayrollReviewedByUsername = null
    };

    private async Task<ActionResult?> ValidateAsync(SaveAbsenceRequest request, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (request.EmployeeId == Guid.Empty || !await _repository.EmployeeExistsAsync(request.EmployeeId, cancellationToken)) return BadRequest(ApiErrorResponse.Failure("A valid employee is required."));
        if (request.AbsenceDate == default) return BadRequest(ApiErrorResponse.Failure("A valid absence date is required."));
        var cairoToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Africa/Cairo").DateTime);
        if (request.AbsenceDate > cairoToday) return BadRequest(ApiErrorResponse.Failure("Absence cannot be recorded for a future date."));
        if (!await _repository.EmployeeEligibleOnDateAsync(request.EmployeeId, request.AbsenceDate, cancellationToken)) return BadRequest(ApiErrorResponse.Failure("Absence date must fall within the employee employment period."));
        if (await _repository.AbsenceExistsAsync(request.EmployeeId, request.AbsenceDate, excludingId, cancellationToken)) return Conflict(ApiErrorResponse.Failure("An absence case already exists for this employee and date."));
        if (await _repository.HasApprovedLeaveAsync(request.EmployeeId, request.AbsenceDate, cancellationToken)) return Conflict(ApiErrorResponse.Failure("An absence case cannot be recorded on an approved leave date."));
        if (await _repository.HasConflictingAttendanceAsync(request.EmployeeId, request.AbsenceDate, cancellationToken)) return Conflict(ApiErrorResponse.Failure("Recorded attendance conflicts with this absence date. Resolve the attendance record first."));
        if (!string.Equals(request.Type, AbsenceValues.AbsentType, StringComparison.OrdinalIgnoreCase)) return BadRequest(ApiErrorResponse.Failure("Type must be Absent for V1."));
        if (!string.Equals(request.AttendanceSource, AbsenceValues.ManualSource, StringComparison.OrdinalIgnoreCase)) return BadRequest(ApiErrorResponse.Failure("Attendance source must be Manual for V1."));
        if (NormalizeStatus(request.Status) is null) return BadRequest(ApiErrorResponse.Failure("Status must be Pending, Excused, or Unexcused."));
        return null;
    }

    private static string? NormalizeStatus(string? value) => value?.Trim().ToLowerInvariant() switch { "pending" => AbsenceValues.PendingStatus, "excused" => AbsenceValues.ExcusedStatus, "unexcused" => AbsenceValues.UnexcusedStatus, _ => null };
}
