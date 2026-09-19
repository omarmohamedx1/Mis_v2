using MIS.Application.Common;
using MIS.Application.Interfaces;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class SocialInsuranceImportTests
{
    private static readonly SocialInsuranceImportMapper.EmployeeMatch Employee = new(Guid.NewGuid(), "EMP-1", "12345678901234", "Test Employee");
    private static Dictionary<string, string> Values() => new() { ["EmployeeNumber"] = "EMP-1", ["SocialInsuranceNumber"] = "1234", ["InsuranceStartDate"] = "2026-01-01", ["InsurableSalary"] = "5000.25", ["InsuranceStatus"] = "Insured" };
    [Fact]
    public void Matches_existing_employee_and_never_uses_name_alone()
    {
        var values = Values();
        Assert.Equal(Employee.Id, SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Record.EmployeeId);
        values.Remove("EmployeeNumber"); values["NationalId"] = Employee.NationalId!;
        Assert.Equal("Ready", SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Status);
        values.Remove("NationalId"); values["EmployeeId"] = Employee.Id.ToString();
        Assert.Equal("Ready", SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Status);
        values.Remove("EmployeeId"); values["EmployeeName"] = Employee.Name;
        Assert.Equal("Error", SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Status);
    }
    [Fact]
    public void Strong_identifier_cannot_be_bypassed_and_conflicts_are_rejected()
    {
        var values = Values(); values["EmployeeNumber"] = "UNKNOWN"; values["NationalId"] = Employee.NationalId!;
        Assert.Equal("Error", SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Status);
        values["EmployeeNumber"] = Employee.Number; values["NationalId"] = "different";
        Assert.Equal("Error", SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Status);
    }
    [Theory]
    [InlineData("InsurableSalary", "0")]
    [InlineData("InsurableSalary", "-1")]
    [InlineData("InsurableSalary", "1.234")]
    [InlineData("InsuranceStartDate", "")]
    [InlineData("InsuranceStartDate", "31/02/2026")]
    [InlineData("InsuranceStatus", "Unknown")]
    [InlineData("SocialInsuranceNumber", "")]
    [InlineData("InsuranceEndDate", "2025-01-01")]
    public void Invalid_data_is_not_silently_fixed(string field, string value)
    {
        var values = Values(); values[field] = value;
        Assert.Equal("Error", SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Status);
    }
    [Fact]
    public void Existing_employee_or_number_is_skipped_and_ended_dates_are_validated()
    {
        Assert.Equal("Existing", SocialInsuranceImportMapper.Map(2, Values(), null, [Employee], [Employee.Id], []).Status);
        Assert.Equal("Existing", SocialInsuranceImportMapper.Map(2, Values(), null, [Employee], [], ["1234"]).Status);
        var values = Values(); values["InsuranceStatus"] = "منتهي"; values["InsuranceEndDate"] = "2026-02-01";
        var row = SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []);
        Assert.Equal("Ready", row.Status); Assert.Equal("Ended", SocialInsuranceImportMapper.Create(row.Record).InsuranceStatus);
        values["InsuranceEndDate"] = "2025-02-01";
        Assert.Equal("Error", SocialInsuranceImportMapper.Map(2, values, null, [Employee], [], []).Status);
    }
    [Fact]
    public async Task Every_import_operation_requires_management_permission_before_accessing_data()
    {
        var service = new SocialInsuranceImportService(null!, null!, null!, new Viewer(), null!);
        await Assert.ThrowsAsync<HrForbiddenException>(() => service.BuildTemplateAsync(default));
        await Assert.ThrowsAsync<HrForbiddenException>(() => service.UploadAsync(null!, default));
        await Assert.ThrowsAsync<HrForbiddenException>(() => service.PreviewAsync(Guid.NewGuid(), new(), default));
        await Assert.ThrowsAsync<HrForbiddenException>(() => service.ConfirmAsync(Guid.NewGuid(), Guid.NewGuid(), default));
        await Assert.ThrowsAsync<HrForbiddenException>(() => service.HistoryAsync(default));
    }
    private sealed class Viewer : ICurrentUserContext
    {
        public Guid UserId => Guid.NewGuid(); public string Username => "viewer";
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => ["hr.social_insurance.view"];
    }
}
