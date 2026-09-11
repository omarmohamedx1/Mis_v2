using System.ComponentModel.DataAnnotations;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Services;

public sealed class EmployeeCreationService(IHrEmployeeRepository repository, IHrTransactionRunner transactions, IHrAuditService audit) : IEmployeeCreationService
{
    public async Task ValidateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new HrValidationException("Employee validation failed.", errors.Select(error => error.ErrorMessage!).ToArray());
        if (request.DepartmentId == Guid.Empty || !await repository.DepartmentExistsAsync(request.DepartmentId, cancellationToken))
            throw new HrValidationException("A valid department is required.");
        if (await repository.EmployeeNumberExistsAsync(request.EmployeeNumber, null, cancellationToken))
            throw new HrConflictException("An employee with this employee ID already exists.");
        if (await repository.NationalIdExistsAsync(request.NationalId, null, cancellationToken))
            throw new HrConflictException("An employee with this National ID already exists.");
        if (!request.PositionId.HasValue || !await repository.PositionExistsAsync(request.PositionId.Value, cancellationToken))
            throw new HrValidationException("A valid position is required.");
        if (request.OperationalRole?.Trim().ToUpperInvariant() is not ("COLLECTOR" or "ADMIN" or "SUPERVISOR"))
            throw new HrValidationException("Employee role must be COLLECTOR, ADMIN, or SUPERVISOR.");
        if (!request.WorkStartDate.HasValue) throw new HrValidationException("Work start date is required.");
        try { BuildEmployee(request); }
        catch (ArgumentException error) { throw new HrValidationException(error.Message); }
    }

    internal static Employee BuildEmployee(SaveEmployeeRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var employee = new Employee(request.EmployeeNumber, request.FullName, request.DepartmentId, request.IsActive, now);
        employee.SetNationalId(request.NationalId, now);
        if (request.Status is not null)
            employee.ChangeStatus(request.Status, request.Status == Employee.ActiveStatus, request.WorkEndDate, null, now);
        employee.ApplyEmployeeProfile(request.PositionId!.Value, request.OperationalRole!, request.WorkStartDate!.Value,
            request.FingerprintEnrollmentDate, request.DateOfBirth, request.Address, request.WorkEndDate, now);
        if (request.Gender is not null)
            employee.UpdatePersonalInformation(null, null, request.NationalId, request.DateOfBirth, request.Gender, null, null, now);
        return employee;
    }

    public async Task<EmployeeDetailsDto> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request, cancellationToken);
        var employee = BuildEmployee(request);
        return await transactions.ExecuteAsync(async token =>
        {
            repository.Add(employee);
            await repository.SaveChangesAsync(token);
            var details = await repository.GetDetailsByIdAsync(employee.Id, token)
                ?? throw new InvalidOperationException("The created employee could not be reloaded.");
            await audit.WriteAsync(new AuditWriteRequest("EmployeeCreated", nameof(Employee), employee.Id.ToString(),
                employee.Id, null, details, $"Created employee {request.EmployeeNumber}."), token);
            return details;
        }, cancellationToken);
    }
}
