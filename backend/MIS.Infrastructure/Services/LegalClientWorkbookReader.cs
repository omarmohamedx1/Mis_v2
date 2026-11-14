using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using MIS.Application.Common;
using MIS.Application.DTOs.Legal;

namespace MIS.Infrastructure.Services;

// Reading the table is independent of customer validation: an unfamiliar column
// name must not prevent the user from opening the file and mapping its fields.
internal static class LegalClientWorkbookReader
{
    internal const int InspectionRows = 25;
    private const int MaximumRows = 5_000;
    private const int MaximumColumns = 100;

    static LegalClientWorkbookReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    internal static string[] BuildHeaders(IReadOnlyList<string> cells)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return cells.Select((cell, index) =>
        {
            var basis = string.IsNullOrWhiteSpace(cell) ? $"Column {index + 1}" : cell.Trim();
            var header = basis;
            for (var suffix = 2; !used.Add(header); suffix++) header = $"{basis} ({suffix})";
            return header;
        }).ToArray();
    }

    internal static async Task<IReadOnlyList<LegalClientWorkbookSheetDto>> InspectAsync(Stream stream, string extension, CancellationToken token)
    {
        var sheets = new List<LegalClientWorkbookSheetDto>();
        await ReadAsync(stream, extension, null, true, (name, _, cells) =>
        {
            if (sheets.Count == 0 || sheets[^1].Name != name)
                sheets.Add(new LegalClientWorkbookSheetDto(name, new List<string[]>()));
            ((List<string[]>)sheets[^1].Rows).Add(cells);
        }, token);
        if (sheets.Count == 0 || sheets.All(sheet => sheet.Rows.All(row => row.All(string.IsNullOrWhiteSpace))))
            throw new HrValidationException("The workbook does not contain data rows.");
        return sheets;
    }

    internal static async Task<IReadOnlyCollection<ParsedCollectionRow>> ParseAsync(
        Stream stream, string extension, LegalClientColumnMapping mapping, CancellationToken token)
    {
        if (mapping.HeaderRow is < 1 or > InspectionRows || mapping.Columns is null)
            throw new HrValidationException("Select a header row from the workbook preview.");
        var selected = mapping.Columns.Values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (selected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
            throw new HrValidationException("Select a different workbook column for each client field.");
        var csvWidth = extension == ".csv"
            ? (await InspectAsync(stream, extension, token)).SelectMany(sheet => sheet.Rows).Max(row => row.Length)
            : 0;
        string[]? headers = null;
        var rows = new List<ParsedCollectionRow>();
        await ReadAsync(stream, extension, mapping.SheetName, false, (_, number, cells) =>
        {
            if (number == mapping.HeaderRow)
            {
                headers = BuildHeaders(cells.Concat(Enumerable.Repeat(string.Empty, Math.Max(0, csvWidth - cells.Length))).ToArray());
                if (mapping.Columns.Values.Any(column => !string.IsNullOrEmpty(column) && !headers.Contains(column, StringComparer.Ordinal)))
                    throw new HrValidationException("A selected column was not found. Read the workbook again.");
                if (mapping.ExtraColumns?.Any(column => !headers.Contains(column, StringComparer.Ordinal)) == true)
                    throw new HrValidationException("A selected column was not found. Read the workbook again.");
            }
            if (number <= mapping.HeaderRow || cells.All(string.IsNullOrWhiteSpace)) return;
            if (headers is null) throw new HrValidationException("The selected header row was not found.");
            if (rows.Count >= MaximumRows) throw new HrValidationException($"Client imports cannot exceed {MaximumRows:N0} rows.");
            // CSV records may contain extra cells after the header. Keep them with
            // generated names instead of silently dropping customer data.
            if (cells.Length > headers.Length)
                headers = BuildHeaders(headers.Concat(Enumerable.Repeat(string.Empty, cells.Length - headers.Length)).ToArray());
            var original = headers.Select((header, index) => (header, value: index < cells.Length ? cells[index] : string.Empty))
                .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase);
            rows.Add(new ParsedCollectionRow(number, original, original));
        }, token, mapping.HeaderRow);
        if (headers is null || rows.Count == 0) throw new HrValidationException("The selected worksheet does not contain data rows after the header.");
        return rows;
    }

    private static async Task ReadAsync(Stream stream, string extension, string? selectedSheet, bool inspect,
        Action<string?, int, string[]> visit, CancellationToken token, int headerRow = 1)
    {
        try
        {
            await ReadCoreAsync(stream, extension, selectedSheet, inspect, visit, token, headerRow);
        }
        catch (Exception error) when (error is InvalidDataException or DecoderFallbackException or CsvHelperException or ExcelReaderException)
        {
            throw new HrValidationException("The workbook could not be read. Use a valid Excel or UTF-8 CSV file without password protection.");
        }
    }

    private static async Task ReadCoreAsync(Stream stream, string extension, string? selectedSheet, bool inspect,
        Action<string?, int, string[]> visit, CancellationToken token, int headerRow)
    {
        stream.Position = 0;
        if (extension == ".csv")
        {
            using var text = new StreamReader(stream, new UTF8Encoding(false, true), true, leaveOpen: true);
            using var csv = new CsvReader(text, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false, DetectDelimiter = true, DetectDelimiterValues = [",", ";", "\t", "|"],
                IgnoreBlankLines = false, MissingFieldFound = null, TrimOptions = TrimOptions.Trim,
                MaxFieldSize = 4_000,
            });
            var number = 0;
            while (await csv.ReadAsync())
            {
                token.ThrowIfCancellationRequested();
                var cells = csv.Parser.Record ?? [];
                ValidateWidth(cells.Length);
                visit(null, ++number, cells.Select(cell => cell.Trim()).ToArray());
                if (inspect && number >= InspectionRows) break;
            }
            return;
        }

        using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
        var found = inspect;
        do
        {
            if (!inspect && !string.Equals(selectedSheet, reader.Name, StringComparison.Ordinal)) continue;
            found = true;
            var number = 0;
            string[]? headers = null;
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                ValidateWidth(reader.FieldCount);
                var cells = Enumerable.Range(0, reader.FieldCount).Select(index =>
                    CollectionImportParser.FormatCellValue(reader.GetValue(index), headers is not null && index < headers.Length ? headers[index] : null,
                        reader.GetNumberFormatString(index))).ToArray();
                if (++number == headerRow) headers = cells;
                visit(reader.Name, number, cells);
                if (inspect && number >= InspectionRows) break;
            }
        } while (reader.NextResult());
        if (!found) throw new HrValidationException("The selected worksheet was not found.");
    }

    private static void ValidateWidth(int count)
    {
        if (count > MaximumColumns) throw new HrValidationException($"Client imports cannot exceed {MaximumColumns} columns.");
    }
}
