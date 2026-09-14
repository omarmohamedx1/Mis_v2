using System.Text;
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
}
