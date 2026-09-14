using System.Text;
using ClosedXML.Excel;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeImportTests
{
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid PositionId = Guid.NewGuid();
    private static readonly EmployeeImportMapper.Lookup[] Departments = [new(DepartmentId, "Accounting", "ACC", "الحسابات")];
    private static readonly EmployeeImportMapper.Lookup[] Positions = [new(PositionId, "Accountant", "ACCOUNTANT", "محاسب")];
    private static Dictionary<string, string> Values() => new()
    {
        ["EmployeeNumber"] = "TEST-IMPORT", ["FullName"] = "Test Employee", ["NationalId"] = "12345678901234",
        ["Department"] = "accounting", ["Position"] = "ACCOUNTANT", ["OperationalRole"] = "ADMIN", ["WorkStartDate"] = "17-Aug-26"
    };

    [Theory]
    [InlineData("Male", "Male")]
    [InlineData("M", "Male")]
    [InlineData("ذكر", "Male")]
    [InlineData("Female", "Female")]
    [InlineData("F", "Female")]
    [InlineData("أنثى", "Female")]
    public void Mapper_normalizes_gender_and_looks_up_existing_master_data(string source, string expected)
    {
        var values = Values(); values["Gender"] = source;
        var errors = new List<string>();
        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);
        Assert.Empty(errors); Assert.Equal(expected, request.Gender);
        Assert.Equal(DepartmentId, request.DepartmentId); Assert.Equal(PositionId, request.PositionId);
        Assert.Equal(new DateOnly(2026, 8, 17), request.WorkStartDate);
    }

    [Theory]
    [InlineData("2026-08-17")]
    [InlineData("17/08/2026")]
    [InlineData("17-Aug-26")]
    [InlineData("2026-08-17 00:00:00.0000000")]
    public void Import_dates_include_native_Excel_representation(string value)
    {
        Assert.True(EmployeeImportMapper.TryDate(value, null, out var date));
        Assert.Equal(new DateOnly(2026, 8, 17), date);
    }

    [Theory]
    [InlineData("Collector", "COLLECTOR")]
    [InlineData("Supervisor", "SUPERVISOR")]
    [InlineData("Data Entry", "ADMIN")]
    [InlineData("Accounting", "ADMIN")]
    [InlineData("محصل", "COLLECTOR")]
    [InlineData("مشرف", "SUPERVISOR")]
    public void Mapper_maps_employee_role_aliases_to_operational_roles(string source, string expected)
    {
        var values = Values(); values["OperationalRole"] = source;
        var errors = new List<string>();
        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);
        Assert.Empty(errors);
        Assert.Equal(expected, request.OperationalRole);
    }

    [Fact]
    public void Mapper_reports_department_not_found_with_source_value()
    {
        var values = Values(); values["Department"] = "Unknown Dept";
        var errors = new List<string>();
        EmployeeImportMapper.Map(values, null, Departments, Positions, errors);
        Assert.Contains(errors, error => error == "Department not found: Unknown Dept");
    }

    [Fact]
    public void Unknown_or_ambiguous_lookups_and_invalid_dates_are_errors()
    {
        var values = Values(); values["Department"] = "Accounts"; values["DateOfBirth"] = "31/02/2000";
        var errors = new List<string>();
        EmployeeImportMapper.Map(values, null, Departments, Positions, errors);
        Assert.Contains(errors, error => error.StartsWith("Department not found:"));
        Assert.Contains(errors, error => error.StartsWith("DateOfBirth:"));
        values["Department"] = "Accounting"; errors.Clear();
        EmployeeImportMapper.Map(values, null, [.. Departments, new(Guid.NewGuid(), "Accounting", "OTHER", null)], Positions, errors);
        Assert.Contains(errors, error => error.StartsWith("Department not found:") || error.StartsWith("Department:"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_CSV_and_Excel_reader_returns_source_columns_without_creating_employees(bool excel)
    {
        using var stream = new MemoryStream();
        if (excel)
        {
            using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Employees");
            sheet.Cell(1, 1).Value = "Code"; sheet.Cell(1, 2).Value = "Name"; sheet.Cell(1, 3).Value = "Date Of Employment";
            sheet.Cell(2, 1).Value = "TEST-IMPORT"; sheet.Cell(2, 2).Value = "Test Employee";
            sheet.Cell(2, 3).Value = new DateTime(2026, 8, 17); sheet.Cell(2, 3).Style.DateFormat.Format = "dd-mmm-yy";
            workbook.SaveAs(stream);
        }
        else { var bytes = Encoding.UTF8.GetBytes("Code,Name,Date Of Employment\nTEST-IMPORT,Test Employee,17-Aug-26\n"); stream.Write(bytes); }
        var table = await AttendanceImportParser.ReadTableAsync(stream, excel ? ".xlsx" : ".csv", null, 1, 2, default);
        Assert.Equal(["Code", "Name", "Date Of Employment"], table.Headers);
        var row = Assert.Single(table.Rows); Assert.Equal("TEST-IMPORT", row[0]);
        Assert.True(EmployeeImportMapper.TryDate(row[2], null, out var date)); Assert.Equal(new DateOnly(2026, 8, 17), date);
    }

    [Fact]
    public async Task Preview_validation_is_read_only_and_creation_uses_existing_employee_rules()
    {
        var repository = new MemoryEmployees(); var audit = new MemoryAudit();
        var service = new EmployeeCreationService(repository, new MemoryTransactions(), audit);
        var values = Values(); values["Gender"] = "F"; values["WorkEndDate"] = "31-Aug-26";
        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, []);
        await service.ValidateAsync(request, default);
        Assert.Empty(repository.Employees); Assert.Empty(audit.Actions);
        var result = await service.CreateAsync(request, default);
        var employee = Assert.Single(repository.Employees);
        Assert.Equal(result.Id, employee.Id); Assert.Equal("Female", employee.Gender);
        Assert.False(employee.IsArchived); Assert.Equal("Active", employee.Status);
        Assert.Equal(new DateOnly(2026, 8, 31), employee.TerminationDate);
        Assert.Equal(["EmployeeCreated"], audit.Actions);
        await Assert.ThrowsAsync<HrConflictException>(() => service.CreateAsync(request, default));
        Assert.Single(repository.Employees);
    }

    [Fact]
    public async Task National_ID_duplicate_is_rejected_even_with_different_employee_number()
    {
        var repository = new MemoryEmployees(); var service = new EmployeeCreationService(repository, new MemoryTransactions(), new MemoryAudit());
        var values = Values();
        await service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default);
        values["EmployeeNumber"] = "DIFFERENT";
        await Assert.ThrowsAsync<HrConflictException>(() => service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default));
        Assert.Single(repository.Employees);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Inactive")]
    [InlineData("Terminated")]
    public async Task Explicit_status_keeps_mapped_end_work_date_without_archiving(string status)
    {
        var repository = new MemoryEmployees(); var service = new EmployeeCreationService(repository, new MemoryTransactions(), new MemoryAudit());
        var values = Values(); values["Status"] = status; values["WorkEndDate"] = "31-Aug-26";
        await service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default);
        var employee = Assert.Single(repository.Employees);
        Assert.Equal(status, employee.Status);
        Assert.Equal(new DateOnly(2026, 8, 31), employee.TerminationDate);
        Assert.False(employee.IsArchived);
    }

    [Theory]
    [InlineData("NationalId", "123")]
    [InlineData("FullName", "")]
    [InlineData("EmployeeNumber", "")]
    [InlineData("OperationalRole", "UNKNOWN")]
    [InlineData("WorkEndDate", "01-Jan-2000")]
    public async Task Invalid_requests_cannot_create_employee(string field, string value)
    {
        var repository = new MemoryEmployees(); var service = new EmployeeCreationService(repository, new MemoryTransactions(), new MemoryAudit());
        var values = Values(); values[field] = value;
        await Assert.ThrowsAsync<HrValidationException>(() => service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default));
        Assert.Empty(repository.Employees);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" 01012345678 ", "01012345678")]
    [InlineData("01123456789", "01123456789")]
    [InlineData("+201012345678", "+201012345678")]
    [InlineData("+20 10 1234 5678", "+20 10 1234 5678")]
    public async Task Optional_mobile_flows_from_import_through_creation_and_details(string? source, string? expected)
    {
        var values = Values(); if (source != null) values["MobileNumber"] = source;
        var repository = new MemoryEmployees();
        var service = new EmployeeCreationService(repository, new MemoryTransactions(), new MemoryAudit());
        var result = await service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default);
        Assert.Equal(expected, Assert.Single(repository.Employees).MobileNumber);
        Assert.Equal(expected, result.MobileNumber);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("12345678901234567890123456789012345")]
    public async Task Invalid_mobile_does_not_create_an_employee(string mobile)
    {
        var values = Values(); values["MobileNumber"] = mobile;
        var repository = new MemoryEmployees();
        var service = new EmployeeCreationService(repository, new MemoryTransactions(), new MemoryAudit());
        await Assert.ThrowsAsync<HrValidationException>(() => service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default));
        Assert.Empty(repository.Employees);
        Assert.Throws<HrValidationException>(() => EmployeeMobileNumber.Normalize(mobile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Phone_reader_preserves_text_and_Excel_zero_padding(bool excel)
    {
        using var stream = new MemoryStream();
        if (excel)
        {
            using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Employees");
            sheet.Cell(1, 1).Value = "Telephone";
            sheet.Cell(2, 1).Value = "01012345678";
            sheet.Cell(3, 1).Value = "+201012345678";
            sheet.Cell(4, 1).Value = 1012345678d;
            sheet.Cell(4, 1).Style.NumberFormat.Format = "00000000000";
            workbook.SaveAs(stream);
        }
        else { var bytes = Encoding.UTF8.GetBytes("Telephone\n01012345678\n+201012345678\n01012345678\n"); stream.Write(bytes); }
        var table = await AttendanceImportParser.ReadTableAsync(stream, excel ? ".xlsx" : ".csv", null, 1, 2, default, "Telephone");
        Assert.Equal(new[] { "01012345678", "+201012345678", "01012345678" }, table.Rows.Select(row => row[0]));
    }

    [Fact]
    public void Editing_primary_mobile_preserves_other_contact_fields_and_can_clear_mobile()
    {
        var employee = new Employee("TEST", "Test Employee", DepartmentId, true, DateTimeOffset.UtcNow);
        employee.UpdateContactInformation("01012345678", "01123456789", "test@example.com", "Cairo", "Cairo", DateTimeOffset.UtcNow);
        employee.UpdateContactInformation(EmployeeMobileNumber.Normalize(" +201012345678 "), employee.AlternativeMobileNumber, employee.Email, employee.Address, employee.City, DateTimeOffset.UtcNow);
        Assert.Equal("+201012345678", employee.MobileNumber);
        Assert.Equal("01123456789", employee.AlternativeMobileNumber); Assert.Equal("test@example.com", employee.Email);
        employee.UpdateContactInformation(EmployeeMobileNumber.Normalize(" "), employee.AlternativeMobileNumber, employee.Email, employee.Address, employee.City, DateTimeOffset.UtcNow);
        Assert.Null(employee.MobileNumber); Assert.Equal("Cairo", employee.Address);
    }

    private sealed class MemoryTransactions : IHrTransactionRunner
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) => operation(cancellationToken);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) => operation(cancellationToken);
    }
    private sealed class MemoryAudit : IHrAuditService
    {
        public List<string> Actions { get; } = [];
        public Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken) { Actions.Add(request.Action); return Task.CompletedTask; }
        public Task<PagedAuditLogsDto> GetPagedAsync(int page, int pageSize, string? search, string? action, string? entityType, Guid? employeeId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class MemoryEmployees : IHrEmployeeRepository
    {
        public List<Employee> Employees { get; } = [];
        public void Add(Employee employee) => Employees.Add(employee);
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
        public Task<bool> DepartmentExistsAsync(Guid id, CancellationToken token) => Task.FromResult(id == DepartmentId);
        public Task<bool> PositionExistsAsync(Guid id, CancellationToken token) => Task.FromResult(id == PositionId);
        public Task<bool> EmployeeNumberExistsAsync(string number, Guid? excludingId, CancellationToken token) => Task.FromResult(Employees.Any(employee => string.Equals(employee.EmployeeNumber, number, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> NationalIdExistsAsync(string number, Guid? excludingId, CancellationToken token) => Task.FromResult(Employees.Any(employee => employee.NationalId == number));
        public Task<EmployeeDetailsDto?> GetDetailsByIdAsync(Guid id, CancellationToken token)
        {
            var employee = Employees.Single(item => item.Id == id);
            return Task.FromResult<EmployeeDetailsDto?>(new(employee.Id, employee.EmployeeNumber, employee.FullName, employee.NationalId,
                DepartmentId, "Accounting", "ACC", PositionId, "Accountant", employee.OperationalRole, employee.HireDate,
                employee.FingerprintEnrollmentDate, employee.DateOfBirth, employee.Address, employee.TerminationDate,
                employee.IsActive, employee.CreatedAt, employee.UpdatedAt, employee.Status, employee.IsArchived, null, null, employee.MobileNumber));
        }
        public Task<Employee?> GetTrackedByIdAsync(Guid id, CancellationToken token) => Task.FromResult(Employees.SingleOrDefault(item => item.Id == id));
        public Task<IReadOnlyCollection<DepartmentOptionDto>> GetDepartmentsAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<PagedEmployeesDto> GetPagedAsync(int page, int pageSize, string? search, Guid? departmentId, bool? isActive, CancellationToken token) => throw new NotSupportedException();
        public Task<PagedEmployeesDto> GetPagedByStatusAsync(int page, int pageSize, string? search, Guid? departmentId, string? status, string? role, bool? archived, CancellationToken token) => throw new NotSupportedException();
    }
}
