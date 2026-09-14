using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using Npgsql;

namespace MIS.Infrastructure.Services;

public sealed class EmployeeImportService(ApplicationDbContext db, IHrFileStorage storage, IHrAuditService audit,
    ICurrentUserContext user, IEmployeeCreationService creation, IWorkingCalendarCalculator calendar) : IEmployeeImportService
{
    private const string Entity = "EmployeeImport";
    private const long MaximumBytes = 20 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, string StorageKey, int TotalRows);

    public async Task<EmployeeImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 20 MB.");
        var stored = await storage.SaveAsync("employee-imports", file.FileName, file.ContentType, file.Content, MaximumBytes, cancellationToken);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, cancellationToken);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, cancellationToken);
            var id = Guid.NewGuid();
            await Write(id, "EmployeeImportUploaded", new Uploaded(stored.OriginalFileName, stored.StorageKey, extension), cancellationToken);
            return new EmployeeImportUpload(id, stored.OriginalFileName, sheets);
        }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken cancellationToken) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id
            && log.Action == "EmployeeImportUploaded" && log.UserId == user.UserId, cancellationToken)
        ?? throw new HrNotFoundException("Employee import was not found.");

    public async Task<EmployeeImportPreview> PreviewAsync(Guid id, EmployeeImportMapping mapping, CancellationToken cancellationToken)
    {
        var upload = Read<Uploaded>((await OwnedUpload(id, cancellationToken)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportCompleted", cancellationToken))
            throw new HrConflictException("This employee import is already complete.");
        if (mapping.Columns.Count > EmployeeImportMapper.Fields.Length || mapping.Columns.Keys.Except(EmployeeImportMapper.Fields).Any())
            throw new HrValidationException("Unsupported employee field mapping.");
        await using var stream = await storage.OpenReadAsync(upload.StorageKey, cancellationToken);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, cancellationToken, mapping.Columns.GetValueOrDefault("MobileNumber"));
        var indexes = new Dictionary<string, int>();
        foreach (var pair in mapping.Columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }
        var departments = await db.Departments.AsNoTracking().Select(item => new EmployeeImportMapper.Lookup(item.Id, item.Name, item.Code, item.NameArabic)).ToArrayAsync(cancellationToken);
        var positions = await db.Positions.AsNoTracking().Where(item => item.IsActive).Select(item => new EmployeeImportMapper.Lookup(item.Id, item.Name, item.Code, item.NameArabic)).ToArrayAsync(cancellationToken);
        var rows = new List<EmployeeImportRow>();
        var existingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingNationalIds = new HashSet<string>(StringComparer.Ordinal);
        var sourceNumbers = indexes.TryGetValue("EmployeeNumber", out var numberIndex)
            ? table.Rows.Where(row => numberIndex < row.Length).Select(row => row[numberIndex].Trim().ToUpperInvariant()).ToArray() : [];
        var sourceIds = indexes.TryGetValue("NationalId", out var idIndex)
            ? table.Rows.Where(row => idIndex < row.Length).Select(row => row[idIndex].Trim()).ToArray() : [];
        var existing = await db.Employees.AsNoTracking().Where(employee => sourceNumbers.Contains(employee.EmployeeNumber.ToUpper())
            || (employee.NationalId != null && sourceIds.Contains(employee.NationalId)))
            .Select(employee => new { employee.EmployeeNumber, employee.NationalId }).ToArrayAsync(cancellationToken);
        foreach (var employee in existing) { existingNumbers.Add(employee.EmployeeNumber); if (employee.NationalId is not null) existingNationalIds.Add(employee.NationalId); }
        var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nationalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cells in table.Rows)
        {
            var values = indexes.ToDictionary(pair => pair.Key, pair => pair.Value < cells.Length ? cells[pair.Value] : "");
            var errors = new List<string>();
            var request = EmployeeImportMapper.Map(values, mapping.DateFormat, departments, positions, errors);
            var duplicate = (!string.IsNullOrWhiteSpace(request.EmployeeNumber) && !numbers.Add(request.EmployeeNumber.Trim()))
                | (!string.IsNullOrWhiteSpace(request.NationalId) && !nationalIds.Add(request.NationalId));
            var status = duplicate || existingNumbers.Contains(request.EmployeeNumber) || existingNationalIds.Contains(request.NationalId) ? "Existing" : "Ready";
            if (duplicate) errors.Add("Duplicate employee within this file; skipped.");
            else if (status == "Existing") errors.Add("Existing employee; skipped.");
            try { await creation.ValidateAsync(request, cancellationToken); }
            catch (HrConflictException) { status = "Existing"; errors.Add("Existing employee; skipped."); }
            catch (HrValidationException error) { errors.AddRange(error.Errors.Count > 0 ? error.Errors : [error.Message]); }
            if (status != "Existing" && errors.Count > 0) status = "Error";
            if (status == "Ready" && request.WorkEndDate.HasValue && request.Status is null)
            { status = "Warning"; errors.Add("End work date is recorded; employee status is unchanged, as in manual creation."); }
            rows.Add(new EmployeeImportRow(rows.Count + 1, request, values.GetValueOrDefault("Department", ""), values.GetValueOrDefault("Position", ""), status, errors));
        }
        var preview = new EmployeeImportPreview(id, Guid.NewGuid(), rows);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(preview, Json));
        var stored = await storage.SaveAsync("employee-import-previews", "preview.json", "application/json", content, MaximumBytes, cancellationToken);
        try { await Write(id, "EmployeeImportPreviewed", new PreviewSaved(preview.PreviewId, stored.StorageKey, rows.Count), cancellationToken); }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return Localize(preview);
    }

    public async Task<EmployeeImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken)
    {
        await OwnedUpload(id, cancellationToken);
        // The advisory transaction lock serializes confirmations of this batch, including retries.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", cancellationToken);
        var completed = await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportCompleted", cancellationToken);
        if (completed is not null) return Read<EmployeeImportResult>(completed.NewValue);
        var events = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeImportPreviewed").OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault();
        if (saved is null || saved.PreviewId != previewId)
            throw new HrValidationException("Build and review the employee preview before confirming.");
        await using var stream = await storage.OpenReadAsync(saved.StorageKey, cancellationToken);
        var preview = await JsonSerializer.DeserializeAsync<EmployeeImportPreview>(stream, Json, cancellationToken)
            ?? throw new HrValidationException("The employee preview could not be read.");
        var imported = 0; var skipped = 0; var failed = 0;
        foreach (var row in preview.Rows)
        {
            if (row.Status is not ("Ready" or "Warning")) { skipped++; continue; }
            await transaction.CreateSavepointAsync("employee_row", cancellationToken);
            try { await creation.CreateAsync(row.Employee, cancellationToken); imported++; }
            catch (Exception error) when (error is HrException or ArgumentException or DbUpdateException)
            {
                await transaction.RollbackToSavepointAsync("employee_row", cancellationToken);
                if (error is HrConflictException || error is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }) skipped++;
                else failed++;
            }
            finally { db.ChangeTracker.Clear(); }
            await transaction.ReleaseSavepointAsync("employee_row", cancellationToken);
        }
        var result = new EmployeeImportResult(imported, skipped, failed);
        await Write(id, "EmployeeImportCompleted", result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyCollection<EmployeeImportHistory>> HistoryAsync(CancellationToken cancellationToken)
    {
        var uploads = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.Action == "EmployeeImportUploaded" && log.UserId == user.UserId)
            .OrderByDescending(log => log.Timestamp).Take(50).Select(log => new { log.EntityId, log.NewValue, log.Timestamp, UserName = log.User == null ? "" : log.User.Username }).ToArrayAsync(cancellationToken);
        var ids = uploads.Select(item => item.EntityId).ToArray();
        var events = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && ids.Contains(log.EntityId) && (log.Action == "EmployeeImportCompleted" || log.Action == "EmployeeImportPreviewed"))
            .OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        return uploads.Select(item => new EmployeeImportHistory(item.EntityId, Read<Uploaded>(item.NewValue).FileName, item.UserName, item.Timestamp,
            events.Where(log => log.EntityId == item.EntityId && log.Action == "EmployeeImportPreviewed").Select(log => (int?)Read<PreviewSaved>(log.NewValue).TotalRows).FirstOrDefault(),
            events.Where(log => log.EntityId == item.EntityId && log.Action == "EmployeeImportCompleted").Select(log => Read<EmployeeImportResult>(log.NewValue)).FirstOrDefault())).ToArray();
    }

    private Task Write(Guid id, string action, object value, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditWriteRequest(action, Entity, id.ToString(), null, null, value, action), cancellationToken);
    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");
    private static EmployeeImportPreview Localize(EmployeeImportPreview preview) => preview with
    { Rows = preview.Rows.Select(row => row with { Errors = ApiTextLocalizer.LocalizeErrors(row.Errors) }).ToArray() };
}
