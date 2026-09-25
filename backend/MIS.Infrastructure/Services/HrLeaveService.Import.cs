using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using CsvHelper;
using ExcelDataReader;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Services;

public sealed partial class HrLeaveService
{
    private sealed record ImportRow(int Number, string EmployeeNumber, Guid? EmployeeId, string? EmployeeName,
        string LeaveTypeText, Guid? LeaveTypeId, DateOnly? Start, DateOnly? End, string? Reason, string? Error);
    private sealed record StagedImport(Guid UserId, DateTimeOffset ExpiresAt, string FileName, IReadOnlyList<ImportRow> Rows);
    private static readonly ConcurrentDictionary<Guid, StagedImport> Imports = new();
    private const int MaxRows = 5000;

    public async Task<LeaveImportReviewDto> ReviewImportAsync(Stream stream, string fileName, long length, CancellationToken cancellationToken)
    {
        if (length <= 0 || length > ExcelImportLimits.MaximumBytes) throw new HrValidationException("Leave sheet must be between 1 byte and 50 MB.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xls" or ".csv")) throw new HrValidationException("Only XLSX, XLS, and CSV leave sheets are supported.");
        List<string[]> raw;
        try { raw = extension == ".csv" ? ReadCsv(stream) : ReadExcel(stream); }
        catch (Exception exception) when (exception is not HrValidationException) { throw new HrValidationException("The leave sheet is malformed or unreadable."); }
        if (raw.Count < 2) throw new HrValidationException("The leave sheet contains no data rows.");
        if (raw.Count - 1 > MaxRows) throw new HrValidationException($"Leave sheets cannot exceed {MaxRows} rows.");
        if (raw.Any(row => row.Length > 30)) throw new HrValidationException("Leave sheets cannot exceed 30 columns.");

        var headers = raw[0].Select(NormalizeHeader).ToArray();
        int Column(params string[] aliases) => Array.FindIndex(headers, h => aliases.Contains(h));
        var employeeColumn = Column("employeenumber", "employeeid", "رقمالموظف");
        var typeColumn = Column("leavetype", "نوعالإجازة", "نوعالاجازة");
        var startColumn = Column("startdate", "تاريخالبداية", "تاريخالبدء");
        var endColumn = Column("enddate", "تاريخالنهاية");
        var reasonColumn = Column("reason", "السبب");
        var statusColumn = Column("status", "الحالة");
        if (employeeColumn < 0 || typeColumn < 0 || startColumn < 0 || endColumn < 0)
            throw new HrValidationException("Required columns: Employee Number, Leave Type, Start Date, End Date.");

        var employees = await _dbContext.Employees.AsNoTracking().ToDictionaryAsync(x => x.EmployeeNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var leaveTypes = await _dbContext.LeaveTypes.AsNoTracking().ToListAsync(cancellationToken);
        var rows = new List<ImportRow>();
        for (var index = 1; index < raw.Count; index++)
        {
            var cells = raw[index]; if (cells.All(string.IsNullOrWhiteSpace)) continue;
            string Cell(int column) => column >= 0 && column < cells.Length ? cells[column].Trim() : string.Empty;
            var employeeNumber = Cell(employeeColumn); employees.TryGetValue(employeeNumber, out var employee);
            var typeText = Cell(typeColumn);
            var matches = leaveTypes.Where(x => string.Equals(x.Name, typeText, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.NameArabic, typeText, StringComparison.OrdinalIgnoreCase)).ToArray();
            var start = ParseDate(Cell(startColumn)); var end = ParseDate(Cell(endColumn)); var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(employeeNumber)) errors.Add("Employee Number is required."); else if (employee is null) errors.Add("Employee not found.");
            if (matches.Length != 1) errors.Add("Invalid leave type.");
            if (!start.HasValue) errors.Add("Invalid start date."); if (!end.HasValue) errors.Add("Invalid end date.");
            if (start.HasValue && end.HasValue && end < start) errors.Add("End date cannot be before start date.");
            var status = Cell(statusColumn); if (status.Length > 0 && !status.Equals("Pending", StringComparison.OrdinalIgnoreCase) && status != "قيد الانتظار") errors.Add("Imported status must be Pending.");
            if (Cell(reasonColumn).Length > 2000) errors.Add("Reason cannot exceed 2000 characters.");
            if (employee is not null && start.HasValue && end.HasValue && errors.Count == 0)
            {
                try { await EnsureEmployeeLifecycleRangeAsync(employee.Id, start.Value, end.Value, cancellationToken); await EnsureNoOverlapAsync(employee.Id, start.Value, end.Value, null, cancellationToken); await CountWorkingDaysAsync(start.Value, end.Value, cancellationToken); }
                catch (HrValidationException ex) { errors.Add(ex.Message); }
                if (rows.Any(x => x.EmployeeId == employee.Id && x.Start.HasValue && x.End.HasValue && start <= x.End && end >= x.Start)) errors.Add("Overlapping row in this sheet.");
            }
            rows.Add(new(index + 1, employeeNumber, employee?.Id, employee?.FullName, typeText, matches.SingleOrDefault()?.Id, start, end, Cell(reasonColumn), errors.Count == 0 ? null : string.Join(" ", errors)));
        }
        if (rows.Count == 0) throw new HrValidationException("The leave sheet contains no data rows.");
        var id = Guid.NewGuid(); Imports[id] = new(_currentUser.UserId, DateTimeOffset.UtcNow.AddMinutes(30), Path.GetFileName(fileName), rows);
        return Review(id, Path.GetFileName(fileName), rows);
    }

    public async Task<LeaveImportResultDto> ConfirmImportAsync(Guid importId, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null)
    {
        if (!Imports.TryRemove(importId, out var staged) || staged.UserId != _currentUser.UserId || staged.ExpiresAt < DateTimeOffset.UtcNow)
            throw new HrValidationException("This leave import has expired or was already confirmed.");
        var excluded = (excludedRows ?? []).ToHashSet();
        var rows = staged.Rows.Where(row => !excluded.Contains(row.Number)).ToArray();
        if (rows.Length == 0) throw new HrValidationException("Select at least one leave row to import.");
        if (rows.Any(x => x.Error is not null)) throw new HrValidationException("Resolve all row errors before importing.");
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows)
        {
            await EnsureRequestReferencesAsync(row.EmployeeId!.Value, row.LeaveTypeId!.Value, null, cancellationToken);
            await EnsureEmployeeLifecycleRangeAsync(row.EmployeeId.Value, row.Start!.Value, row.End!.Value, cancellationToken);
            await EnsureNoOverlapAsync(row.EmployeeId.Value, row.Start.Value, row.End.Value, null, cancellationToken);
            var days = await CountWorkingDaysAsync(row.Start.Value, row.End.Value, cancellationToken);
            _dbContext.LeaveRequests.Add(new LeaveRequest(row.EmployeeId.Value, row.LeaveTypeId.Value, row.Start.Value, row.End.Value, days, row.Reason, null, null, now, _currentUser.UserId, now));
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest("LeaveSheetImported", nameof(LeaveRequest), importId.ToString(), null, null,
            new { staged.FileName, ImportedRecords = rows.Length }, $"Imported {rows.Length} pending leave requests from {staged.FileName}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(rows.Length);
    }

    public async Task<LeaveTemplateDto> BuildImportTemplateAsync(CancellationToken cancellationToken)
    {
        var types = await _dbContext.LeaveTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.Name, x.NameArabic }).ToListAsync(cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leave Import");
        var headers = new[] { "Employee Number", "Leave Type", "Start Date", "End Date", "Reason", "Status" };
        for (var i = 0; i < headers.Length; i++) { sheet.Cell(1, i + 1).Value = headers[i]; sheet.Cell(1, i + 1).Style.Font.Bold = true; }
        sheet.Cell(2, 6).Value = "Pending"; sheet.Columns().AdjustToContents();
        var guidance = workbook.Worksheets.Add("Valid Leave Types"); guidance.Cell(1, 1).Value = "English"; guidance.Cell(1, 2).Value = "Arabic"; guidance.Range(1, 1, 1, 2).Style.Font.Bold = true;
        for (var i = 0; i < types.Count; i++) { guidance.Cell(i + 2, 1).Value = types[i].Name; guidance.Cell(i + 2, 2).Value = types[i].NameArabic ?? string.Empty; }
        guidance.Columns().AdjustToContents(); using var output = new MemoryStream(); workbook.SaveAs(output);
        return new(output.ToArray(), "Leave_Import_Template.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static LeaveImportReviewDto Review(Guid id, string file, IReadOnlyList<ImportRow> rows) => new(id, file, rows.Count,
        rows.Count(x => x.Error is null), 0, rows.Count(x => x.Error is not null), rows.Select(x => new LeaveImportRowDto(x.Number, x.EmployeeNumber, x.EmployeeName, x.LeaveTypeText, x.Start, x.End, x.Reason, x.Error is null ? "Valid" : "Error", x.Error)).ToArray());
    private static string NormalizeHeader(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static DateOnly? ParseDate(string value) { if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d) || DateOnly.TryParse(value, new CultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out d) || DateOnly.TryParse(value, new CultureInfo("ar-EG"), DateTimeStyles.AllowWhiteSpaces, out d)) return d; if (double.TryParse(value, CultureInfo.InvariantCulture, out var serial)) { try { return DateOnly.FromDateTime(DateTime.FromOADate(serial)); } catch { } } return null; }
    private static List<string[]> ReadCsv(Stream stream) { using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true); using var csv = new CsvReader(reader, CultureInfo.InvariantCulture); var result = new List<string[]>(); while (csv.Read()) { var count = csv.Parser.Count; result.Add(Enumerable.Range(0, count).Select(i => csv.GetField(i) ?? string.Empty).ToArray()); } return result; }
    private static List<string[]> ReadExcel(Stream stream) { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); using var reader = ExcelReaderFactory.CreateReader(stream); var result = new List<string[]>(); do { while (reader.Read()) result.Add(Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty).ToArray()); if (result.Count > 0) break; } while (reader.NextResult()); return result; }
}
