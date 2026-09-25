using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using MIS.Application.Common;

namespace MIS.Infrastructure.Services;

internal static class BankPortfolioFileInspector
{
    public const int MaximumRows = 100_000;
    public const int MaximumColumns = 200;

    static BankPortfolioFileInspector() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static async Task<int> CountRowsAsync(Stream stream, string extension, CancellationToken token)
    {
        if (!stream.CanSeek) throw new HrValidationException("The uploaded portfolio file must be readable.");
        stream.Position = 0;
        return extension switch
        {
            ".csv" => await CountCsvRowsAsync(stream, token),
            ".xlsx" => CountWorkbookRows(stream, false, token),
            ".xls" => CountWorkbookRows(stream, true, token),
            _ => throw new HrValidationException("Only XLSX, XLS, and CSV portfolio files are supported.")
        };
    }

    private static async Task<int> CountCsvRowsAsync(Stream stream, CancellationToken token)
    {
        var prefix = new byte[Math.Min(4096, (int)stream.Length)];
        _ = await stream.ReadAsync(prefix, token);
        if (prefix.Any(value => value == 0)) throw new HrValidationException("The CSV file contains unreadable binary data.");
        stream.Position = 0;
        using var text = new StreamReader(stream, new UTF8Encoding(false, true), true, leaveOpen: true);
        using var csv = new CsvReader(text, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = _ => throw new HrValidationException("The CSV file contains malformed data."),
            MissingFieldFound = null,
            DetectDelimiter = true,
            TrimOptions = TrimOptions.Trim
        });
        if (!await csv.ReadAsync() || !csv.ReadHeader()) throw new HrValidationException("The portfolio file must contain a header row.");
        ValidateColumnCount(csv.HeaderRecord?.Length ?? 0);
        var count = 0;
        while (await csv.ReadAsync())
        {
            token.ThrowIfCancellationRequested();
            if (csv.Parser.Record?.All(string.IsNullOrWhiteSpace) != false) continue;
            if (++count > MaximumRows) throw new HrValidationException($"Portfolio files cannot exceed {MaximumRows:N0} data rows.");
        }
        return RequireRows(count);
    }

    private static int CountWorkbookRows(Stream stream, bool legacy, CancellationToken token)
    {
        ValidateWorkbookSignature(stream, legacy);
        stream.Position = 0;
        using var reader = legacy
            ? ExcelReaderFactory.CreateBinaryReader(stream, new ExcelReaderConfiguration { LeaveOpen = true })
            : ExcelReaderFactory.CreateOpenXmlReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
        var count = 0;
        do
        {
            token.ThrowIfCancellationRequested();
            if (CollectionImportParser.IsInstructionSheet(reader.Name)) continue;
            var first = reader.Read();
            if (!first) continue;
            ValidateColumnCount(reader.FieldCount);
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                var populated = Enumerable.Range(0, reader.FieldCount).Any(index => !string.IsNullOrWhiteSpace(reader.GetValue(index)?.ToString()));
                if (!populated) continue;
                if (++count > MaximumRows) throw new HrValidationException($"Portfolio files cannot exceed {MaximumRows:N0} data rows.");
            }
        } while (reader.NextResult());
        return RequireRows(count);
    }

    private static void ValidateWorkbookSignature(Stream stream, bool legacy)
    {
        var signature = new byte[8];
        if (stream.Read(signature, 0, signature.Length) < 4) throw new HrValidationException("The workbook is invalid or unreadable.");
        var valid = legacy
            ? signature.SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 })
            : signature[0] == 0x50 && signature[1] == 0x4B;
        if (!valid) throw new HrValidationException("The workbook file signature does not match its extension.");
    }

    private static void ValidateColumnCount(int count)
    {
        if (count is < 1 or > MaximumColumns) throw new HrValidationException($"Portfolio files must contain between 1 and {MaximumColumns} columns.");
    }

    private static int RequireRows(int count) => count > 0 ? count : throw new HrValidationException("The portfolio file does not contain data rows.");
}
