using System.Text;
using ClosedXML.Excel;
using MIS.Application.Common;
using MIS.Infrastructure.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionImportParserTests
{
    [Fact]
    public async Task CsvParser_NormalizesArabicAndEnglishHeaders()
    {
        const string csv = "Account Reference,كود العميل,Customer Name,Outstanding Balance,Overdue Balance,DPD\r\nACC-1,C-1,Test Customer,1000.50,200,31\r\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var rows = await CollectionImportParser.ParseAsync(stream, ".csv", CancellationToken.None);
        var row = Assert.Single(rows);
        Assert.Equal("ACC-1", row.Values["accountreference"]);
        Assert.Equal("C-1", row.Values["كودالعميل"]);
    }

    [Fact]
    public async Task CsvParser_RejectsBinaryData()
    {
        await using var stream = new MemoryStream([0x41, 0x00, 0x42, 0x0A]);
        await Assert.ThrowsAsync<HrValidationException>(() => CollectionImportParser.ParseAsync(stream, ".csv", CancellationToken.None));
    }

    [Fact]
    public async Task Parser_RejectsUnsupportedFormats()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));
        await Assert.ThrowsAsync<HrValidationException>(() => CollectionImportParser.ParseAsync(stream, ".txt", CancellationToken.None));
    }

    [Fact]
    public async Task CsvParser_PreservesLeadingZerosInIdentifierColumns()
    {
        const string csv = "Account No,Customer Name,Current Balance,BKT,National ID,Mobile\r\n0000123,Test Customer,100,BKT 1,00123456789012,01001234567\r\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var rows = await CollectionImportParser.ParseAsync(stream, ".csv", CancellationToken.None);
        var row = Assert.Single(rows);
        var mapped = CollectionFileRowMapper.Read(row);

        Assert.Equal("0000123", mapped.Account);
        Assert.Equal("00123456789012", mapped.NationalId);
        Assert.Equal("01001234567", mapped.Mobile1);
    }

    [Fact]
    public async Task XlsxParserReadsLocalizedHeadersAndTextIdentifiers()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Loans");
        sheet.Cell(1, 1).Value = "الرصيد الحالي";
        sheet.Cell(1, 2).Value = "اسم العميل";
        sheet.Cell(1, 3).Value = "Acct No";
        sheet.Cell(1, 4).Value = "BKT";
        sheet.Cell(2, 1).Value = "250.50";
        sheet.Cell(2, 2).Value = "عميل تجريبي";
        sheet.Cell(2, 3).Value = "00009876";
        sheet.Cell(2, 4).Value = "BKT 1";
        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var rows = await CollectionImportParser.ParseAsync(stream, ".xlsx", CancellationToken.None);
        var row = Assert.Single(rows);
        var mapped = CollectionFileRowMapper.Read(row);

        Assert.Equal("00009876", mapped.Account);
        Assert.Equal("عميل تجريبي", mapped.NameArabic);
        Assert.Equal(250.50m, mapped.Outstanding);
        Assert.Empty(CollectionFileRowMapper.MissingRequiredImportColumns(row.OriginalValues.Keys));
    }
}
