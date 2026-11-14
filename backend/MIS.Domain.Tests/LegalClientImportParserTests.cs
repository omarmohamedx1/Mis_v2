using ClosedXML.Excel;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalClientImportParserTests
{
    private static readonly string[] Headers =
    [
        "#", "Branches", "ID Number", "Installment Unique ID", "Cust Full Name", "serial",
        "تاريخ المحضر", "رقم المحضر", "النيابة", "تاريخ الحكم", "الحكم", "Agency",
    ];

    [Fact]
    public async Task Parses_the_legal_workbook_headers_and_preserves_original_values()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("ورقة1");
        WriteHeaders(sheet);

        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 1).Style.NumberFormat.Format = "000";
        sheet.Cell(2, 2).Value = "BANK";
        sheet.Cell(2, 3).Value = string.Empty;
        sheet.Cell(2, 4).Value = "00001234";
        sheet.Cell(2, 5).Value = "عميل تجريبي";
        sheet.Cell(2, 6).Value = 23;
        sheet.Cell(2, 6).Style.NumberFormat.Format = "0000";
        sheet.Cell(2, 7).Value = new DateTime(2023, 8, 10);
        sheet.Cell(2, 7).Style.DateFormat.Format = "dd-MMM-yyyy";
        sheet.Cell(2, 8).Value = "000056";
        sheet.Cell(2, 9).Value = "النيابة";
        sheet.Cell(2, 10).Value = "10-Aug-2023";
        sheet.Cell(2, 11).Value = "حكم تجريبي";
        sheet.Cell(2, 12).Value = "Agency A";

        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var parsed = await CollectionImportParser.ParseAsync(content, ".xlsx", default);
        var row = Assert.Single(parsed);
        var source = LegalClientService.MapImportRow(row, 1);

        Assert.Equal("عميل تجريبي", source.Name);
        Assert.Null(source.NationalId);
        Assert.Null(source.BankName);
        Assert.Equal("00001234", source.ExternalIdentifier);
        Assert.Equal("BANK", source.OriginalFields!["Branches"]);
        Assert.Equal("Agency A", source.OriginalFields["Agency"]);
        Assert.Equal("001", source.OriginalFields!["#"]);
        Assert.Equal("0023", source.OriginalFields["serial"]);
        Assert.Equal("2023-08-10", source.OriginalFields["تاريخ المحضر"]);
        Assert.Equal("10-Aug-2023", source.OriginalFields["تاريخ الحكم"]);
        Assert.Equal(Headers.Length, source.OriginalFields.Count);
    }

    [Fact]
    public async Task Parses_277_rows_across_worksheets_and_maps_the_legal_columns()
    {
        using var workbook = new XLWorkbook();
        var firstSheet = workbook.AddWorksheet("Clients 1");
        var secondSheet = workbook.AddWorksheet("Clients 2");
        WriteHeaders(firstSheet);
        WriteHeaders(secondSheet);
        for (var index = 0; index < 277; index++)
        {
            var sheet = index < 139 ? firstSheet : secondSheet;
            var sheetRow = index < 139 ? index + 2 : index - 137;
            WriteLegalRow(sheet, sheetRow, index);
        }

        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var parsed = await CollectionImportParser.ParseAsync(content, ".xlsx", default);
        var sources = parsed.Select(row => LegalClientService.MapImportRow(row, 1)).ToArray();

        Assert.Equal(277, sources.Length);
        Assert.All(sources, source =>
        {
            Assert.Null(source.NationalId);
            Assert.NotNull(source.ExternalIdentifier);
            Assert.NotNull(source.Name);
            Assert.Equal(Headers.Length, source.OriginalFields!.Count);
            Assert.Equal("Branch source", source.OriginalFields["Branches"]);
            Assert.Equal("Agency source", source.OriginalFields["Agency"]);
        });
        Assert.Equal("2023-08-10", sources[0].OriginalFields!["تاريخ المحضر"]);
        Assert.Equal("2023-08-11", sources[1].OriginalFields!["تاريخ المحضر"]);
        Assert.Equal("12/08/2023", sources[2].OriginalFields!["تاريخ المحضر"]);
        Assert.Equal("00000001", sources[0].ExternalIdentifier);
        var firstOriginalFields = sources[0].OriginalFields ?? throw new InvalidOperationException();
        Assert.Equal("Report 1", firstOriginalFields["رقم المحضر"]);
        Assert.Equal("Prosecution", firstOriginalFields["النيابة"]);
        Assert.Equal("Judgment", firstOriginalFields["الحكم"]);
        Assert.Equal("10/08/2023", firstOriginalFields["تاريخ الحكم"]);
        Assert.Null(sources[0].BankName);
    }

    [Fact]
    public async Task Parses_workbook_with_leading_empty_rows_and_trailing_formatted_columns()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Clients");
        for (var index = 0; index < Headers.Length; index++)
            sheet.Cell(3, index + 1).Value = Headers[index];
        WriteLegalRow(sheet, 4, 0);
        sheet.Cell(4, 15).Style.Fill.BackgroundColor = XLColor.White;
        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var source = LegalClientService.MapImportRow(
            Assert.Single(await CollectionImportParser.ParseAsync(content, ".xlsx", default)), 1);

        Assert.Equal("Customer 1", source.Name);
        Assert.Equal("00000001", source.ExternalIdentifier);
        Assert.Equal(Headers.Length, source.OriginalFields!.Count);
    }

    [Fact]
    public async Task Rejects_data_in_a_trailing_column_without_a_header()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Clients");
        WriteHeaders(sheet);
        WriteLegalRow(sheet, 2, 0);
        sheet.Cell(2, 13).Value = "Must not be silently discarded";
        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        await Assert.ThrowsAsync<MIS.Application.Common.HrValidationException>(
            () => CollectionImportParser.ParseAsync(content, ".xlsx", default));
    }

    private static void WriteHeaders(IXLWorksheet sheet)
    {
        for (var index = 0; index < Headers.Length; index++)
            sheet.Cell(1, index + 1).Value = Headers[index];
    }

    private static void WriteLegalRow(IXLWorksheet sheet, int row, int index)
    {
        sheet.Cell(row, 1).Value = index + 1;
        sheet.Cell(row, 2).Value = "Branch source";
        sheet.Cell(row, 3).Value = string.Empty;
        sheet.Cell(row, 4).Value = (index + 1).ToString("D8");
        sheet.Cell(row, 5).Value = $"Customer {index + 1}";
        sheet.Cell(row, 6).Value = index + 1;
        switch (index % 3)
        {
            case 0:
                sheet.Cell(row, 7).Value = new DateTime(2023, 8, 10);
                sheet.Cell(row, 7).Style.DateFormat.Format = "dd-MMM-yyyy";
                break;
            case 1:
                sheet.Cell(row, 7).Value = "2023-08-11";
                break;
            default:
                sheet.Cell(row, 7).Value = "12/08/2023";
                break;
        }
        sheet.Cell(row, 8).Value = $"Report {index + 1}";
        sheet.Cell(row, 9).Value = "Prosecution";
        sheet.Cell(row, 10).Value = "10/08/2023";
        sheet.Cell(row, 11).Value = "Judgment";
        sheet.Cell(row, 12).Value = "Agency source";
    }
}
