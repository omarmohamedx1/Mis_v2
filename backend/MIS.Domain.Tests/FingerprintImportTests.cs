using System.Text;
using ClosedXML.Excel;
using MIS.Application.DTOs.Hr;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class FingerprintImportTests
{
    private static AttendanceImportParser Parser() => new(new HrCalendarService(null!, null!, null!));
    private static AttendanceImportColumnMappingRequest Mapping(bool combined = false) => new()
    {
        Layout = "PunchRows", EmployeeNumberColumn = "No.", AttendanceDateColumn = combined ? null : "Date",
        TimeColumn = combined ? null : "Time", AmPmColumn = combined ? null : "AM-PM",
        PunchDateTimeColumn = combined ? "Date/Time" : null,
        DateFormat = "dd-MMM-yy", TimeFormat = combined ? "h:mm:ss tt" : "h:mm:ss",
        TimeSystem = "12-hour", TimeZoneId = "Africa/Cairo"
    };

    [Theory]
    [InlineData("10:09:32", "AM", 10, 9, 32)]
    [InlineData("1:23:41", "PM", 13, 23, 41)]
    [InlineData("12:49:32", "PM", 12, 49, 32)]
    [InlineData("12:05:00", "AM", 0, 5, 0)]
    [InlineData("5:30:00", "م", 17, 30, 0)]
    public async Task Separate_marker_and_single_punch(string time, string marker, int hour, int minute, int second)
    {
        using var stream = Csv($"999,70,,31-Jul-26,{time},{marker}");
        var row = Assert.Single((await Parser().ParseAsync(stream, ".csv", Mapping(), default)).Groups);
        Assert.Empty(row.Errors);
        Assert.Equal("70", row.SourceEmployeeNumber);
        Assert.Null(row.SourceEmployeeName);
        Assert.Equal(new DateOnly(2026, 7, 31), row.AttendanceDate);
        Assert.Equal(new TimeOnly(hour, minute, second), TimeOnly.FromDateTime(row.CheckIn!.Value.DateTime));
        Assert.Null(row.CheckOut);
        Assert.Single(row.Punches);
        Assert.Equal(marker, Assert.Single(row.SourceRows)["AM-PM"]);
    }

    [Fact]
    public async Task Unsorted_punches_group_into_one_day_and_preserve_raw_rows()
    {
        using var stream = Csv("70,70,,04-Aug-26,5:31:00,PM\n70,70,,04-Aug-26,9:02:00,AM\n70,70,,04-Aug-26,2:00:00,PM\n70,70,,04-Aug-26,1:15:00,PM");
        var row = Assert.Single((await Parser().ParseAsync(stream, ".csv", Mapping(), default)).Groups);
        Assert.Empty(row.Errors);
        Assert.Equal(9, row.CheckIn!.Value.Hour);
        Assert.Equal(17, row.CheckOut!.Value.Hour);
        Assert.Equal(4, row.Punches.Count);
        Assert.Equal(4, row.SourceRows.Count);
    }

    [Theory]
    [InlineData("AM")]
    [InlineData("PM")]
    public async Task Combined_date_time_remains_supported(string marker)
    {
        using var stream = Csv($"11,11,17-Aug-26 1:23:41 {marker},17-Aug-26,1:23:41,{marker}");
        var row = Assert.Single((await Parser().ParseAsync(stream, ".csv", Mapping(true), default)).Groups);
        Assert.Empty(row.Errors);
        Assert.Equal(marker == "PM" ? 13 : 1, row.CheckIn!.Value.Hour);
        Assert.Equal(new DateOnly(2026, 8, 17), row.AttendanceDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("XM")]
    public async Task Invalid_marker_is_not_ignored(string marker)
    {
        using var stream = Csv($"70,70,,31-Jul-26,1:23:41,{marker}");
        var row = Assert.Single((await Parser().ParseAsync(stream, ".csv", Mapping(), default)).Groups);
        Assert.Contains("AM/PM value is missing or invalid.", row.Errors);
        Assert.Empty(row.Punches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_Excel_structure_supports_text_and_native_cells(bool native)
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Fingerprint");
            var headers = new[] { "Name", "No.", "Date/Time", "Date", "Time", "AM-PM" };
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(2, 1).Value = 999;
            sheet.Cell(2, 2).Value = 70;
            sheet.Cell(2, 3).Value = "31-Jul-26 1:23:41 PM";
            sheet.Cell(2, 6).Value = "PM";
            if (native)
            {
                sheet.Cell(2, 4).Value = new DateTime(2026, 7, 31);
                sheet.Cell(2, 4).Style.DateFormat.Format = "dd-mmm-yy";
                sheet.Cell(2, 5).Value = new TimeSpan(1, 23, 41).TotalDays;
                sheet.Cell(2, 5).Style.DateFormat.Format = "h:mm:ss";
            }
            else
            {
                sheet.Cell(2, 4).Value = "31-Jul-26";
                sheet.Cell(2, 5).Value = "1:23:41";
            }
            workbook.SaveAs(stream);
        }
        var row = Assert.Single((await Parser().ParseAsync(stream, ".xlsx", Mapping(), default)).Groups);
        Assert.Empty(row.Errors);
        Assert.Equal("70", row.SourceEmployeeNumber);
        Assert.Equal(13, row.CheckIn!.Value.Hour);
        Assert.Equal(23, row.CheckIn.Value.Minute);
    }

    [Fact]
    public async Task Duplicate_single_punch_does_not_become_checkout()
    {
        using var stream = Csv("70,70,,31-Jul-26,1:23:41,PM\n70,70,,31-Jul-26,1:23:41,PM");
        var row = Assert.Single((await Parser().ParseAsync(stream, ".csv", Mapping(), default)).Groups);
        Assert.Single(row.Punches);
        Assert.Null(row.CheckOut);
        Assert.Equal(2, row.SourceRows.Count);
    }

    [Fact]
    public async Task Invalid_clock_is_not_reinterpreted_as_valid_morning_time()
    {
        using var stream = Csv("70,70,,31-Jul-26,13:23:41,AM");
        var row = Assert.Single((await Parser().ParseAsync(stream, ".csv", Mapping(), default)).Groups);
        Assert.NotEmpty(row.Errors);
        Assert.Empty(row.Punches);
    }

    private static MemoryStream Csv(string rows) => new(Encoding.UTF8.GetBytes("Name,No.,Date/Time,Date,Time,AM-PM\n" + rows + "\n"));
}
