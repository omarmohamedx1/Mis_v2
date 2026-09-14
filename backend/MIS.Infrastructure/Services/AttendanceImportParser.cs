using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using ExcelDataReader;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Domain.Constants;

namespace MIS.Infrastructure.Services;

internal sealed class AttendanceImportParser
{
    public const int MaximumRows = 100_000;
    public const int MaximumGroups = 50_000;
    private const int MaximumColumns = 256;
    private const int MaximumCellCharacters = 4_000;
    private const int InspectionRows = 25;
    private readonly IWorkingCalendarCalculator _calendar;

    static AttendanceImportParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public AttendanceImportParser(IWorkingCalendarCalculator calendar)
    {
        _calendar = calendar;
    }

    public async Task<IReadOnlyCollection<AttendanceImportSheetDto>> InspectAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        Reset(stream);
        if (extension == ".csv")
        {
            var rows = new List<string[]>();
            await foreach (var row in ReadCsvRowsAsync(stream, cancellationToken))
            {
                rows.Add(row.Cells);
                if (rows.Count >= InspectionRows) break;
            }
            var (headerRow, columns) = SelectHeader(rows);
            return [new AttendanceImportSheetDto(null, headerRow, columns, SuggestTimeSystem(rows.Skip(headerRow)))];
        }

        var sheets = new List<AttendanceImportSheetDto>();
        using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = new List<string[]>();
            while (reader.Read() && rows.Count < InspectionRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(ReadExcelCells(reader));
            }
            var (headerRow, columns) = SelectHeader(rows);
            sheets.Add(new AttendanceImportSheetDto(reader.Name, headerRow, columns, SuggestTimeSystem(rows.Skip(headerRow))));
        } while (reader.NextResult());

        if (sheets.Count == 0) throw new HrValidationException("The workbook does not contain any worksheets.");
        return sheets;
    }

    public async Task<ParsedAttendanceImport> ParseAsync(
        Stream stream,
        string extension,
        AttendanceImportColumnMappingRequest mapping,
        CancellationToken cancellationToken)
    {
        ValidateMapping(mapping);
        var culture = ResolveCulture(mapping.CultureName);
        var zone = ResolveTimeZone(mapping.TimeZoneId);
        var accumulator = new PreviewAccumulator(mapping, culture, zone, _calendar);
        Reset(stream);

        if (extension == ".csv")
        {
            string[]? headers = null;
            ResolvedColumns? columns = null;
            var foundHeader = false;
            await foreach (var row in ReadCsvRowsAsync(stream, cancellationToken))
            {
                if (row.RowNumber == mapping.HeaderRowNumber)
                {
                    headers = BuildHeaders(row.Cells);
                    columns = ResolveColumns(headers, mapping);
                    foundHeader = true;
                }
                if (row.RowNumber >= mapping.DataStartRowNumber)
                {
                    if (!foundHeader || columns is null) throw new HrValidationException("The configured CSV header row was not found before the data rows.");
                    accumulator.Add(row.RowNumber, row.Cells, headers!, columns);
                }
            }
            if (!foundHeader) throw new HrValidationException("The configured CSV header row was not found.");
        }
        else
        {
            using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
            var selectedSheetFound = false;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var selected = string.IsNullOrWhiteSpace(mapping.SheetName)
                    ? !selectedSheetFound
                    : string.Equals(reader.Name, mapping.SheetName.Trim(), StringComparison.OrdinalIgnoreCase);
                if (!selected) continue;

                selectedSheetFound = true;
                string[]? headers = null;
                ResolvedColumns? columns = null;
                var rowNumber = 0;
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rowNumber++;
                    var cells = ReadExcelCells(reader);
                    if (rowNumber == mapping.HeaderRowNumber)
                    {
                        headers = BuildHeaders(cells);
                        columns = ResolveColumns(headers, mapping);
                    }
                    if (rowNumber >= mapping.DataStartRowNumber)
                    {
                        if (columns is null || headers is null) throw new HrValidationException("The configured Excel header row was not found before the data rows.");
                        accumulator.Add(rowNumber, cells, headers, columns);
                    }
                }
                break;
            } while (reader.NextResult());

            if (!selectedSheetFound) throw new HrValidationException("The selected worksheet was not found.");
        }

        return accumulator.Build();
    }

    private static async IAsyncEnumerable<SourceRow> ReadCsvRowsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in ReadDelimitedRowsAsync(stream, cancellationToken)) yield return row;
    }

    internal static async Task<(string[] Headers, List<string[]> Rows)> ReadTableAsync(Stream stream, string extension,
        string? sheetName, int headerRow, int firstDataRow, CancellationToken cancellationToken, string? preserveZeroPaddingColumn = null)
    {
        if (headerRow < 1 || headerRow > 1000 || firstDataRow <= headerRow || firstDataRow > 2000)
            throw new HrValidationException("Invalid header or first data row.");
        Reset(stream);
        string[]? headers = null;
        var rows = new List<string[]>();
        void Add(int number, string[] cells)
        {
            if (number == headerRow) headers = BuildHeaders(cells);
            if (number >= firstDataRow && cells.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                if (rows.Count >= 2000) throw new HrValidationException("Employee imports cannot exceed 2000 rows.");
                rows.Add(cells);
            }
        }
        if (extension == ".csv")
        {
            await foreach (var row in ReadDelimitedRowsAsync(stream, cancellationToken)) Add(row.RowNumber, row.Cells);
        }
        else
        {
            using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration { LeaveOpen = true });
            var found = false;
            do
            {
                if (!string.IsNullOrWhiteSpace(sheetName) && !string.Equals(reader.Name, sheetName, StringComparison.OrdinalIgnoreCase)) continue;
                found = true;
                var number = 0;
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var cells = ReadExcelCells(reader);
                    // Employee phone imports opt in; other imports retain their existing conversions.
                    var phoneIndex = headers is null || string.IsNullOrEmpty(preserveZeroPaddingColumn) ? -1 : Array.IndexOf(headers, preserveZeroPaddingColumn);
                    if (phoneIndex >= 0 && phoneIndex < cells.Length && reader.GetValue(phoneIndex) is double phone)
                    {
                        var format = reader.GetNumberFormatString(phoneIndex);
                        if (format is { Length: > 1 and <= 32 } && format.All(c => c == '0') && phone >= 0 && Math.Truncate(phone) == phone)
                            cells[phoneIndex] = phone.ToString(format, CultureInfo.InvariantCulture);
                    }
                    Add(++number, cells);
                }
                break;
            } while (reader.NextResult());
            if (!found) throw new HrValidationException("The selected worksheet was not found.");
        }
        if (headers is null || rows.Count == 0) throw new HrValidationException("No employee data rows were found.");
        return (headers, rows);
    }

    private static async IAsyncEnumerable<SourceRow> ReadDelimitedRowsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var textReader = new StreamReader(stream, Encoding.UTF8, true, 81_920, true);
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            DetectDelimiter = true,
            DetectDelimiterValues = [",", ";", "\t", "|"],
            IgnoreBlankLines = false,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            MaxFieldSize = MaximumCellCharacters
        };
        using var csv = new CsvReader(textReader, configuration);
        var rowNumber = 0;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var cells = csv.Parser.Record ?? [];
            EnsureColumnLimit(cells.Length);
            yield return new SourceRow(rowNumber, cells.Select(NormalizeCell).ToArray());
        }
    }

    private static string[] ReadExcelCells(IExcelDataReader reader)
    {
        EnsureColumnLimit(reader.FieldCount);
        var cells = new string[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var value = reader.GetValue(index);
            cells[index] = NormalizeCell(value switch
            {
                null => null,
                DateTime dateTime when dateTime >= new DateTime(1899, 12, 30) && dateTime < new DateTime(1900, 1, 2)
                    => dateTime.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                TimeSpan timeSpan when timeSpan >= TimeSpan.Zero && timeSpan < TimeSpan.FromDays(1)
                    => TimeOnly.FromTimeSpan(timeSpan).ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                float number => number.ToString("R", CultureInfo.InvariantCulture),
                decimal number => number.ToString(CultureInfo.InvariantCulture),
                bool boolean => boolean ? "true" : "false",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            });
        }
        return cells;
    }

    private static (int HeaderRow, IReadOnlyCollection<string> Columns) SelectHeader(IReadOnlyList<string[]> rows)
    {
        if (rows.Count == 0) throw new HrValidationException("The uploaded file does not contain any rows.");

        static string NormalizeHeader(string value)
        {
            var text = value.Normalize(NormalizationForm.FormKC)
                .Replace("\uFEFF", string.Empty, StringComparison.Ordinal)
                .Replace('\u00A0', ' ')
                .Replace('\u2007', ' ')
                .Replace('\u202F', ' ');
            foreach (var ch in new[] { '\u200B', '\u200C', '\u200D', '\u2060' })
                text = text.Replace(ch.ToString(), string.Empty, StringComparison.Ordinal);
            text = Regex.Replace(text, @"[_\./\\:|()]+", " ");
            text = Regex.Replace(text, @"[\-–—−]+", " ");
            return Regex.Replace(text, @"\s+", " ").Trim().ToLowerInvariant();
        }

        static int HeaderLikeness(string[] cells)
        {
            string[] hints =
            [
                "code", "employee code", "employee number",
                "name in arabic", "arabic name", "employee name", "name",
                "male female", "gender",
                "title", "position", "job title",
                "card number", "national id",
                "date of employment", "employment date",
                "fingerprint date",
                "birth of day", "date of birth", "birth date",
                "address",
                "date out of work employer", "end work date",
                "department", "mobile", "phone", "status"
            ];
            var score = 0;
            foreach (var cell in cells)
            {
                if (string.IsNullOrWhiteSpace(cell)) continue;
                var text = NormalizeHeader(cell);
                if (text.Any(char.IsLetter)) score += 2;
                if (hints.Any(hint => text == hint || text.Replace(" ", string.Empty, StringComparison.Ordinal) == hint.Replace(" ", string.Empty, StringComparison.Ordinal)))
                    score += 30;
                else if (text.Contains("name", StringComparison.Ordinal)
                    || text.Contains("date", StringComparison.Ordinal)
                    || text.Contains("card", StringComparison.Ordinal)
                    || text.Contains("fingerprint", StringComparison.Ordinal)
                    || text.Contains("employee", StringComparison.Ordinal)
                    || text.Contains("اسم", StringComparison.Ordinal)
                    || text.Contains("موظف", StringComparison.Ordinal)
                    || text.Contains("تاريخ", StringComparison.Ordinal)
                    || text.Contains("بطاقة", StringComparison.Ordinal))
                    score += 8;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) score -= 3;
            }
            return score;
        }

        var selected = rows
            .Select((cells, index) => new
            {
                Cells = cells,
                Row = index + 1,
                Count = cells.Count(cell => !string.IsNullOrWhiteSpace(cell)),
                Likeness = HeaderLikeness(cells)
            })
            .OrderByDescending(item => item.Likeness)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Row)
            .First();
        if (selected.Count == 0) throw new HrValidationException("No usable header row was detected.");
        return (selected.Row, BuildHeaders(selected.Cells));
    }

    private static string[] BuildHeaders(IReadOnlyList<string> cells)
    {
        EnsureColumnLimit(cells.Count);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headers = new string[cells.Count];
        for (var index = 0; index < cells.Count; index++)
        {
            var baseName = string.IsNullOrWhiteSpace(cells[index]) ? $"Column {index + 1}" : cells[index].Trim();
            counts.TryGetValue(baseName, out var count);
            count++;
            counts[baseName] = count;
            headers[index] = count == 1 ? baseName : $"{baseName} ({count})";
        }
        return headers;
    }

    private static ResolvedColumns ResolveColumns(string[] headers, AttendanceImportColumnMappingRequest mapping)
    {
        var lookup = headers.Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
        int Required(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !lookup.TryGetValue(value.Trim(), out var index))
                throw new HrValidationException($"Mapped {label} column was not found in the selected header row.");
            return index;
        }
        int? Optional(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (!lookup.TryGetValue(value.Trim(), out var index))
                throw new HrValidationException($"Mapped {label} column was not found in the selected header row.");
            return index;
        }

        return new ResolvedColumns(
            Required(mapping.EmployeeNumberColumn, "employee ID"),
            Optional(mapping.EmployeeNameColumn, "employee name"),
            Optional(mapping.AttendanceDateColumn, "attendance date"),
            Optional(mapping.CheckInColumn, "check-in"),
            Optional(mapping.CheckOutColumn, "check-out"),
            Optional(mapping.PunchDateTimeColumn, "punch date/time"),
            Optional(mapping.PunchTypeColumn, "punch type"),
            Optional(mapping.TimeColumn, "time"),
            Optional(mapping.AmPmColumn, "AM/PM"));
    }

    private static void ValidateMapping(AttendanceImportColumnMappingRequest mapping)
    {
        if (mapping.TimeSystem is not ("24-hour" or "12-hour"))
            throw new HrValidationException("Import time system is invalid.");
        if (mapping.HeaderRowNumber < 1 || mapping.DataStartRowNumber <= mapping.HeaderRowNumber)
            throw new HrValidationException("Data must start after the configured header row.");
        if (mapping.DataStartRowNumber > 1_000_000) throw new HrValidationException("Data start row is outside the supported range.");
        if (string.IsNullOrWhiteSpace(mapping.EmployeeNumberColumn)) throw new HrValidationException("Employee ID mapping is required.");
        if (string.IsNullOrWhiteSpace(mapping.TimeZoneId)) throw new HrValidationException("Import time zone is required.");
        if (mapping.Layout == HrAttendanceImportLayouts.CheckInCheckOutColumns)
        {
            if (string.IsNullOrWhiteSpace(mapping.AttendanceDateColumn)) throw new HrValidationException("Attendance date mapping is required for check-in/check-out layout.");
            if (string.IsNullOrWhiteSpace(mapping.CheckInColumn) && string.IsNullOrWhiteSpace(mapping.CheckOutColumn))
                throw new HrValidationException("At least one of check-in or check-out must be mapped.");
        }
        else if (mapping.Layout == HrAttendanceImportLayouts.PunchRows)
        {
            if (!string.IsNullOrWhiteSpace(mapping.TimeColumn))
            {
                if (string.IsNullOrWhiteSpace(mapping.AttendanceDateColumn)) throw new HrValidationException("Attendance date mapping is required for separate punch times.");
                if (mapping.TimeSystem == "12-hour" && string.IsNullOrWhiteSpace(mapping.AmPmColumn)) throw new HrValidationException("AM/PM mapping is required for separate 12-hour punch times.");
            }
            else if (string.IsNullOrWhiteSpace(mapping.PunchDateTimeColumn)) throw new HrValidationException("Punch date/time mapping is required for punch-row layout.");
        }
        else
        {
            throw new HrValidationException("Attendance import layout is invalid.");
        }
    }

    private static CultureInfo ResolveCulture(string? cultureName)
    {
        try
        {
            return string.IsNullOrWhiteSpace(cultureName) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName.Trim());
        }
        catch (CultureNotFoundException)
        {
            throw new HrValidationException("The selected import culture is invalid.");
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            throw new HrValidationException("The selected import time zone is not available on this server.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new HrValidationException("The selected import time zone is invalid.");
        }
    }

    private static void EnsureColumnLimit(int count)
    {
        if (count > MaximumColumns) throw new HrValidationException($"Attendance imports cannot exceed {MaximumColumns} columns.");
    }

    private static string NormalizeCell(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > MaximumCellCharacters) throw new HrValidationException($"Attendance cell values cannot exceed {MaximumCellCharacters} characters.");
        return normalized;
    }

    private static void Reset(Stream stream)
    {
        if (!stream.CanSeek) throw new HrValidationException("The stored attendance file must be seekable.");
        stream.Position = 0;
    }

    private sealed class PreviewAccumulator
    {
        private readonly AttendanceImportColumnMappingRequest _mapping;
        private string TimeFormat => string.IsNullOrWhiteSpace(_mapping.TimeFormat)
            ? _mapping.TimeSystem == "12-hour" ? "hh:mm tt" : "HH:mm"
            : _mapping.TimeFormat;
        private readonly CultureInfo _culture;
        private readonly TimeZoneInfo _zone;
        private readonly IWorkingCalendarCalculator _calendar;
        private readonly Dictionary<string, MutablePreviewGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
        private int _sourceRows;

        public PreviewAccumulator(
            AttendanceImportColumnMappingRequest mapping,
            CultureInfo culture,
            TimeZoneInfo zone,
            IWorkingCalendarCalculator calendar)
        {
            _mapping = mapping;
            _culture = culture;
            _zone = zone;
            _calendar = calendar;
        }

        public void Add(int rowNumber, string[] cells, string[] headers, ResolvedColumns columns)
        {
            if (cells.All(string.IsNullOrWhiteSpace)) return;
            _sourceRows++;
            if (_sourceRows > MaximumRows) throw new HrValidationException($"Attendance imports cannot exceed {MaximumRows:N0} data rows.");

            var employeeNumber = Cell(cells, columns.EmployeeNumber);
            var employeeName = Cell(cells, columns.EmployeeName);
            var errors = new List<string>();
            DateOnly? attendanceDate = null;
            var punches = new List<ParsedPunch>();
            DateTimeOffset? checkIn = null;
            DateTimeOffset? checkOut = null;

            if (string.IsNullOrWhiteSpace(employeeNumber)) errors.Add("Employee ID is missing.");
            if (_mapping.Layout == HrAttendanceImportLayouts.CheckInCheckOutColumns)
            {
                var dateText = Cell(cells, columns.AttendanceDate);
                if (!TryParseDate(dateText, _mapping.DateFormat, _culture, out var parsedDate))
                    errors.Add("Attendance date is missing or invalid.");
                else
                    attendanceDate = parsedDate;

                var checkInText = Cell(cells, columns.CheckIn);
                var checkOutText = Cell(cells, columns.CheckOut);
                if (!string.IsNullOrWhiteSpace(checkInText) && attendanceDate.HasValue)
                {
                    if (TryParseInstant(checkInText, attendanceDate.Value, false, null, out var parsedCheckIn))
                    {
                        checkIn = parsedCheckIn;
                        punches.Add(new ParsedPunch(parsedCheckIn, AttendanceValues.CheckInPunch, rowNumber));
                    }
                    else errors.Add("Invalid check-in time.");
                }
                if (!string.IsNullOrWhiteSpace(checkOutText) && attendanceDate.HasValue)
                {
                    if (TryParseInstant(checkOutText, attendanceDate.Value, true, checkIn, out var parsedCheckOut))
                    {
                        checkOut = parsedCheckOut;
                        punches.Add(new ParsedPunch(parsedCheckOut, AttendanceValues.CheckOutPunch, rowNumber));
                    }
                    else errors.Add("Invalid check-out time.");
                }
            }
            else
            {
                var dateText = Cell(cells, columns.AttendanceDate);
                if (!string.IsNullOrWhiteSpace(dateText))
                {
                    if (TryParseDate(dateText, _mapping.DateFormat, _culture, out var parsedDate)) attendanceDate = parsedDate;
                    else errors.Add("Attendance date is invalid.");
                }
                var punchText = Cell(cells, columns.Time ?? columns.PunchDateTime);
                if (columns.Time.HasValue && columns.AmPm.HasValue)
                {
                    var marker = Cell(cells, columns.AmPm).Trim().ToUpperInvariant();
                    if (marker is not ("AM" or "PM" or "ص" or "م"))
                    {
                        errors.Add("AM/PM value is missing or invalid.");
                        punchText = string.Empty;
                    }
                    else punchText = CombineSeparateTime(punchText, marker);
                }
                if (string.IsNullOrWhiteSpace(punchText))
                {
                    errors.Add("Punch date/time is missing.");
                }
                else if (TryParsePunch(punchText, attendanceDate, out var punchInstant, out var punchDate))
                {
                    attendanceDate ??= punchDate;
                    var typeText = Cell(cells, columns.PunchType);
                    var type = string.IsNullOrWhiteSpace(typeText)
                        ? AttendanceValues.UnknownPunch
                        : AttendanceValues.NormalizePunchType(typeText);
                    if (type is null) errors.Add("Punch type is invalid.");
                    else punches.Add(new ParsedPunch(punchInstant, type, rowNumber));
                }
                else
                {
                    errors.Add("Punch date/time is invalid.");
                }
            }

            var key = !string.IsNullOrWhiteSpace(employeeNumber) && attendanceDate.HasValue
                ? $"{employeeNumber.Trim().ToLowerInvariant()}|{attendanceDate:yyyyMMdd}"
                : $"invalid|{rowNumber}";
            if (!_groups.TryGetValue(key, out var group))
            {
                if (_groups.Count >= MaximumGroups) throw new HrValidationException($"Attendance imports cannot exceed {MaximumGroups:N0} employee/day groups.");
                group = new MutablePreviewGroup(employeeNumber, employeeName, attendanceDate);
                _groups.Add(key, group);
            }

            group.RowNumbers.Add(rowNumber);
            group.RawRows.Add(BuildRawRow(rowNumber, cells, headers, columns));
            group.Punches.AddRange(punches);
            group.Errors.AddRange(errors);
            if (checkIn.HasValue && (!group.CheckIn.HasValue || checkIn < group.CheckIn)) group.CheckIn = checkIn;
            if (checkOut.HasValue && (!group.CheckOut.HasValue || checkOut > group.CheckOut)) group.CheckOut = checkOut;
        }

        public ParsedAttendanceImport Build()
        {
            var groups = _groups.Values.Select(group =>
            {
                var punches = group.Punches
                    .DistinctBy(item => new { item.Instant, item.Type })
                    .OrderBy(item => item.Instant)
                    .ToArray();
                var checkIn = group.CheckIn ?? punches.Where(item => item.Type == AttendanceValues.CheckInPunch).Select(item => (DateTimeOffset?)item.Instant).FirstOrDefault();
                var checkOut = group.CheckOut ?? punches.Where(item => item.Type == AttendanceValues.CheckOutPunch).Select(item => (DateTimeOffset?)item.Instant).LastOrDefault();
                var unknown = punches.Where(item => item.Type == AttendanceValues.UnknownPunch).ToArray();
                checkIn ??= unknown.Select(item => (DateTimeOffset?)item.Instant).FirstOrDefault();
                if (!checkOut.HasValue && unknown.Length > 1) checkOut = unknown[^1].Instant;
                var errors = group.Errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (checkOut < checkIn) errors.Add("Check-out is before check-in.");
                return new ParsedAttendanceGroup(
                    group.RowNumbers.Order().ToArray(),
                    group.RawRows,
                    group.EmployeeNumber,
                    group.EmployeeName,
                    group.AttendanceDate,
                    checkIn,
                    checkOut,
                    punches,
                    errors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }).ToArray();
            return new ParsedAttendanceImport(_sourceRows, groups);
        }

        private bool TryParseInstant(
            string value,
            DateOnly date,
            bool isCheckOut,
            DateTimeOffset? parsedCheckIn,
            out DateTimeOffset instant)
        {
            if (TryParseOffset(value, _culture, out instant)) return true;
            if (TryParseTime(value, TimeFormat, _culture, out var time))
            {
                var localDate = isCheckOut && parsedCheckIn.HasValue
                    ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(parsedCheckIn.Value, _zone).DateTime)
                    : date;
                instant = _calendar.ToInstant(localDate, time, _mapping.TimeZoneId);
                if (isCheckOut && parsedCheckIn.HasValue && instant < parsedCheckIn.Value)
                    instant = _calendar.ToInstant(localDate.AddDays(1), time, _mapping.TimeZoneId);
                return true;
            }
            if (TryParseLocalDateTime(value, _mapping.DateFormat, TimeFormat, _culture, out var local))
            {
                instant = _calendar.ToInstant(DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local), _mapping.TimeZoneId);
                return true;
            }
            instant = default;
            return false;
        }

        private bool TryParsePunch(string value, DateOnly? date, out DateTimeOffset instant, out DateOnly localDate)
        {
            if (date.HasValue && TryParseTime(value, TimeFormat, _culture, out var time))
            {
                instant = _calendar.ToInstant(date.Value, time, _mapping.TimeZoneId);
                localDate = date.Value;
                return true;
            }
            if (TryParseOffset(value, _culture, out instant))
            {
                localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _zone).DateTime);
                return true;
            }
            if (TryParseLocalDateTime(value, _mapping.DateFormat, TimeFormat, _culture, out var local))
            {
                localDate = DateOnly.FromDateTime(local);
                instant = _calendar.ToInstant(localDate, TimeOnly.FromDateTime(local), _mapping.TimeZoneId);
                return true;
            }
            instant = default;
            localDate = default;
            return false;
        }

        private static Dictionary<string, string?> BuildRawRow(
            int rowNumber,
            string[] cells,
            string[] headers,
            ResolvedColumns columns)
        {
            var indexes = new[]
            {
                columns.EmployeeNumber, columns.EmployeeName, columns.AttendanceDate, columns.CheckIn,
                columns.CheckOut, columns.PunchDateTime, columns.PunchType, columns.Time, columns.AmPm
            }.Where(index => index.HasValue).Select(index => index!.Value).Distinct();
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["SourceRowNumber"] = rowNumber.ToString(CultureInfo.InvariantCulture) };
            foreach (var index in indexes) values[headers[index]] = Cell(cells, index);
            return values;
        }

        private static string Cell(string[] cells, int? index) =>
            index.HasValue && index.Value < cells.Length ? cells[index.Value] : string.Empty;
    }

    private static bool TryParseDate(string value, string? format, CultureInfo culture, out DateOnly date)
    {
        // ExcelDataReader returns typed date cells as DateTime, serialized invariantly above.
        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var excelDate))
        {
            date = DateOnly.FromDateTime(excelDate);
            return true;
        }
        if (!string.IsNullOrWhiteSpace(format) && DateOnly.TryParseExact(value, format, culture, DateTimeStyles.AllowWhiteSpaces, out date)) return true;
        if (DateOnly.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out date)) return true;
        if (DateOnly.TryParseExact(value, "dd-MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)) return true;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and <= 2_958_465)
        {
            try
            {
                date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
                return true;
            }
            catch (ArgumentException)
            {
            }
        }
        date = default;
        return false;
    }

    private static bool TryParseTime(string value, string? format, CultureInfo culture, out TimeOnly time)
    {
        value = NormalizeMeridiem(value);
        if (Regex.IsMatch(value, @"(?:AM|PM)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            // Parse marked clock times explicitly: never reinterpret 13:00 PM as a valid 24-hour time.
            if (!string.IsNullOrWhiteSpace(format) && TimeOnly.TryParseExact(value, format,
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time)) return true;
            return TimeOnly.TryParseExact(value, ["h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt"],
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time);
        }
        if (!string.IsNullOrWhiteSpace(format) && TimeOnly.TryParseExact(value, format, culture, DateTimeStyles.AllowWhiteSpaces, out time)) return true;
        if (TimeOnly.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out time)) return true;
        if (TimeSpan.TryParse(value, culture, out var span) && span >= TimeSpan.Zero && span < TimeSpan.FromDays(1))
        {
            time = TimeOnly.FromTimeSpan(span);
            return true;
        }
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 0 and < 1)
        {
            time = TimeOnly.FromTimeSpan(TimeSpan.FromDays(serial));
            return true;
        }
        time = default;
        return false;
    }

    private static bool TryParseOffset(string value, CultureInfo culture, out DateTimeOffset instant)
    {
        instant = default;
        var containsOffset = value.EndsWith('Z') || value.LastIndexOf('+') > 7 || value.LastIndexOf('-') > 9;
        return containsOffset && DateTimeOffset.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out instant);
    }

    private static bool TryParseLocalDateTime(
        string value,
        string? dateFormat,
        string? timeFormat,
        CultureInfo culture,
        out DateTime local)
    {
        value = NormalizeMeridiem(value);
        // A failed clock value must not fall back to DateTime's implicit current date.
        if (!Regex.IsMatch(value, @"\d[/.\-]\d|[\p{L}]{3,}"))
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) || numeric < 1)
            {
                local = default;
                return false;
            }
        }
        if (Regex.IsMatch(value, @"(?:AM|PM)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            culture = CultureInfo.InvariantCulture;
        if (!string.IsNullOrWhiteSpace(dateFormat) && !string.IsNullOrWhiteSpace(timeFormat) &&
            DateTime.TryParseExact(value, $"{dateFormat} {timeFormat}", culture, DateTimeStyles.AllowWhiteSpaces, out local)) return true;
        if (DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out local) &&
            (value.Contains(':', StringComparison.Ordinal) || value.Contains('T', StringComparison.OrdinalIgnoreCase))) return true;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 1 and <= 2_958_465)
        {
            try
            {
                local = DateTime.FromOADate(serial);
                return true;
            }
            catch (ArgumentException)
            {
            }
        }
        local = default;
        return false;
    }

    private sealed record SourceRow(int RowNumber, string[] Cells);
    private static string NormalizeMeridiem(string value) => Regex.Replace(value.Trim(),
        @"\s*(AM|PM|ص|م)$", match => " " + (match.Groups[1].Value.ToUpperInvariant() switch
        {
            "ص" => "AM", "م" => "PM", var marker => marker
        }), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string SuggestTimeSystem(IEnumerable<string[]> rows) => rows.SelectMany(row => row)
        .Any(value => Regex.IsMatch(value, @"\d{1,2}:\d{2}(?::\d{2})?\s*(AM|PM|ص|م)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ? "12-hour" : "24-hour";
    private static string CombineSeparateTime(string value, string marker)
    {
        // Native Excel clock cells may arrive as a fractional day or a normalized clock.
        if ((double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 0 and < 1
             || Regex.IsMatch(value, @"^\d{2}:\d{2}:\d{2}\.\d{7}$"))
            && TryParseTime(value, null, CultureInfo.InvariantCulture, out var clock))
            value = clock.ToString("h:mm:ss", CultureInfo.InvariantCulture);
        return $"{value} {marker}";
    }

    private sealed record ResolvedColumns(int EmployeeNumber, int? EmployeeName, int? AttendanceDate, int? CheckIn, int? CheckOut, int? PunchDateTime, int? PunchType, int? Time, int? AmPm);

    private sealed class MutablePreviewGroup
    {
        public MutablePreviewGroup(string? employeeNumber, string? employeeName, DateOnly? attendanceDate)
        {
            EmployeeNumber = string.IsNullOrWhiteSpace(employeeNumber) ? null : employeeNumber.Trim();
            EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? null : employeeName.Trim();
            AttendanceDate = attendanceDate;
        }

        public string? EmployeeNumber { get; }
        public string? EmployeeName { get; }
        public DateOnly? AttendanceDate { get; }
        public List<int> RowNumbers { get; } = [];
        public List<Dictionary<string, string?>> RawRows { get; } = [];
        public List<ParsedPunch> Punches { get; } = [];
        public List<string> Errors { get; } = [];
        public DateTimeOffset? CheckIn { get; set; }
        public DateTimeOffset? CheckOut { get; set; }
    }
}

internal sealed record ParsedAttendanceImport(int SourceRowCount, IReadOnlyCollection<ParsedAttendanceGroup> Groups);

internal sealed record ParsedAttendanceGroup(
    IReadOnlyCollection<int> SourceRowNumbers,
    IReadOnlyCollection<Dictionary<string, string?>> SourceRows,
    string? SourceEmployeeNumber,
    string? SourceEmployeeName,
    DateOnly? AttendanceDate,
    DateTimeOffset? CheckIn,
    DateTimeOffset? CheckOut,
    IReadOnlyCollection<ParsedPunch> Punches,
    IReadOnlyCollection<string> Errors);

internal sealed record ParsedPunch(DateTimeOffset Instant, string Type, int SourceRowNumber);
