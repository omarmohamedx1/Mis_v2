using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/employees")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrEmployeesController : ControllerBase
{
    private readonly IHrEmployeeRepository _repository;
    private readonly IEmployeeCreationService _creation;
    private readonly IHrAuditService _audit;
    private readonly IHrTransactionRunner _transactions;
    private readonly ICurrentUserContext _currentUser;

    public HrEmployeesController(
        IHrEmployeeRepository repository, IEmployeeCreationService creation,
        IHrAuditService audit,
        IHrTransactionRunner transactions,
        ICurrentUserContext currentUser)
    {
        _repository = repository; _creation = creation;
        _audit = audit;
        _transactions = transactions;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<PagedEmployeesDto>> GetEmployees(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? searchField = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? role = null,
        [FromQuery] string? gender = null,
        [FromQuery] Guid? positionId = null,
        [FromQuery] bool? archived = false,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiErrorResponse.Failure("Page must be at least 1 and pageSize must be between 1 and 100."));
        if (search?.Length > 160)
            return BadRequest(ApiErrorResponse.Failure("Search cannot exceed 160 characters."));
        var normalizedSearchField = searchField?.Trim().ToLowerInvariant() ?? "identity";
        if (normalizedSearchField is not ("identity" or "employeenumber" or "name" or "nationalid" or "mobile" or "organization" or "position"))
            return BadRequest(ApiErrorResponse.Failure("Search field is not supported."));

        var normalizedStatus = status?.Trim().ToLowerInvariant();
        var employeeStatus = normalizedStatus switch
        {
            null or "" or "all" => null,
            "active" => Employee.ActiveStatus,
            "inactive" => Employee.InactiveStatus,
            "onleave" or "on_leave" or "on leave" => Employee.OnLeaveStatus,
            "suspended" => Employee.SuspendedStatus,
            "terminated" => Employee.TerminatedStatus,
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(normalizedStatus) && normalizedStatus is not ("all" or "active" or "inactive" or "onleave" or "on_leave" or "on leave" or "suspended" or "terminated"))
            return BadRequest(ApiErrorResponse.Failure("Status must be all, active, inactive, on leave, suspended, or terminated."));

        var operationalRole = NormalizeOperationalRole(role);
        if (!string.IsNullOrWhiteSpace(role) && operationalRole is null) return BadRequest(ApiErrorResponse.Failure("Role must be COLLECTOR, ADMIN, SUPERVISOR, or OFFICE."));
        var normalizedGender = NormalizeGender(gender);
        if (!string.IsNullOrWhiteSpace(gender) && normalizedGender is null) return BadRequest(ApiErrorResponse.Failure("Gender must be Male or Female."));
        return Ok(await _repository.GetPagedByStatusAsync(page, pageSize, search, normalizedSearchField, departmentId, employeeStatus, operationalRole, archived, organizationId, normalizedGender, positionId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailsDto>> GetEmployee(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _repository.GetDetailsByIdAsync(id, cancellationToken);
        return employee is null ? NotFound(ApiErrorResponse.Failure("Employee was not found.")) : Ok(employee);
    }

    [HttpGet("organizations")]
    public async Task<ActionResult<IReadOnlyCollection<EmployeeOrganizationAssignmentDto>>> GetOrganizations(CancellationToken cancellationToken) =>
        Ok(await _repository.GetOrganizationsAsync(cancellationToken));

    [HttpGet("identity-availability")]
    public async Task<ActionResult<EmployeeIdentityAvailabilityDto>> GetIdentityAvailability(
        [FromQuery] string? employeeNumber,
        [FromQuery] string? nationalId,
        [FromQuery] Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var employeeNumberTaken = !string.IsNullOrWhiteSpace(employeeNumber)
            && await _repository.EmployeeNumberExistsAsync(employeeNumber, excludingId, cancellationToken);
        var nationalIdTaken = false;
        if (!string.IsNullOrWhiteSpace(nationalId)
            && EgyptianHrDataValidator.TryParseNationalId(nationalId, out var parsed, out _))
            nationalIdTaken = await _repository.NationalIdExistsAsync(parsed.NationalId, excludingId, cancellationToken);
        return Ok(new EmployeeIdentityAvailabilityDto(employeeNumberTaken, nationalIdTaken));
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDetailsDto>> CreateEmployee(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var created = await _creation.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetEmployee), new { id = created.Id }, created);
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailsDto>> UpdateEmployee(Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await _repository.GetTrackedByIdAsync(id, cancellationToken);
        if (employee is null) return NotFound(ApiErrorResponse.Failure("Employee was not found."));
        var oldValue = await _repository.GetDetailsByIdAsync(id, cancellationToken);
        var normalized = EmployeeSaveIdentity.Normalize(request);
        var error = await ValidateRequestAsync(normalized, id, cancellationToken);
        if (error is not null) return error;

        employee.Update(normalized.EmployeeNumber, normalized.FullName, normalized.DepartmentId, normalized.IsActive, DateTimeOffset.UtcNow);
        employee.UpdateLocalizedNames(
            normalized.FullNameArabic ?? employee.FullNameArabic,
            normalized.FullNameEnglish ?? employee.FullNameEnglish,
            DateTimeOffset.UtcNow);
        employee.SetNationalId(normalized.NationalId, DateTimeOffset.UtcNow);
        var position = await _repository.GetPositionAsync(normalized.PositionId!.Value, cancellationToken)
            ?? throw new InvalidOperationException("The selected position could not be loaded.");
        EmployeeOperationalRoles.TryResolve(normalized.OperationalRole, position.Code, position.Name, position.NameArabic, out var operationalRole);
        employee.ApplyEmployeeProfile(normalized.PositionId.Value, operationalRole, normalized.WorkStartDate!.Value,
            normalized.FingerprintEnrollmentDate, normalized.DateOfBirth, normalized.Address, normalized.WorkEndDate, DateTimeOffset.UtcNow);
        employee.UpdateContactInformation(normalized.MobileNumber, employee.AlternativeMobileNumber, employee.Email, employee.Address, employee.City, DateTimeOffset.UtcNow);
        if (normalized.Gender is not null)
            employee.UpdatePersonalInformation(employee.FullNameArabic, employee.FullNameEnglish, normalized.NationalId, normalized.DateOfBirth, normalized.Gender, employee.MaritalStatus, employee.ProfilePhotoStorageKey, DateTimeOffset.UtcNow);
        employee.UpdateWorkAssignment(normalized.WorkNumber, normalized.PackageType, DateTimeOffset.UtcNow);
        var updated = await _transactions.ExecuteAsync(async token =>
        {
            await _repository.ReplaceOrganizationAssignmentsAsync(id, normalized.OrganizationIds, token);
            if (normalized.BasicSalary.HasValue)
            {
                await _repository.UpsertCurrentSalaryAsync(
                    id,
                    normalized.BasicSalary.Value,
                    normalized.Allowances ?? 0,
                    normalized.WorkStartDate!.Value,
                    token);
            }
            await _repository.SaveChangesAsync(token);
            var details = await _repository.GetDetailsByIdAsync(id, token)
                ?? throw new InvalidOperationException("The updated employee could not be reloaded.");
            await _audit.WriteAsync(new AuditWriteRequest(
                "EmployeeUpdated",
                nameof(Employee),
                id.ToString(),
                id,
                oldValue,
                details,
                $"Updated employee {normalized.EmployeeNumber}."), token);
            return details;
        }, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<EmployeeDetailsDto>> Archive(Guid id, ArchiveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await _repository.GetTrackedByIdAsync(id, cancellationToken);
        if (employee is null) return NotFound(ApiErrorResponse.Failure("Employee was not found."));
        var before = await _repository.GetDetailsByIdAsync(id, cancellationToken);
        try { employee.Archive(request.Reason, _currentUser.UserId, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { return Conflict(ApiErrorResponse.Failure(ex.Message)); }
        return Ok(await _transactions.ExecuteAsync(async token => { await _repository.SaveChangesAsync(token); var details = await _repository.GetDetailsByIdAsync(id, token) ?? throw new InvalidOperationException(); await _audit.WriteAsync(new AuditWriteRequest("EmployeeArchived", nameof(Employee), id.ToString(), id, before, details, $"Archived employee {employee.EmployeeNumber}."), token); return details; }, cancellationToken));
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<EmployeeDetailsDto>> Restore(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _repository.GetTrackedByIdAsync(id, cancellationToken);
        if (employee is null) return NotFound(ApiErrorResponse.Failure("Employee was not found."));
        var before = await _repository.GetDetailsByIdAsync(id, cancellationToken);
        try { employee.Restore(DateTimeOffset.UtcNow); }
        catch (InvalidOperationException ex) { return Conflict(ApiErrorResponse.Failure(ex.Message)); }
        return Ok(await _transactions.ExecuteAsync(async token => { await _repository.SaveChangesAsync(token); var details = await _repository.GetDetailsByIdAsync(id, token) ?? throw new InvalidOperationException(); await _audit.WriteAsync(new AuditWriteRequest("EmployeeRestored", nameof(Employee), id.ToString(), id, before, details, $"Restored employee {employee.EmployeeNumber}."), token); return details; }, cancellationToken));
    }

    [HttpPost("remove")]
    public async Task<ActionResult<RemoveEmployeesResultDto>> Remove(RemoveEmployeesRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length is < 1 or > 200)
            return BadRequest(ApiErrorResponse.Failure("Select between 1 and 200 employees."));
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (request.KeepData && reason.Length < 2)
            return BadRequest(ApiErrorResponse.Failure("Archive reason is required when keeping employee data."));
        if (!request.KeepData && !IsAdmin())
            throw new HrForbiddenException("Only administrators can permanently delete employees.");

        var kept = 0;
        var deleted = 0;
        var skipped = 0;
        if (request.KeepData)
        {
            await _transactions.ExecuteAsync(async token =>
            {
                foreach (var id in ids)
                {
                    var employee = await _repository.GetTrackedByIdAsync(id, token);
                    if (employee is null || employee.IsArchived)
                    {
                        skipped++;
                        continue;
                    }
                    var before = await _repository.GetDetailsByIdAsync(id, token);
                    employee.Archive(reason, _currentUser.UserId, DateTimeOffset.UtcNow);
                    await _repository.SaveChangesAsync(token);
                    var details = await _repository.GetDetailsByIdAsync(id, token)
                        ?? throw new InvalidOperationException("The archived employee could not be reloaded.");
                    await _audit.WriteAsync(new AuditWriteRequest("EmployeeArchived", nameof(Employee), id.ToString(), id, before, details,
                        $"Archived employee {employee.EmployeeNumber}."), token);
                    kept++;
                }
            }, cancellationToken);
        }
        else
        {
            foreach (var id in ids)
            {
                var before = await _repository.GetDetailsByIdAsync(id, cancellationToken);
                if (before is null)
                {
                    skipped++;
                    continue;
                }
                await _transactions.ExecuteAsync(async token =>
                {
                    await _repository.DeletePermanentlyAsync(id, token);
                    await _audit.WriteAsync(new AuditWriteRequest(
                        "EmployeeDeleted",
                        nameof(Employee),
                        id.ToString(),
                        null,
                        before,
                        null,
                        $"Permanently deleted employee {before.EmployeeNumber}."), token);
                }, cancellationToken);
                deleted++;
            }
        }

        return Ok(new RemoveEmployeesResultDto(kept, deleted, skipped));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
            throw new HrForbiddenException("Only administrators can permanently delete employees.");
        var before = await _repository.GetDetailsByIdAsync(id, cancellationToken);
        if (before is null) return NotFound(ApiErrorResponse.Failure("Employee was not found."));
        await _transactions.ExecuteAsync(async token =>
        {
            await _repository.DeletePermanentlyAsync(id, token);
            await _audit.WriteAsync(new AuditWriteRequest(
                "EmployeeDeleted",
                nameof(Employee),
                id.ToString(),
                null,
                before,
                null,
                $"Permanently deleted employee {before.EmployeeNumber}."), token);
        }, cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateRequestAsync(SaveEmployeeRequest request, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (request.DepartmentId == Guid.Empty || !await _repository.DepartmentExistsAsync(request.DepartmentId, cancellationToken))
            return BadRequest(ApiErrorResponse.Failure("A valid department is required."));
        if (await _repository.EmployeeNumberExistsAsync(request.EmployeeNumber, excludingId, cancellationToken))
            return Conflict(ApiErrorResponse.Failure("An employee with this employee ID already exists."));
        if (await _repository.NationalIdExistsAsync(request.NationalId, excludingId, cancellationToken))
            return Conflict(ApiErrorResponse.Failure("An employee with this National ID already exists."));
        if (!request.PositionId.HasValue || request.PositionId == Guid.Empty || !await _repository.PositionExistsAsync(request.PositionId.Value, cancellationToken))
            return BadRequest(ApiErrorResponse.Failure("A valid position is required."));
        var position = await _repository.GetPositionAsync(request.PositionId.Value, cancellationToken);
        if (position is null || !EmployeeOperationalRoles.TryResolve(request.OperationalRole, position.Code, position.Name, position.NameArabic, out _))
            return BadRequest(ApiErrorResponse.Failure("Employee role must be COLLECTOR, ADMIN, SUPERVISOR, or OFFICE."));
        if (!request.WorkStartDate.HasValue)
            return BadRequest(ApiErrorResponse.Failure("Work start date is required."));
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(ApiErrorResponse.Failure("Date of birth cannot be in the future."));
        if (request.WorkEndDate.HasValue && request.WorkEndDate < request.WorkStartDate)
            return BadRequest(ApiErrorResponse.Failure("Work end date cannot be before work start date."));
        if (!await _repository.OrganizationsExistAsync(request.OrganizationIds, cancellationToken))
            return BadRequest(ApiErrorResponse.Failure("One or more assigned organizations are invalid or inactive."));
        return null;
    }

    private bool IsAdmin() =>
        _currentUser.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase)
        || _currentUser.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase);

    private static string? NormalizeOperationalRole(string? value) => value?.Trim().ToUpperInvariant() is "COLLECTOR" or "ADMIN" or "SUPERVISOR" or "OFFICE" ? value.Trim().ToUpperInvariant() : null;

    private static string? NormalizeGender(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "male" or "m" or "boy" or "boys" or "ذكر" or "ولاد" or "ولد" => "Male",
        "female" or "f" or "girl" or "girls" or "أنثى" or "انثى" or "بنات" or "بنت" => "Female",
        _ => null
    };
}
