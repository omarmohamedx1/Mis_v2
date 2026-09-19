using ClosedXML.Excel;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class HrImportTemplateTests
{
    [Fact]
    public void Social_insurance_template_headers_match_import_fields()
    {
        using var workbook = new XLWorkbook(new MemoryStream(HrImportWorkbookBuilder.BuildSocialInsurance()));
        Assert.Equal(
            ["Employee Number", "Employee Name", "National ID", "Social Insurance Number",
             "Insurance Start Date", "Insurance End Date", "Insurable Salary", "Insurance Status",
             "Insurance Office", "Reference Number", "Notes"],
            Headers(workbook, "Insurance"));
    }

    [Fact]
    public void Absence_template_headers_match_import_fields()
    {
        using var workbook = new XLWorkbook(new MemoryStream(HrImportWorkbookBuilder.BuildAbsence()));
        Assert.Equal(
            ["Employee Number", "Employee Name", "National ID", "Mobile Number",
             "Absence Date", "Absence Type", "Reason", "Notes", "Status"],
            Headers(workbook, "Absences"));
    }

    [Fact]
    public void Attendance_template_includes_check_in_out_and_fingerprint_sheets()
    {
        using var workbook = new XLWorkbook(new MemoryStream(HrImportWorkbookBuilder.BuildAttendance()));
        Assert.Equal(
            ["Employee Number", "Employee Name", "Attendance Date", "Check In", "Check Out"],
            Headers(workbook, "Attendance"));
        Assert.Equal(
            ["Name", "No.", "Date/Time", "Date", "Time", "AM-PM"],
            Headers(workbook, "Fingerprint"));
    }

    private static string[] Headers(XLWorkbook workbook, string sheetName) =>
        workbook.Worksheet(sheetName).Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray();
}
