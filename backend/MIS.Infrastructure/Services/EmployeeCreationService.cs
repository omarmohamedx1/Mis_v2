using System.ComponentModel.DataAnnotations;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Domain.Hr;

namespace MIS.Infrastructure.Services;

public sealed class EmployeeCreationService(IHrEmployeeRepository repository, IHrTransactionRunner transactions, IHrAuditService audit) : IEmployeeCreationService
{
    public Task ValidateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken) =>
        ValidateCoreAsync(EmployeeSaveIdentity.Normalize(request), cancellationToken);

    private async Task ValidateCoreAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new HrValidationException(errors[0].ErrorMessage ?? "Employee validation failed.", errors.Select(error => error.ErrorMessage!).ToArray());
        if (request.DepartmentId == Guid.Empty || !await repository.DepartmentExistsAsync(request.DepartmentId, cancellationToken))
            throw new HrValidationException("A valid department is required.");
        if (await repository.EmployeeNumberExistsAsync(request.EmployeeNumber, null, cancellationToken))
            throw new HrConflictException("An employee with this employee ID already exists.");
        if (await repository.NationalIdExistsAsync(request.NationalId, null, cancellationToken))
            throw new HrConflictException("An employee with this National ID already exists.");
        if (!request.PositionId.HasValue || !await repository.PositionExistsAsync(request.PositionId.Value, cancellationToken))
            throw new HrValidationException("A valid position is required.");
        var position = await repository.GetPositionAsync(request.PositionId.Value, cancellationToken)
            ?? throw new HrValidationException("A valid position is required.");
        if (!EmployeeOperationalRoles.TryResolve(request.OperationalRole, position.Code, position.Name, position.NameArabic, out var operationalRole))
            throw new HrValidationException("Employee role must be COLLECTOR, ADMIN, SUPERVISOR, or OFFICE.");
        if (!request.WorkStartDate.HasValue) throw new HrValidationException("Work start date is required.");
        if (request.BasicSalary < 0 || request.Allowances < 0)
            throw new HrValidationException("Salary and allowances cannot be negative.");
        if (!await repository.OrganizationsExistAsync(request.OrganizationIds, cancellationToken))
            throw new HrValidationException("One or more assigned organizations are invalid or inactive.");
        try { BuildEmployee(request, operationalRole); }
        catch (ArgumentException error) { throw new HrValidationException(error.Message); }
    }

    internal static Employee BuildEmployee(SaveEmployeeRequest request, string operationalRole)
    {
        var now = DateTimeOffset.UtcNow;
        var employee = new Employee(request.EmployeeNumber, request.FullName, request.DepartmentId, request.IsActive, now);
        employee.UpdateLocalizedNames(
            request.FullNameArabic ?? employee.FullNameArabic,
            request.FullNameEnglish ?? employee.FullNameEnglish,
            now);
        employee.SetNationalId(request.NationalId, now);
        if (request.Status is not null)
            employee.ChangeStatus(request.Status, request.Status == Employee.ActiveStatus, request.WorkEndDate, null, now);
        employee.ApplyEmployeeProfile(request.PositionId!.Value, operationalRole, request.WorkStartDate!.Value,
            request.FingerprintEnrollmentDate, request.DateOfBirth, request.Address, request.WorkEndDate, now);
        if (request.Gender is not null)
            employee.UpdatePersonalInformation(employee.FullNameArabic, employee.FullNameEnglish, request.NationalId, request.DateOfBirth, request.Gender, null, null, now);
        employee.UpdateContactInformation(request.MobileNumber, null, null, employee.Address, null, now);
        employee.UpdateWorkAssignment(request.WorkNumber, request.PackageType, now);
        return employee;
    }

    public async Task<EmployeeDetailsDto> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var normalized = EmployeeSaveIdentity.Normalize(request);
        await ValidateCoreAsync(normalized, cancellationToken);
        var position = await repository.GetPositionAsync(normalized.PositionId!.Value, cancellationToken)
            ?? throw new HrValidationException("A valid position is required.");
        EmployeeOperationalRoles.TryResolve(normalized.OperationalRole, position.Code, position.Name, position.NameArabic, out var operationalRole);
        var employee = BuildEmployee(normalized, operationalRole);
        return await transactions.ExecuteAsync(async token =>
        {
            repository.Add(employee);
            if (normalized.BasicSalary.HasValue)
            {
                await repository.UpsertCurrentSalaryAsync(
                    employee.Id,
                    normalized.BasicSalary.Value,
                    normalized.Allowances ?? 0,
                    normalized.WorkStartDate!.Value,
                    token);
            }
            await repository.ReplaceOrganizationAssignmentsAsync(employee.Id, normalized.OrganizationIds, token);
            await repository.SaveChangesAsync(token);
            var details = await repository.GetDetailsByIdAsync(employee.Id, token)
                ?? throw new InvalidOperationException("The created employee could not be reloaded.");
            await audit.WriteAsync(new AuditWriteRequest("EmployeeCreated", nameof(Employee), employee.Id.ToString(),
                employee.Id, null, details, $"Created employee {normalized.EmployeeNumber}."), token);
            return details;
        }, cancellationToken);
    }
}
