using MIS.Domain.Constants;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class AbsenceImportMapperTests
{
    private static AbsenceImportMapper.EmployeeMatch Employee(
        string number = "E-001",
        string? mobile = "01012345678",
        string name = "Ahmed Test",
        string? arabic = "أحمد") =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), number, "29801010101234", mobile, name, arabic, "Ahmed Test", new DateOnly(2020, 1, 1), null);

    [Fact]
    public void Matches_employee_by_number_then_mobile_and_rejects_unmatched()
    {
        var employees = new[] { Employee() };
        var today = new DateOnly(2026, 9, 12);
        var empty = new HashSet<(Guid, DateOnly)>();
        var batch = new HashSet<(Guid, DateOnly)>();

        var byNumber = AbsenceImportMapper.Map(2, new() { ["EmployeeNumber"] = "E-001", ["AbsenceDate"] = "2026-09-10" }, null, employees, empty, empty, empty, empty, batch, today);
        Assert.Equal("Ready", byNumber.Status);
        Assert.Equal(employees[0].Id, byNumber.Record.EmployeeId);

        batch.Clear();
        var byMobile = AbsenceImportMapper.Map(3, new() { ["MobileNumber"] = "01012345678", ["AbsenceDate"] = "2026-09-10" }, null, employees, empty, empty, empty, empty, batch, today);
        Assert.Equal("Ready", byMobile.Status);

        batch.Clear();
        var missing = AbsenceImportMapper.Map(4, new() { ["EmployeeNumber"] = "MISSING", ["AbsenceDate"] = "2026-09-10" }, null, employees, empty, empty, empty, empty, batch, today);
        Assert.Equal("Error", missing.Status);
        Assert.Contains(missing.Errors, e => e.Contains("Unmatched Employee", StringComparison.OrdinalIgnoreCase) || e.Contains("موظف غير موجود"));
    }

    [Fact]
    public void Duplicate_absence_is_existing_and_approved_excuse_is_error()
    {
        var employees = new[] { Employee() };
        var today = new DateOnly(2026, 9, 12);
        var date = new DateOnly(2026, 9, 10);
        var key = (employees[0].Id, date);
        var batch = new HashSet<(Guid, DateOnly)>();

        var duplicate = AbsenceImportMapper.Map(2, new() { ["EmployeeNumber"] = "E-001", ["AbsenceDate"] = "2026-09-10" }, null, employees, [key], [], [], [], batch, today);
        Assert.Equal("Existing", duplicate.Status);

        batch.Clear();
        var excused = AbsenceImportMapper.Map(3, new() { ["EmployeeNumber"] = "E-001", ["AbsenceDate"] = "2026-09-10", ["Status"] = "Pending" }, null, employees, [], [], [], [key], batch, today);
        Assert.Equal("Error", excused.Status);
        Assert.Contains(excused.Errors, e => e.Contains("approved excuse", StringComparison.OrdinalIgnoreCase) || e.Contains("عذر"));
    }

    [Fact]
    public void Ambiguous_name_match_is_rejected()
    {
        var employees = new[]
        {
            Employee(number: "E-001", name: "Same Name"),
            new AbsenceImportMapper.EmployeeMatch(Guid.NewGuid(), "E-002", null, null, "Same Name", null, null, new DateOnly(2020, 1, 1), null)
        };
        var today = new DateOnly(2026, 9, 12);
        var batch = new HashSet<(Guid, DateOnly)>();
        var row = AbsenceImportMapper.Map(2, new() { ["EmployeeName"] = "Same Name", ["AbsenceDate"] = "2026-09-10" }, null, employees, [], [], [], [], batch, today);
        Assert.Equal("Error", row.Status);
        Assert.Contains(row.Errors, e => e.Contains("Multiple", StringComparison.OrdinalIgnoreCase) || e.Contains("أكثر من موظف"));
    }

    [Fact]
    public void Defaults_type_and_status_and_parses_arabic_status()
    {
        var employees = new[] { Employee() };
        var today = new DateOnly(2026, 9, 12);
        var batch = new HashSet<(Guid, DateOnly)>();
        var row = AbsenceImportMapper.Map(2, new() { ["EmployeeNumber"] = "E-001", ["AbsenceDate"] = "10/09/2026", ["Status"] = "بدون عذر" }, "dd/MM/yyyy", employees, [], [], [], [], batch, today);
        Assert.Equal("Ready", row.Status);
        Assert.Equal(AbsenceValues.AbsentType, row.Record.Type);
        Assert.Equal(AbsenceValues.UnexcusedStatus, row.Record.Status);
        Assert.Equal(new DateOnly(2026, 9, 10), row.Record.AbsenceDate);
    }
}
