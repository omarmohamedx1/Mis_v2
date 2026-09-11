using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using MIS.Application.DTOs.Hr;
using MIS.Domain.Entities;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class AttendanceImportParserTests
{
    private static AttendanceImportParser Parser() => new(new HrCalendarService(null!, null!, null!));

    public static IEnumerable<object[]> ClockCases()
    {
        var cases = new (string Source, string Expected)[]
        {
            ("09:00 AM", "09:00:00"), ("09:00 PM", "21:00:00"),
            ("12:00 AM", "00:00:00"), ("12:00 PM", "12:00:00"),
            ("1:30 PM", "13:30:00"), ("01:30:25 PM", "13:30:25"),
            ("09:00 ص", "09:00:00"), ("05:30 م", "17:30:00"),
            ("17:30", "17:30:00"), ("08:00 AM", "08:00:00"),
            ("06:45 PM", "18:45:00"), ("05:30 pm", "17:30:00")
        };
        foreach (var extension in new[] { ".csv", ".xlsx" })
        foreach (var system in new[] { "24-hour", "12-hour" })
        foreach (var (source, expected) in cases)
            yield return [extension, system, source, expected];
    }

    [Theory]
    [MemberData(nameof(ClockCases))]
    public async Task Both_columns_parse_and_normalize_without_database_writes(string extension, string system, string source, string expected)
    {
        using var file = File(extension, source, source);
        var parsed = await Parser().ParseAsync(file, extension, Mapping(system), default);
        var row = Assert.Single(parsed.Groups);
        Assert.Empty(row.Errors);
        Assert.Equal(expected, row.CheckIn!.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        Assert.Equal(expected, row.CheckOut!.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        Assert.Equal(new DateOnly(2026, 9, 5), row.AttendanceDate);
        Assert.Equal(source, Assert.Single(row.SourceRows)["CheckIn"]);

        // Exercise the existing persistence entity's UTC normalization in memory only.
        var stored = new AttendanceImportRow(Guid.NewGuid(), "[2]", "[]", "TEST", null, Guid.NewGuid(),
            row.AttendanceDate, row.CheckIn, row.CheckOut, "[]", "[]", "[]", true, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, stored.CheckIn!.Value.Offset);
        Assert.Equal(row.CheckIn.Value.ToUniversalTime(), stored.CheckIn);
        Assert.Equal(row.CheckOut.Value.ToUniversalTime(), stored.CheckOut);
    }

    [Theory]
    [InlineData("h:mm tt")]
    [InlineData("hh:mm tt")]
    [InlineData("h:mm:ss tt")]
    [InlineData("hh:mm:ss tt")]
    public async Task Common_formats_and_Arabic_culture_support_English_and_Arabic_markers(string format)
    {
        using var file = File(".csv", "09:15 AM", "05:30 م");
        var row = Assert.Single((await Parser().ParseAsync(file, ".csv", Mapping("12-hour", format, "ar-EG"), default)).Groups);
        Assert.Empty(row.Errors);
        Assert.Equal(9, row.CheckIn!.Value.Hour);
        Assert.Equal(17, row.CheckOut!.Value.Hour);
        Assert.Equal(30, row.CheckOut.Value.Minute);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("13:00 PM")]
    [InlineData("09:99 AM")]
    [InlineData("24:00")]
    [InlineData("05:30 XM")]
    public async Task Invalid_times_are_errors_on_both_columns(string invalid)
    {
        using var file = File(".csv", invalid, invalid);
        var row = Assert.Single((await Parser().ParseAsync(file, ".csv", Mapping("12-hour"), default)).Groups);
        Assert.Contains("Invalid check-in time.", row.Errors);
        Assert.Contains("Invalid check-out time.", row.Errors);
        Assert.Null(row.CheckIn);
        Assert.Null(row.CheckOut);
    }

    [Theory]
    [InlineData(".csv", "09:00 AM", "12-hour")]
    [InlineData(".xlsx", "05:30 م", "12-hour")]
    [InlineData(".csv", "17:30", "24-hour")]
    public async Task Inspection_suggests_time_system(string extension, string value, string expected)
    {
        using var file = File(extension, value, value);
        Assert.Equal(expected, Assert.Single(await Parser().InspectAsync(file, extension, default)).SuggestedTimeSystem);
    }

    [Fact]
    public async Task Native_Excel_time_cells_and_overnight_checkout_remain_supported()
    {
        using var file = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Attendance");
            Headers(sheet);
            sheet.Cell(2, 1).Value = "TEST";
            sheet.Cell(2, 2).Value = new DateTime(2026, 9, 5);
            sheet.Cell(2, 2).Style.DateFormat.Format = "dd/MM/yyyy";
            sheet.Cell(2, 3).Value = 21d / 24;
            sheet.Cell(2, 3).Style.DateFormat.Format = "hh:mm AM/PM";
            sheet.Cell(2, 4).Value = 6d / 24;
            sheet.Cell(2, 4).Style.DateFormat.Format = "hh:mm AM/PM";
            workbook.SaveAs(file);
        }
        var row = Assert.Single((await Parser().ParseAsync(file, ".xlsx", Mapping("12-hour"), default)).Groups);
        Assert.Empty(row.Errors);
        Assert.Equal(21, row.CheckIn!.Value.Hour);
        Assert.Equal(6, row.CheckOut!.Value.Hour);
        Assert.Equal(row.CheckIn.Value.Date.AddDays(1), row.CheckOut.Value.Date);
    }

    private static AttendanceImportColumnMappingRequest Mapping(string system, string? format = null, string? culture = null) => new()
    {
        EmployeeNumberColumn = "Employee", AttendanceDateColumn = "Date", CheckInColumn = "CheckIn",
        CheckOutColumn = "CheckOut", DateFormat = "dd/MM/yyyy", TimeSystem = system,
        TimeFormat = format, CultureName = culture, TimeZoneId = "Africa/Cairo"
    };

    private static MemoryStream File(string extension, string checkIn, string checkOut)
    {
        if (extension == ".csv")
            return new MemoryStream(Encoding.UTF8.GetBytes($"Employee,Date,CheckIn,CheckOut\nTEST,05/09/2026,{checkIn},{checkOut}\n"));
        var stream = new MemoryStream();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Attendance");
        Headers(sheet);
        sheet.Cell(2, 1).Value = "TEST";
        sheet.Cell(2, 2).Value = "05/09/2026";
        sheet.Cell(2, 3).Value = checkIn;
        sheet.Cell(2, 4).Value = checkOut;
        workbook.SaveAs(stream);
        return stream;
    }

    private static void Headers(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "Employee";
        sheet.Cell(1, 2).Value = "Date";
        sheet.Cell(1, 3).Value = "CheckIn";
        sheet.Cell(1, 4).Value = "CheckOut";
    }
}
