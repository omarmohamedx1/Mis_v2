using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;

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
        [FromQuery] Guid? departmentId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? archived = false,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiErrorResponse.Failure("Page must be at least 1 and pageSize must be between 1 and 100."));
        if (search?.Length > 160)
            return BadRequest(ApiErrorResponse.Failure("Search cannot exceed 160 characters."));

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
        if (!string.IsNullOrWhiteSpace(role) && operationalRole is null) return BadRequest(ApiErrorResponse.Failure("Role must be COLLECTOR, ADMIN, or SUPERVISOR."));
        return Ok(await _repository.GetPagedByStatusAsync(page, pageSize, search, departmentId, employeeStatus, operationalRole, archived, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailsDto>> GetEmployee(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _repository.GetDetailsByIdAsync(id, cancellationToken);
        return employee is null ? NotFound(ApiErrorResponse.Failure("Employee was not found.")) : Ok(employee);
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
        var error = await ValidateRequestAsync(request, id, cancellationToken);
        if (error is not null) return error;

        employee.Update(request.EmployeeNumber, request.FullName, request.DepartmentId, request.IsActive, DateTimeOffset.UtcNow);
        employee.SetNationalId(request.NationalId, DateTimeOffset.UtcNow);
        employee.ApplyEmployeeProfile(request.PositionId!.Value, request.OperationalRole!, request.WorkStartDate!.Value,
            request.FingerprintEnrollmentDate, request.DateOfBirth, request.Address, request.WorkEndDate, DateTimeOffset.UtcNow);
        employee.UpdateContactInformation(EmployeeMobileNumber.Normalize(request.MobileNumber), employee.AlternativeMobileNumber, employee.Email, employee.Address, employee.City, DateTimeOffset.UtcNow);
        var updated = await _transactions.ExecuteAsync(async token =>
        {
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
                $"Updated employee {request.EmployeeNumber}."), token);
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

    private async Task<ActionResult?> ValidateRequestAsync(SaveEmployeeRequest request, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (request.DepartmentId == Guid.Empty || !await _repository.DepartmentExistsAsync(request.DepartmentId, cancellationToken))
            return BadRequest(ApiErrorResponse.Failure("A valid department is required."));
        if (await _repository.EmployeeNumberExistsAsync(request.EmployeeNumber, excludingId, cancellationToken))
            return Conflict(ApiErrorResponse.Failure("An employee with this employee ID already exists."));
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.NationalId ?? string.Empty, "^[0-9]{14}$"))
            return BadRequest(ApiErrorResponse.Failure("National ID must contain exactly 14 digits."));
        if (await _repository.NationalIdExistsAsync(request.NationalId!, excludingId, cancellationToken))
            return Conflict(ApiErrorResponse.Failure("An employee with this National ID already exists."));
        if (!request.PositionId.HasValue || request.PositionId == Guid.Empty || !await _repository.PositionExistsAsync(request.PositionId.Value, cancellationToken))
            return BadRequest(ApiErrorResponse.Failure("A valid position is required."));
        if (NormalizeOperationalRole(request.OperationalRole) is null)
            return BadRequest(ApiErrorResponse.Failure("Employee role must be COLLECTOR, ADMIN, or SUPERVISOR."));
        if (!request.WorkStartDate.HasValue)
            return BadRequest(ApiErrorResponse.Failure("Work start date is required."));
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(ApiErrorResponse.Failure("Date of birth cannot be in the future."));
        if (request.WorkEndDate.HasValue && request.WorkEndDate < request.WorkStartDate)
            return BadRequest(ApiErrorResponse.Failure("Work end date cannot be before work start date."));
        return null;
    }

    private static string? NormalizeOperationalRole(string? value) => value?.Trim().ToUpperInvariant() is "COLLECTOR" or "ADMIN" or "SUPERVISOR" ? value.Trim().ToUpperInvariant() : null;
}
