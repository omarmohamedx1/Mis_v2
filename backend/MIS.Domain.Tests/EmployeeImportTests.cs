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
        ["EmployeeNumber"] = "TEST-IMPORT", ["FullName"] = "Test Employee", ["NationalId"] = "28712010111213",
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

    [Fact]
    public void Mapper_maps_basic_salary_and_allowances()
    {
        var values = Values();
        values["BasicSalary"] = "12,500.50";
        values["Allowances"] = "١٢٠٠";
        var errors = new List<string>();
        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);
        Assert.Empty(errors);
        Assert.Equal(12500.50m, request.BasicSalary);
        Assert.Equal(1200m, request.Allowances);
    }

    [Fact]
    public void Mapper_maps_optional_work_number_and_package_type()
    {
        var values = Values();
        values["WorkNumber"] = "  W-441 ";
        values["PackageType"] = "باقة قانونية";
        var errors = new List<string>();
        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);
        Assert.Empty(errors);
        Assert.Equal("W-441", request.WorkNumber);
        Assert.Equal("باقة قانونية", request.PackageType);
    }

    [Fact]
    public void Mapper_leaves_work_number_and_package_type_empty_when_absent()
    {
        var errors = new List<string>();
        var request = EmployeeImportMapper.Map(Values(), null, Departments, Positions, errors);
        Assert.Empty(errors);
        Assert.Null(request.WorkNumber);
        Assert.Null(request.PackageType);
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
    [InlineData("Office", "OFFICE")]
    [InlineData("Office Boy", "OFFICE")]
    [InlineData("أوفيس", "OFFICE")]
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

    [Theory]
    [InlineData("Collector", "Collector", "COLLECTOR")]
    [InlineData("Supervisor", "Supervisor", "SUPERVISOR")]
    [InlineData("Collection Manegar", "Collection Manager", "SUPERVISOR")]
    [InlineData("Office Boy", "Office Boy", "OFFICE")]
    [InlineData("Office Girl", "Office Girl", "OFFICE")]
    public void Mapper_infers_operational_role_from_legacy_title_when_role_column_is_absent(
        string sourceTitle, string masterTitle, string expectedRole)
    {
        var values = Values();
        values["Position"] = sourceTitle;
        values.Remove("OperationalRole");
        var errors = new List<string>();
        var positions = new[] { new EmployeeImportMapper.Lookup(PositionId, masterTitle, masterTitle.ToUpperInvariant().Replace(' ', '_'), null) };

        var request = EmployeeImportMapper.Map(values, null, Departments, positions, errors);

        Assert.Empty(errors);
        Assert.Equal(expectedRole, request.OperationalRole);
    }

    [Fact]
    public void Mapper_normalizes_hyphens_underscores_spacing_and_non_breaking_spaces_in_master_data()
    {
        var values = Values();
        values["Department"] = "  DATA-ENTRY\u00a0";
        var dataEntryId = Guid.NewGuid();
        var departments = new[] { new EmployeeImportMapper.Lookup(dataEntryId, "Data Entry", "DATA_ENTRY", "إدخال البيانات") };
        var errors = new List<string>();

        var request = EmployeeImportMapper.Map(values, null, departments, Positions, errors);

        Assert.Empty(errors);
        Assert.Equal(dataEntryId, request.DepartmentId);
    }

    [Fact]
    public void Mapper_treats_bank_department_as_collections_assignment()
    {
        var values = Values();
        values["Department"] = "AlexBank";
        values["Position"] = "collector";
        values.Remove("OperationalRole");
        var collectionsId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var errors = new List<string>();
        var warnings = new List<string>();

        var request = EmployeeImportMapper.Map(values, null,
            [new EmployeeImportMapper.Lookup(collectionsId, "Collections", "COLLECTIONS", "التحصيل")],
            [new EmployeeImportMapper.Lookup(positionId, "Collector", "COLLECTOR", null)],
            errors, warnings,
            [new EmployeeImportMapper.Lookup(organizationId, "AlexBank", "ALEXBANK", "بنك الإسكندرية")],
            new EmployeeImportMapper.Lookup(collectionsId, "Collections", "COLLECTIONS", "التحصيل"));

        Assert.Empty(errors);
        Assert.Equal(collectionsId, request.DepartmentId);
        Assert.Equal(positionId, request.PositionId);
        Assert.Equal("COLLECTOR", request.OperationalRole);
        Assert.Equal([organizationId], request.OrganizationIds);
        Assert.Contains(warnings, warning => warning.Contains("Collections", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Halan")]
    [InlineData("MNT-Halan")]
    [InlineData("Attijariwafa Egypt")]
    [InlineData("Premium Card")]
    public void Mapper_matches_client_aliases_when_the_spreadsheet_department_is_a_bank_or_company(string department)
    {
        var values = Values();
        values["Department"] = department;
        values["Position"] = "collector";
        values.Remove("OperationalRole");
        var collectionsId = Guid.NewGuid();
        var alexId = Guid.NewGuid();
        var attijariId = Guid.NewGuid();
        var rayaId = Guid.NewGuid();
        var halanId = Guid.NewGuid();
        var premiumId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var errors = new List<string>();
        var warnings = new List<string>();
        var organizations = new EmployeeImportMapper.Lookup[]
        {
            new(alexId, "AlexBank", "ALEXBANK", "بنك الإسكندرية"),
            new(attijariId, "Attijariwafa Bank Egypt", "ATTIJARIWAFA", "التجاري وفا بنك إيجيبت"),
            new(rayaId, "Raya", "RAYA", "راية"),
            new(halanId, "MNT-Halan", "MNT_HALAN", "إم إن تي حالا"),
            new(premiumId, "Premium Card", "PREMIUM_CARD", "بريميوم كارد")
        };

        var request = EmployeeImportMapper.Map(values, null,
            [new EmployeeImportMapper.Lookup(collectionsId, "Collections", "COLLECTIONS", "التحصيل")],
            [new EmployeeImportMapper.Lookup(positionId, "Collector", "COLLECTOR", "محصل")],
            errors, warnings, organizations,
            new EmployeeImportMapper.Lookup(collectionsId, "Collections", "COLLECTIONS", "التحصيل"));

        Assert.Empty(errors);
        Assert.Equal(collectionsId, request.DepartmentId);
        Assert.Single(request.OrganizationIds);
        Assert.Contains(warnings, warning => warning.Contains("Collections", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mapper_treats_office_title_department_as_office()
    {
        var values = Values();
        values["Department"] = "Office girl";
        values["Position"] = "OFFICE GIRL";
        values.Remove("OperationalRole");
        var adminId = Guid.NewGuid();
        var officeId = Guid.NewGuid();
        var collectionsId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var errors = new List<string>();
        var warnings = new List<string>();

        var request = EmployeeImportMapper.Map(values, null,
            [
                new EmployeeImportMapper.Lookup(adminId, "Administration", "ADMIN", "الإدارة"),
                new EmployeeImportMapper.Lookup(officeId, "Office", "OFFICE", "الأوفيس"),
                new EmployeeImportMapper.Lookup(collectionsId, "Collections", "COLLECTIONS", "التحصيل")
            ],
            [new EmployeeImportMapper.Lookup(positionId, "Office Girl", "OFFICE_GIRL", "عاملة خدمات", officeId)],
            errors, warnings, [],
            new EmployeeImportMapper.Lookup(collectionsId, "Collections", "COLLECTIONS", "التحصيل"));

        Assert.Empty(errors);
        Assert.Equal(officeId, request.DepartmentId);
        Assert.Equal(positionId, request.PositionId);
        Assert.Equal("OFFICE", request.OperationalRole);
        Assert.Empty(request.OrganizationIds);
        Assert.Contains(warnings, warning => warning.Contains("position", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mapper_splits_arabic_and_english_names_into_localized_fields()
    {
        var values = Values();
        values["FullName"] = "محمد أحمد";
        values["FullNameEnglish"] = "Mohamed Ahmed";
        var errors = new List<string>();

        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);

        Assert.Empty(errors);
        Assert.Equal("محمد أحمد", request.FullNameArabic);
        Assert.Equal("Mohamed Ahmed", request.FullNameEnglish);
        Assert.Equal("محمد أحمد", request.FullName);
    }

    [Fact]
    public void Mapper_normalizes_Arabic_digits_and_restores_an_unambiguous_mobile_leading_zero()
    {
        var values = Values();
        values["MobileNumber"] = "١٠١٢٣٤٥٦٧٨";
        values["NationalId"] = "٢٨٧١٢٠١٠١١١٢١٣";
        values["DateOfBirth"] = "01/12/1987";
        var errors = new List<string>();
        var warnings = new List<string>();

        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, errors, warnings);

        Assert.Empty(errors);
        Assert.Equal("01012345678", request.MobileNumber);
        Assert.Equal("28712010111213", request.NationalId);
        Assert.Contains("The missing leading zero was restored in the mobile number.", warnings);
    }

    [Theory]
    [InlineData("0101234567")]
    [InlineData("010123456789")]
    [InlineData("01312345678")]
    public void Mapper_rejects_mobile_numbers_with_missing_or_invalid_digits(string mobile)
    {
        var values = Values(); values["MobileNumber"] = mobile;
        var errors = new List<string>();

        _ = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);

        Assert.Contains("Mobile number must contain exactly 11 digits and use a valid Egyptian mobile prefix.", errors);
    }

    [Theory]
    [InlineData("2871201011121")]
    [InlineData("18712010111213")]
    [InlineData("28702310111213")]
    public void Mapper_rejects_invalid_Egyptian_national_IDs(string nationalId)
    {
        var values = Values(); values["NationalId"] = nationalId;
        var errors = new List<string>();

        _ = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Mapper_rejects_date_of_birth_that_does_not_match_national_ID()
    {
        var values = Values(); values["DateOfBirth"] = "02/12/1987";
        var errors = new List<string>();

        _ = EmployeeImportMapper.Map(values, null, Departments, Positions, errors);

        Assert.Contains("Date of birth does not match the birth date encoded in the National ID.", errors);
    }

    [Fact]
    public void Mapper_reports_department_not_found_with_source_value()
    {
        var values = Values(); values["Department"] = "Unknown Dept";
        var errors = new List<string>();
        EmployeeImportMapper.Map(values, null, Departments, Positions, errors);
        Assert.Contains(errors, error => error == "Department not found: Unknown Dept");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Accountant")]
    public void Mapper_safely_infers_department_from_linked_position_when_department_is_missing_or_duplicated(string department)
    {
        var values = Values(); values["Department"] = department;
        var errors = new List<string>();
        var warnings = new List<string>();
        var linkedPositions = new[] { new EmployeeImportMapper.Lookup(PositionId, "Accountant", "ACCOUNTANT", "محاسب", DepartmentId) };

        var request = EmployeeImportMapper.Map(values, null, Departments, linkedPositions, errors, warnings);

        Assert.Empty(errors);
        Assert.Equal(DepartmentId, request.DepartmentId);
        Assert.Contains("Department was inferred from the selected position.", warnings);
    }

    [Fact]
    public void Mapper_rejects_department_that_conflicts_with_linked_position()
    {
        var otherDepartmentId = Guid.NewGuid();
        var values = Values(); values["Department"] = "Human Resources";
        var errors = new List<string>();
        var departments = new[]
        {
            Departments[0],
            new EmployeeImportMapper.Lookup(otherDepartmentId, "Human Resources", "HR", "الموارد البشرية")
        };
        var linkedPositions = new[] { new EmployeeImportMapper.Lookup(PositionId, "Accountant", "ACCOUNTANT", "محاسب", DepartmentId) };

        _ = EmployeeImportMapper.Map(values, null, departments, linkedPositions, errors);

        Assert.Contains("Department does not match the selected position.", errors);
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
        var values = Values(); values["Gender"] = "F"; values["NationalId"] = "28712010111223"; values["WorkEndDate"] = "31-Aug-26";
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
    [InlineData("+201012345678", "01012345678")]
    [InlineData("+20 10 1234 5678", "01012345678")]
    public async Task Optional_mobile_flows_from_import_through_creation_and_details(string? source, string? expected)
    {
        var values = Values(); if (source != null) values["MobileNumber"] = source;
        var repository = new MemoryEmployees();
        var service = new EmployeeCreationService(repository, new MemoryTransactions(), new MemoryAudit());
        var result = await service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default);
        Assert.Equal(expected, Assert.Single(repository.Employees).MobileNumber);
        Assert.Equal(expected, result.MobileNumber);
    }

    [Fact]
    public async Task Optional_work_assignment_flows_from_import_through_creation()
    {
        var values = Values();
        values["WorkNumber"] = "JOB-88";
        values["PackageType"] = "Legal";
        var repository = new MemoryEmployees();
        var service = new EmployeeCreationService(repository, new MemoryTransactions(), new MemoryAudit());
        var result = await service.CreateAsync(EmployeeImportMapper.Map(values, null, Departments, Positions, []), default);
        var employee = Assert.Single(repository.Employees);
        Assert.Equal("JOB-88", employee.WorkNumber);
        Assert.Equal("Legal", employee.PackageType);
        Assert.Equal("JOB-88", result.WorkNumber);
        Assert.Equal("Legal", result.PackageType);
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
        var table = await AttendanceImportParser.ReadTableAsync(stream, excel ? ".xlsx" : ".csv", null, 1, 2, default, ["Telephone"]);
        Assert.Equal(new[] { "01012345678", "+201012345678", "01012345678" }, table.Rows.Select(row => row[0]));
    }

    [Fact]
    public async Task Excel_numeric_identity_cells_are_read_without_rounding_and_mobile_zero_is_recovered_during_mapping()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Employees");
            sheet.Cell(1, 1).Value = "Mobile";
            sheet.Cell(1, 2).Value = "National ID";
            sheet.Cell(2, 1).Value = 1012345678d;
            sheet.Cell(2, 2).Value = 28712010111213d;
            workbook.SaveAs(stream);
        }

        var table = await AttendanceImportParser.ReadTableAsync(stream, ".xlsx", null, 1, 2, default, ["Mobile", "National ID"]);
        var cells = Assert.Single(table.Rows);
        Assert.Equal("1012345678", cells[0]);
        Assert.Equal("28712010111213", cells[1]);

        var values = Values();
        values["MobileNumber"] = cells[0];
        values["NationalId"] = cells[1];
        var errors = new List<string>();
        var warnings = new List<string>();
        var request = EmployeeImportMapper.Map(values, null, Departments, Positions, errors, warnings);
        Assert.Empty(errors);
        Assert.Equal("01012345678", request.MobileNumber);
        Assert.Contains("The missing leading zero was restored in the mobile number.", warnings);
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
        public Task UpsertCurrentSalaryAsync(Guid employeeId, decimal basicSalary, decimal allowances, DateOnly effectiveFrom, CancellationToken token) => Task.CompletedTask;
        public Task DeleteUnusedAsync(Guid id, CancellationToken token) { Employees.RemoveAll(item => item.Id == id); return Task.CompletedTask; }
        public Task DeletePermanentlyAsync(Guid id, CancellationToken token) => DeleteUnusedAsync(id, token);
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
                employee.IsActive, employee.CreatedAt, employee.UpdatedAt, employee.Status, employee.IsArchived, null, null, employee.MobileNumber,
                employee.FullNameArabic, employee.FullNameEnglish, Array.Empty<EmployeeOrganizationAssignmentDto>(), null, null, employee.WorkNumber, employee.PackageType));
        }
        public Task<Employee?> GetTrackedByIdAsync(Guid id, CancellationToken token) => Task.FromResult(Employees.SingleOrDefault(item => item.Id == id));
        public Task<IReadOnlyCollection<DepartmentOptionDto>> GetDepartmentsAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<EmployeeOrganizationAssignmentDto>> GetOrganizationsAsync(CancellationToken token) => Task.FromResult<IReadOnlyCollection<EmployeeOrganizationAssignmentDto>>([]);
        public Task<bool> OrganizationsExistAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken token) => Task.FromResult(true);
        public Task ReplaceOrganizationAssignmentsAsync(Guid employeeId, IReadOnlyCollection<Guid> organizationIds, CancellationToken token) => Task.CompletedTask;
        public Task<PagedEmployeesDto> GetPagedAsync(int page, int pageSize, string? search, Guid? departmentId, bool? isActive, CancellationToken token) => throw new NotSupportedException();
        public Task<PagedEmployeesDto> GetPagedByStatusAsync(int page, int pageSize, string? search, string? searchField, Guid? departmentId, string? status, string? role, bool? archived, Guid? organizationId, string? gender, Guid? positionId, CancellationToken token) => throw new NotSupportedException();
        public Task<PositionLookupDto?> GetPositionAsync(Guid id, CancellationToken token) =>
            Task.FromResult<PositionLookupDto?>(id == PositionId ? new(PositionId, "ACCOUNTANT", "Accountant", "محاسب", DepartmentId) : null);
    }
}
