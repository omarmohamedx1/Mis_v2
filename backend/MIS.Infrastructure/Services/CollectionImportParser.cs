using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using MIS.Application.Common;

namespace MIS.Infrastructure.Services;

internal sealed record ParsedCollectionRow(
    int RowNumber,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, string> OriginalValues);

internal static class CollectionImportParser
{
    public const int MaximumRows = 20_000;
    public const int MaximumColumns = 100;
    static CollectionImportParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static async Task<IReadOnlyCollection<ParsedCollectionRow>> ParseAsync(Stream stream, string extension, CancellationToken token, IReadOnlyCollection<string>? sheetNames = null)
    {
        if (!stream.CanSeek) throw new HrValidationException("The stored import file must be seekable."); stream.Position = 0;
        return extension switch { ".csv" => await ParseCsvAsync(stream, token), ".xlsx" => ParseWorkbook(stream, false, token, sheetNames), ".xls" => ParseWorkbook(stream, true, token, sheetNames), _ => throw new HrValidationException("Only CSV, XLSX, and XLS collection imports are supported.") };
    }

    internal static bool IsInstructionSheet(string? name) =>
        !string.IsNullOrWhiteSpace(name) && System.Text.RegularExpressions.Regex.IsMatch(name, "instruction|تعليمات|reference data|مرجع", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static async Task<IReadOnlyCollection<ParsedCollectionRow>> ParseCsvAsync(Stream stream, CancellationToken token)
    {
        var prefix = new byte[Math.Min(4096, (int)stream.Length)]; _ = await stream.ReadAsync(prefix, token); if (prefix.Any(x => x == 0)) throw new HrValidationException("The CSV file contains binary data."); stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, leaveOpen: true); using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { BadDataFound = _ => throw new HrValidationException("The CSV file contains malformed data."), MissingFieldFound = null, TrimOptions = TrimOptions.Trim });
        if (!await csv.ReadAsync() || !csv.ReadHeader()) throw new HrValidationException("The CSV file does not contain a header row."); var rawHeaders = csv.HeaderRecord ?? []; var headers = ValidateHeaders(rawHeaders); var rows = new List<ParsedCollectionRow>();
        while (await csv.ReadAsync()) { token.ThrowIfCancellationRequested(); if (rows.Count >= MaximumRows) throw new HrValidationException($"Collection imports cannot exceed {MaximumRows:N0} rows."); var rawValues = rawHeaders.Select((header, index) => (header: Limit(header), value: Limit(csv.GetField(index)))).ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase); var values = headers.Select((header, index) => (header, value: rawValues[Limit(rawHeaders[index])])).ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase); if (values.Values.All(string.IsNullOrWhiteSpace)) continue; rows.Add(new ParsedCollectionRow(csv.Parser.Row, values, rawValues)); }
        if (rows.Count == 0) throw new HrValidationException("The import file does not contain data rows."); return rows;
    }

    private static IReadOnlyCollection<ParsedCollectionRow> ParseWorkbook(Stream stream, bool legacy, CancellationToken token, IReadOnlyCollection<string>? sheetNames)
    {
        var signature = new byte[8]; _ = stream.Read(signature);
        var valid = legacy
            ? signature.SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 })
            : signature[0] == 0x50 && signature[1] == 0x4B;
        if (!valid) throw new HrValidationException(legacy ? "The XLS file signature is invalid." : "The XLSX file signature is invalid.");
        stream.Position = 0;
        using var reader = legacy
            ? ExcelReaderFactory.CreateBinaryReader(stream, new ExcelReaderConfiguration { LeaveOpen = true })
            : ExcelReaderFactory.CreateOpenXmlReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
        var wanted = sheetNames?.Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<ParsedCollectionRow>();
        var globalRow = 0;
        var anySheet = false;
        do
        {
            token.ThrowIfCancellationRequested();
            var sheetName = reader.Name ?? string.Empty;
            var selected = wanted is { Count: > 0 }
                ? wanted.Contains(sheetName)
                : !IsInstructionSheet(sheetName);
            if (!selected) continue;
            anySheet = true;
            // Workbooks can contain empty rows before the table, and formatting can
            // extend FieldCount beyond the last column with an actual header.
            string[] rawHeaders = [];
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                rawHeaders = Enumerable.Range(0, reader.FieldCount).Select(i => Limit(Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture))).ToArray();
                if (rawHeaders.Any(header => !string.IsNullOrWhiteSpace(header))) break;
            }
            if (rawHeaders.All(string.IsNullOrWhiteSpace)) continue;
            rawHeaders = rawHeaders.Take(Array.FindLastIndex(rawHeaders, header => !string.IsNullOrWhiteSpace(header)) + 1).ToArray();
            var headers = ValidateHeaders(rawHeaders);
            var rowNumber = 1;
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                rowNumber++;
                if (rows.Count >= MaximumRows) throw new HrValidationException($"Collection imports cannot exceed {MaximumRows:N0} rows.");
                if (Enumerable.Range(rawHeaders.Length, reader.FieldCount - rawHeaders.Length)
                    .Any(index => !string.IsNullOrWhiteSpace(Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture))))
                    throw new HrValidationException("Every import column containing data must have a header.");
                var cellValues = Enumerable.Range(0, rawHeaders.Length)
                    .Select(index => FormatCellValue(reader.GetValue(index), rawHeaders[index], reader.GetNumberFormatString(index)))
                    .ToArray();
                var originalValues = rawHeaders.Select((header, index) => (header, value: cellValues[index]))
                    .ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase);
                var values = headers.Select((header, index) => (header, value: cellValues[index])).ToDictionary(x => x.header, x => x.value, StringComparer.OrdinalIgnoreCase);
                if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
                if (!string.IsNullOrWhiteSpace(sheetName)) values["_sheet"] = sheetName;
                rows.Add(new ParsedCollectionRow(++globalRow, values, originalValues));
            }
        } while (reader.NextResult());
        if (!anySheet) throw new HrValidationException("The selected worksheet was not found.");
        if (rows.Count == 0) throw new HrValidationException("The workbook does not contain data rows.");
        return rows;
    }

    private static string[] ValidateHeaders(string[] headers)
    {
        if (headers.Length == 0 || headers.Length > MaximumColumns) throw new HrValidationException("The import header count is invalid.");
        if (headers.Any(string.IsNullOrWhiteSpace)) throw new HrValidationException("Every import column must have a header.");
        var normalized = headers.Select((header, index) =>
        {
            var key = NormalizeHeader(header);
            return key.Length == 0 ? $"column{index + 1}" : key;
        }).ToArray();
        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length) throw new HrValidationException("Import column headers must be unique.");
        return normalized;
    }

    internal static string NormalizeHeader(string? value) => new((value ?? string.Empty).Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    internal static string FormatCellValue(object? value, string? header, string? numberFormat)
    {
        if (value is null) return string.Empty;
        if (value is DateTime dateTime) return Limit(dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (value is double number)
        {
            var normalizedHeader = NormalizeHeader(header);
            if (normalizedHeader is "تاريخالمحضر" or "تاريخالحكم")
            {
                try { return Limit(DateTime.FromOADate(number).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); }
                catch (ArgumentException) { }
            }

            if (numberFormat is { Length: > 0 } format && format.All(character => character == '0')
                && number >= 0 && number <= long.MaxValue && number == Math.Truncate(number))
                return Limit(Convert.ToInt64(number, CultureInfo.InvariantCulture).ToString($"D{format.Length}", CultureInfo.InvariantCulture));

            var identifierColumns = CollectionFileRowMapper.Columns.Account
                .Concat(CollectionFileRowMapper.Columns.Card)
                .Concat(CollectionFileRowMapper.Columns.CustomerCode)
                .Concat(CollectionFileRowMapper.Columns.National)
                .Concat(CollectionFileRowMapper.Columns.Mobile1)
                .Concat(CollectionFileRowMapper.Columns.Mobile2)
                .Concat(CollectionFileRowMapper.Columns.Mobile3)
                .Concat(CollectionFileRowMapper.Columns.Contract)
                .Select(NormalizeHeader);
            if (identifierColumns.Contains(normalizedHeader, StringComparer.OrdinalIgnoreCase)
                || normalizedHeader is "installmentuniqueid" or "serial" or "رقمالمحضر")
                return Limit(number.ToString("0", CultureInfo.InvariantCulture));
        }

        return Limit(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static string Limit(string? value) { var result = value?.Trim() ?? string.Empty; if (result.Length > 4000) throw new HrValidationException("An import cell exceeds 4,000 characters."); return result; }
}
