using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using Npgsql;

namespace MIS.Infrastructure.Services;

public sealed class AbsenceImportService(
    ApplicationDbContext db,
    IHrFileStorage storage,
    IHrAuditService audit,
    ICurrentUserContext user,
    IWorkingCalendarCalculator calendar) : IAbsenceImportService
{
    private const string Entity = "AbsenceImport";
    private const long MaximumBytes = 20 * 1024 * 1024;
    private const decimal PayrollMonthDivisor = 30m;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, string StorageKey, int TotalRows);

    public async Task<AbsenceImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 20 MB.");
        var stored = await storage.SaveAsync("absence-imports", file.FileName, file.ContentType, file.Content, MaximumBytes, cancellationToken);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, cancellationToken);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, cancellationToken);
            var id = Guid.NewGuid();
            await Write(id, "AbsenceImportUploaded", new Uploaded(stored.OriginalFileName, stored.StorageKey, extension), cancellationToken);
            return new AbsenceImportUpload(id, stored.OriginalFileName, sheets);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken cancellationToken) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id
            && log.Action == "AbsenceImportUploaded" && log.UserId == user.UserId, cancellationToken)
        ?? throw new HrNotFoundException("Absence import was not found.");

    public async Task<AbsenceImportPreview> PreviewAsync(Guid id, AbsenceImportMapping mapping, CancellationToken cancellationToken)
    {
        var upload = Read<Uploaded>((await OwnedUpload(id, cancellationToken)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "AbsenceImportCompleted", cancellationToken))
            throw new HrConflictException("This absence import is already complete.");
        if (mapping.Columns is null || mapping.Columns.Count > AbsenceImportMapper.Fields.Length || mapping.Columns.Keys.Except(AbsenceImportMapper.Fields).Any())
            throw new HrValidationException("Unsupported absence field mapping. / تعيين حقول غير صالح");
        if (!new[] { "EmployeeNumber", "NationalId", "MobileNumber", "EmployeeName" }.Any(key => mapping.Columns.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)))
            throw new HrValidationException("Map an employee identifier. / يجب تعيين معرف الموظف");
        if (!mapping.Columns.TryGetValue("AbsenceDate", out var dateColumn) || string.IsNullOrWhiteSpace(dateColumn))
            throw new HrValidationException("Map the absence date column. / يجب تعيين عمود تاريخ الغياب");
        var selected = mapping.Columns.Values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        if (selected.Distinct().Count() != selected.Length) throw new HrValidationException("Map each source column only once. / لا يمكن تعيين العمود أكثر من مرة");

        await using var stream = await storage.OpenReadAsync(upload.StorageKey, cancellationToken);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, cancellationToken);
        var indexes = new Dictionary<string, int>();
        foreach (var pair in mapping.Columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }

        var employees = await db.Employees.AsNoTracking()
            .Select(e => new AbsenceImportMapper.EmployeeMatch(e.Id, e.EmployeeNumber, e.NationalId, e.MobileNumber, e.FullName, e.FullNameArabic, e.FullNameEnglish, e.HireDate, e.TerminationDate))
            .ToArrayAsync(cancellationToken);

        var existingAbsences = (await db.EmployeeAbsences.AsNoTracking().Select(a => new { a.EmployeeId, a.AbsenceDate }).ToArrayAsync(cancellationToken))
            .Select(a => (a.EmployeeId, a.AbsenceDate)).ToHashSet();
        var approvedLeaves = (await db.LeaveRequests.AsNoTracking()
            .Where(x => x.Status == LeaveRequestStatuses.Approved)
            .Select(x => new { x.EmployeeId, x.StartDate, x.EndDate })
            .ToArrayAsync(cancellationToken))
            .SelectMany(x => Expand(x.EmployeeId, x.StartDate, x.EndDate)).ToHashSet();
        var conflictingAttendance = (await db.AttendanceRecords.AsNoTracking()
            .Where(x => !x.IsDeleted && (x.Status != AttendanceValues.AbsentStatus || x.CheckIn != null || x.CheckOut != null || db.AttendancePunches.Any(p => p.AttendanceRecordId == x.Id)))
            .Select(x => new { x.EmployeeId, x.AttendanceDate })
            .ToArrayAsync(cancellationToken))
            .Select(x => (x.EmployeeId, x.AttendanceDate)).ToHashSet();
        var approvedExcuses = (await db.HrExcuseMissions.AsNoTracking()
            .Where(m => m.Status == "Approved" && m.EmployeeId != null)
            .Select(m => new { EmployeeId = m.EmployeeId!.Value, m.Date })
            .ToArrayAsync(cancellationToken))
            .Select(m => (m.EmployeeId, m.Date)).ToHashSet();

        var cairoToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Africa/Cairo").DateTime);
        var batchKeys = new HashSet<(Guid EmployeeId, DateOnly Date)>();
        var rows = new List<AbsenceImportRow>();
        foreach (var cells in table.Rows)
        {
            var values = indexes.ToDictionary(pair => pair.Key, pair => pair.Value < cells.Length ? cells[pair.Value] : "");
            rows.Add(AbsenceImportMapper.Map(
                rows.Count + mapping.FirstDataRow,
                values,
                mapping.DateFormat,
                employees,
                existingAbsences,
                approvedLeaves,
                conflictingAttendance,
                approvedExcuses,
                batchKeys,
                cairoToday));
        }

        var preview = new AbsenceImportPreview(id, Guid.NewGuid(), rows);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(preview, Json));
        var stored = await storage.SaveAsync("absence-import-previews", "preview.json", "application/json", content, MaximumBytes, cancellationToken);
        try { await Write(id, "AbsenceImportPreviewed", new PreviewSaved(preview.PreviewId, stored.StorageKey, rows.Count), cancellationToken); }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return preview with { Rows = preview.Rows.Select(row => row with { Errors = row.Errors.Select(message => ApiTextLocalizer.Localize(message)).ToArray() }).ToArray() };
    }

    public async Task<AbsenceImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken)
    {
        await OwnedUpload(id, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", cancellationToken);
        var completed = await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "AbsenceImportCompleted", cancellationToken);
        if (completed is not null) return Read<AbsenceImportResult>(completed.NewValue);
        var events = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "AbsenceImportPreviewed").OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault();
        if (saved is null || saved.PreviewId != previewId)
            throw new HrValidationException("Build and review the absence preview before confirming.");
        await using var stream = await storage.OpenReadAsync(saved.StorageKey, cancellationToken);
        var preview = await JsonSerializer.DeserializeAsync<AbsenceImportPreview>(stream, Json, cancellationToken)
            ?? throw new HrValidationException("The absence preview could not be read.");

        var imported = 0; var skipped = 0; var failed = 0;
        foreach (var row in preview.Rows)
        {
            if (row.Status is not ("Ready" or "Warning")) { skipped++; continue; }
            await transaction.CreateSavepointAsync("absence_row", cancellationToken);
            try
            {
                if (!await db.Employees.AnyAsync(e => e.Id == row.Record.EmployeeId, cancellationToken))
                    throw new HrValidationException("Employee not found.");
                if (await db.EmployeeAbsences.AnyAsync(a => a.EmployeeId == row.Record.EmployeeId && a.AbsenceDate == row.Record.AbsenceDate, cancellationToken))
                    throw new HrConflictException("Absence already exists.");
                if (await db.LeaveRequests.AnyAsync(x => x.EmployeeId == row.Record.EmployeeId && x.Status == LeaveRequestStatuses.Approved && x.StartDate <= row.Record.AbsenceDate && x.EndDate >= row.Record.AbsenceDate, cancellationToken))
                    throw new HrConflictException("Approved leave covers this date.");
                if (await db.AttendanceRecords.AnyAsync(x =>
                        x.EmployeeId == row.Record.EmployeeId && x.AttendanceDate == row.Record.AbsenceDate && !x.IsDeleted &&
                        (x.Status != AttendanceValues.AbsentStatus || x.CheckIn != null || x.CheckOut != null ||
                         db.AttendancePunches.Any(punch => punch.AttendanceRecordId == x.Id)), cancellationToken))
                    throw new HrConflictException("Attendance conflict.");
                if (await db.HrExcuseMissions.AnyAsync(m => m.Status == "Approved" && m.EmployeeId == row.Record.EmployeeId && m.Date == row.Record.AbsenceDate, cancellationToken))
                    throw new HrConflictException("Approved excuse covers this date.");

                var absence = AbsenceImportMapper.Create(row.Record, DateTimeOffset.UtcNow);
                decimal? suggested = null;
                if (absence.Status == AbsenceValues.UnexcusedStatus)
                {
                    var basicSalary = await db.EmployeeCompensations.AsNoTracking()
                        .Where(x => x.EmployeeId == absence.EmployeeId && x.EffectiveFrom <= absence.AbsenceDate && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= absence.AbsenceDate))
                        .OrderByDescending(x => x.EffectiveFrom)
                        .Select(x => (decimal?)x.BasicSalary)
                        .FirstOrDefaultAsync(cancellationToken);
                    suggested = basicSalary / PayrollMonthDivisor;
                }
                absence.SynchronizePayrollImpact(suggested, DateTimeOffset.UtcNow);
                db.EmployeeAbsences.Add(absence);
                await db.SaveChangesAsync(cancellationToken);
                imported++;
            }
            catch (Exception error) when (error is HrException or ArgumentException or DbUpdateException)
            {
                await transaction.RollbackToSavepointAsync("absence_row", cancellationToken);
                if (error is HrConflictException || error is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }) skipped++;
                else failed++;
            }
            finally { db.ChangeTracker.Clear(); }
            await transaction.ReleaseSavepointAsync("absence_row", cancellationToken);
        }

        var result = new AbsenceImportResult(imported, skipped, failed);
        await Write(id, "AbsenceImportCompleted", result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyCollection<AbsenceImportHistory>> HistoryAsync(CancellationToken cancellationToken)
    {
        var uploads = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.Action == "AbsenceImportUploaded" && log.UserId == user.UserId)
            .OrderByDescending(log => log.Timestamp).Take(50)
            .Select(log => new { log.EntityId, log.NewValue, log.Timestamp, UserName = log.User == null ? "" : log.User.Username })
            .ToArrayAsync(cancellationToken);
        var ids = uploads.Select(item => item.EntityId).ToArray();
        var events = await db.HrAuditLogs.AsNoTracking()
            .Where(log => log.EntityType == Entity && ids.Contains(log.EntityId) && (log.Action == "AbsenceImportCompleted" || log.Action == "AbsenceImportPreviewed"))
            .OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        return uploads.Select(item => new AbsenceImportHistory(
            item.EntityId,
            Read<Uploaded>(item.NewValue).FileName,
            item.UserName,
            item.Timestamp,
            events.Where(log => log.EntityId == item.EntityId && log.Action == "AbsenceImportPreviewed").Select(log => (int?)Read<PreviewSaved>(log.NewValue).TotalRows).FirstOrDefault(),
            events.Where(log => log.EntityId == item.EntityId && log.Action == "AbsenceImportCompleted").Select(log => Read<AbsenceImportResult>(log.NewValue)).FirstOrDefault())).ToArray();
    }

    private static IEnumerable<(Guid EmployeeId, DateOnly Date)> Expand(Guid employeeId, DateOnly start, DateOnly end)
    {
        for (var date = start; date <= end; date = date.AddDays(1))
            yield return (employeeId, date);
    }

    private Task Write(Guid id, string action, object value, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditWriteRequest(action, Entity, id.ToString(), null, null, value, action), cancellationToken);

    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");
}
