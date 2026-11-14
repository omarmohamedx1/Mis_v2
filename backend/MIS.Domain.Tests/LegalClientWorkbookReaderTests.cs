using System.Text;
using ClosedXML.Excel;
using MIS.Application.Common;
using MIS.Application.DTOs.Legal;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class LegalClientWorkbookReaderTests
{
    [Fact]
    public async Task Opens_unknown_headers_and_reads_only_the_selected_sheet_and_header_row()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Instructions").Cell(1, 1).Value = "Instructions only";
        var sheet = workbook.AddWorksheet("Customers");
        sheet.Cell(1, 1).Value = "October report";
        sheet.Cell(3, 1).Value = "Customer as printed";
        sheet.Cell(3, 2).Value = "Agreement ref";
        sheet.Cell(3, 3).Value = "Legal detail";
        sheet.Cell(4, 1).Value = "عميل تجريبي";
        sheet.Cell(4, 2).Value = 123;
        sheet.Cell(4, 2).Style.NumberFormat.Format = "000000";
        sheet.Cell(4, 3).Value = "Original detail retained";
        using var stream = Save(workbook);

        var sheets = await LegalClientWorkbookReader.InspectAsync(stream, ".xlsx", default);
        Assert.Equal(2, sheets.Count);
        Assert.Equal("October report", sheets[1].Rows[0][0]);

        var mapping = new LegalClientColumnMapping("Customers", 3, new Dictionary<string, string>
        {
            ["name"] = "Customer as printed", ["externalIdentifier"] = "Agreement ref",
        });
        var row = Assert.Single(await LegalClientWorkbookReader.ParseAsync(stream, ".xlsx", mapping, default));
        var source = LegalClientService.MapSelectedColumns(row, mapping);
        Assert.Equal(4, source.RowNumber);
        Assert.Equal("عميل تجريبي", source.Name);
        Assert.Equal("000123", source.ExternalIdentifier);
        Assert.Null(source.NationalId);
        Assert.Equal("Original detail retained", source.OriginalFields!["Legal detail"]);
    }

    [Fact]
    public async Task Blank_and_duplicate_headers_do_not_prevent_opening_or_lose_values()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(2, 1).Value = "Client";
        sheet.Cell(2, 2).Value = "Other name";
        sheet.Cell(2, 3).Value = "001234";
        using var stream = Save(workbook);
        var mapping = new LegalClientColumnMapping("Data", 1, new Dictionary<string, string>
        {
            ["name"] = "Name", ["externalIdentifier"] = "Column 3",
        });

        var row = Assert.Single(await LegalClientWorkbookReader.ParseAsync(stream, ".xlsx", mapping, default));
        Assert.Equal("Client", row.OriginalValues["Name"]);
        Assert.Equal("Other name", row.OriginalValues["Name (2)"]);
        Assert.Equal("001234", row.OriginalValues["Column 3"]);
    }

    [Fact]
    public async Task Csv_supports_semicolons_and_keeps_cells_under_an_unnamed_header()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Person;Reference\r\nClient;00012;Extra detail\r\n"));
        var mapping = new LegalClientColumnMapping(null, 1, new Dictionary<string, string>
        {
            ["name"] = "Person", ["externalIdentifier"] = "Reference", ["notes"] = "Column 3",
        });
        var row = Assert.Single(await LegalClientWorkbookReader.ParseAsync(stream, ".csv", mapping, default));
        var source = LegalClientService.MapSelectedColumns(row, mapping);
        Assert.Equal("00012", source.ExternalIdentifier);
        Assert.Equal("Extra detail", source.Notes);
    }

    [Fact]
    public async Task Validation_reads_beyond_the_inspection_sample()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "Person";
        sheet.Cell(1, 2).Value = "Reference";
        for (var row = 2; row <= 31; row++)
        {
            sheet.Cell(row, 1).Value = $"Client {row}";
            sheet.Cell(row, 2).Value = $"ID-{row}";
        }
        using var stream = Save(workbook);
        Assert.Equal(25, Assert.Single(await LegalClientWorkbookReader.InspectAsync(stream, ".xlsx", default)).Rows.Count);
        var mapping = new LegalClientColumnMapping("Data", 1, new Dictionary<string, string> { ["name"] = "Person", ["externalIdentifier"] = "Reference" });
        var parsed = await LegalClientWorkbookReader.ParseAsync(stream, ".xlsx", mapping, default);
        Assert.Equal(30, parsed.Count);
        Assert.Equal(31, parsed.Last().RowNumber);
    }

    [Fact]
    public async Task Rejects_a_mapping_to_a_column_that_is_not_in_the_selected_header()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name,Phone\r\nClient,01001234567\r\n"));
        var mapping = new LegalClientColumnMapping(null, 1, new Dictionary<string, string> { ["name"] = "Missing" });
        await Assert.ThrowsAsync<HrValidationException>(() => LegalClientWorkbookReader.ParseAsync(stream, ".csv", mapping, default));
    }

    [Fact]
    public async Task Corrupt_workbooks_return_a_validation_error_instead_of_a_server_error()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Not an Excel workbook"));
        await Assert.ThrowsAsync<HrValidationException>(() => LegalClientWorkbookReader.InspectAsync(stream, ".xlsx", default));
    }

    [Fact]
    public async Task A_column_cannot_be_mapped_to_two_different_customer_fields()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name,Phone\r\nClient,01001234567\r\n"));
        var mapping = new LegalClientColumnMapping(null, 1, new Dictionary<string, string> { ["name"] = "Name", ["nationalId"] = "Name" });
        await Assert.ThrowsAsync<HrValidationException>(() => LegalClientWorkbookReader.ParseAsync(stream, ".csv", mapping, default));
    }

    [Fact]
    public async Task Retains_selected_extra_columns_with_their_values_and_always_keeps_mapped_columns()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name,Account,Balance,Branch,Unneeded\r\nClient,00012,2500,Cairo,Skip this\r\n"));
        var mapping = new LegalClientColumnMapping(null, 1, new Dictionary<string, string>
        {
            ["name"] = "Name", ["externalIdentifier"] = "Account",
        }, ["Balance", "Branch"]);

        var row = Assert.Single(await LegalClientWorkbookReader.ParseAsync(stream, ".csv", mapping, default));
        var source = LegalClientService.MapSelectedColumns(row, mapping);

        Assert.Equal("Client", source.Name);
        Assert.Equal("00012", source.ExternalIdentifier);
        Assert.Equal(4, source.OriginalFields!.Count);
        Assert.Equal("2500", source.OriginalFields["Balance"]);
        Assert.Equal("Cairo", source.OriginalFields["Branch"]);
        Assert.Equal("00012", source.OriginalFields["Account"]);
        Assert.DoesNotContain("Unneeded", source.OriginalFields.Keys);
    }

    [Fact]
    public async Task Deselecting_all_extras_keeps_client_fields_and_excludes_only_unmapped_columns()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name,Account,Balance\r\nClient,00012,2500\r\n"));
        var mapping = new LegalClientColumnMapping(null, 1, new Dictionary<string, string>
        {
            ["name"] = "Name", ["externalIdentifier"] = "Account",
        }, []);
        var row = Assert.Single(await LegalClientWorkbookReader.ParseAsync(stream, ".csv", mapping, default));
        var source = LegalClientService.MapSelectedColumns(row, mapping);

        Assert.Equal("00012", source.ExternalIdentifier);
        Assert.Equal(2, source.OriginalFields!.Count);
        Assert.DoesNotContain("Balance", source.OriginalFields.Keys);
    }

    [Fact]
    public async Task Rejects_a_selected_extra_column_that_does_not_exist()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name,Account\r\nClient,00012\r\n"));
        var mapping = new LegalClientColumnMapping(null, 1, new Dictionary<string, string> { ["name"] = "Name" }, ["Missing extra"]);
        await Assert.ThrowsAsync<HrValidationException>(() => LegalClientWorkbookReader.ParseAsync(stream, ".csv", mapping, default));
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
