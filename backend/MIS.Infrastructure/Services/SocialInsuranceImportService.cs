using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using Npgsql;

namespace MIS.Infrastructure.Services;

public sealed class SocialInsuranceImportService(ApplicationDbContext db, IHrFileStorage storage, IHrAuditService audit,
    ICurrentUserContext user, IWorkingCalendarCalculator calendar) : ISocialInsuranceImportService
{
    private void Access()
    {
        if (user.Roles.Contains("Admin") || user.Permissions.Contains("*")) return;
        if (!user.Roles.Any(r => r is "HrManager" or "HrOfficer") && !user.Permissions.Contains("hr.social_insurance.manage"))
            throw new HrForbiddenException("Social insurance management permission is required.");
    }
    private const string Entity = "SocialInsuranceImport";
    private const long MaximumBytes = ExcelImportLimits.MaximumBytes;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, string StorageKey, int TotalRows);

    public Task<HrImportFileTemplate> BuildTemplateAsync(CancellationToken cancellationToken)
    {
        Access();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HrImportFileTemplate(
            HrImportWorkbookBuilder.BuildSocialInsurance(),
            "Social_Insurance_Import_Template.xlsx",
            HrImportWorkbookBuilder.ExcelContentType));
    }

    public async Task<SocialInsuranceImportUpload> UploadAsync(HrUploadFile file, CancellationToken cancellationToken)
    {
        Access();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 50 MB.");
        var stored = await storage.SaveAsync("insurance-imports", file.FileName, file.ContentType, file.Content, MaximumBytes, cancellationToken);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, cancellationToken);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, cancellationToken);
            var id = Guid.NewGuid();
            await Write(id, "SocialInsuranceImportUploaded", new Uploaded(stored.OriginalFileName, stored.StorageKey, extension), cancellationToken);
            return new SocialInsuranceImportUpload(id, stored.OriginalFileName, sheets);
        }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken cancellationToken) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id
            && log.Action == "SocialInsuranceImportUploaded" && log.UserId == user.UserId, cancellationToken)
        ?? throw new HrNotFoundException("Insurance import was not found.");

    public async Task<SocialInsuranceImportPreview> PreviewAsync(Guid id, SocialInsuranceImportMapping mapping, CancellationToken cancellationToken)
    {
        Access();
        var upload = Read<Uploaded>((await OwnedUpload(id, cancellationToken)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "SocialInsuranceImportCompleted", cancellationToken))
            throw new HrConflictException("This insurance import is already complete.");
        if (mapping.Columns is null || mapping.Columns.Count > SocialInsuranceImportMapper.Fields.Length || mapping.Columns.Keys.Except(SocialInsuranceImportMapper.Fields).Any())
            throw new HrValidationException("Unsupported insurance field mapping. / تعيين حقول غير صالح");
        if (!new[] { "EmployeeNumber", "NationalId", "EmployeeId" }.Any(key => mapping.Columns.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)))
            throw new HrValidationException("Map an employee identifier. / يجب تعيين معرف الموظف");
        var selected = mapping.Columns.Values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        if (selected.Distinct().Count() != selected.Length) throw new HrValidationException("Map each source column only once. / لا يمكن تعيين العمود أكثر من مرة");
        await using var stream = await storage.OpenReadAsync(upload.StorageKey, cancellationToken);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, cancellationToken, sheetNames: mapping.SheetNames);
        var indexes = new Dictionary<string, int>();
        foreach (var pair in mapping.Columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }
        var employees = await db.Employees.AsNoTracking().Select(e => new SocialInsuranceImportMapper.EmployeeMatch(e.Id, e.EmployeeNumber, e.NationalId, e.FullName)).ToArrayAsync(cancellationToken);
        var active = await db.SocialInsuranceRecords.AsNoTracking().Where(r => r.InsuranceStatus != "Ended").Select(r => new { r.EmployeeId, r.SocialInsuranceNumber }).ToArrayAsync(cancellationToken);
        var employeeIds = active.Select(r => r.EmployeeId).ToHashSet();
        var numbers = active.Select(r => r.SocialInsuranceNumber).ToHashSet();
        var rows = new List<SocialInsuranceImportRow>();
        foreach (var cells in table.Rows)
        {
            var values = indexes.ToDictionary(pair => pair.Key, pair => pair.Value < cells.Length ? cells[pair.Value] : "");
            var row = SocialInsuranceImportMapper.Map(rows.Count + mapping.FirstDataRow, values, mapping.DateFormat, employees, employeeIds, numbers);
            rows.Add(row);
            if (row.Status is "Ready" or "Warning" && row.Record.InsuranceStatus != "Ended") { employeeIds.Add(row.Record.EmployeeId); numbers.Add(row.Record.SocialInsuranceNumber); }
        }
        var preview = new SocialInsuranceImportPreview(id, Guid.NewGuid(), rows);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(preview, Json));
        var stored = await storage.SaveAsync("insurance-import-previews", "preview.json", "application/json", content, MaximumBytes, cancellationToken);
        try { await Write(id, "SocialInsuranceImportPreviewed", new PreviewSaved(preview.PreviewId, stored.StorageKey, rows.Count), cancellationToken); }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return preview with { Rows = preview.Rows.Select(row => row with { Errors = row.Errors.Select(message => ApiTextLocalizer.Localize(message)).ToArray() }).ToArray() };
    }

    public async Task<SocialInsuranceImportResult> ConfirmAsync(Guid id, Guid previewId, CancellationToken cancellationToken, IReadOnlyCollection<int>? excludedRows = null)
    {
        Access();
        await OwnedUpload(id, cancellationToken);
        // The advisory transaction lock serializes confirmations of this batch, including retries.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", cancellationToken);
        var completed = await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "SocialInsuranceImportCompleted", cancellationToken);
        if (completed is not null) return Read<SocialInsuranceImportResult>(completed.NewValue);
        var events = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "SocialInsuranceImportPreviewed").OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault();
        if (saved is null || saved.PreviewId != previewId)
            throw new HrValidationException("Build and review the insurance preview before confirming.");
        await using var stream = await storage.OpenReadAsync(saved.StorageKey, cancellationToken);
        var preview = await JsonSerializer.DeserializeAsync<SocialInsuranceImportPreview>(stream, Json, cancellationToken)
            ?? throw new HrValidationException("The insurance preview could not be read.");
        var imported = 0; var skipped = 0; var failed = 0;
        var excluded = (excludedRows ?? []).ToHashSet();
        foreach (var row in preview.Rows)
        {
            if (excluded.Contains(row.Row) || row.Status is not ("Ready" or "Warning")) { skipped++; continue; }
            await transaction.CreateSavepointAsync("insurance_row", cancellationToken);
            try {
                if (!await db.Employees.AnyAsync(e => e.Id == row.Record.EmployeeId, cancellationToken)) throw new HrValidationException("Employee not found.");
                if (await db.SocialInsuranceRecords.AnyAsync(r => r.InsuranceStatus != "Ended" && (r.EmployeeId == row.Record.EmployeeId || (row.Record.InsuranceStatus != "Ended" && r.SocialInsuranceNumber == row.Record.SocialInsuranceNumber)), cancellationToken)) throw new HrConflictException("Existing active insurance record.");
                db.SocialInsuranceRecords.Add(SocialInsuranceImportMapper.Create(row.Record));
                await db.SaveChangesAsync(cancellationToken); imported++;
            }
            catch (Exception error) when (error is HrException or ArgumentException or DbUpdateException)
            {
                await transaction.RollbackToSavepointAsync("insurance_row", cancellationToken);
                if (error is HrConflictException || error is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }) skipped++;
                else failed++;
            }
            finally { db.ChangeTracker.Clear(); }
            await transaction.ReleaseSavepointAsync("insurance_row", cancellationToken);
        }
        var result = new SocialInsuranceImportResult(imported, skipped, failed);
        await Write(id, "SocialInsuranceImportCompleted", result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyCollection<SocialInsuranceImportHistory>> HistoryAsync(CancellationToken cancellationToken)
    {
        Access();
        var uploads = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && log.Action == "SocialInsuranceImportUploaded" && log.UserId == user.UserId)
            .OrderByDescending(log => log.Timestamp).Take(50).Select(log => new { log.EntityId, log.NewValue, log.Timestamp, UserName = log.User == null ? "" : log.User.Username }).ToArrayAsync(cancellationToken);
        var ids = uploads.Select(item => item.EntityId).ToArray();
        var events = await db.HrAuditLogs.AsNoTracking().Where(log => log.EntityType == Entity && ids.Contains(log.EntityId) && (log.Action == "SocialInsuranceImportCompleted" || log.Action == "SocialInsuranceImportPreviewed"))
            .OrderByDescending(log => log.Timestamp).ToArrayAsync(cancellationToken);
        return uploads.Select(item => new SocialInsuranceImportHistory(item.EntityId, Read<Uploaded>(item.NewValue).FileName, item.UserName, item.Timestamp,
            events.Where(log => log.EntityId == item.EntityId && log.Action == "SocialInsuranceImportPreviewed").Select(log => (int?)Read<PreviewSaved>(log.NewValue).TotalRows).FirstOrDefault(),
            events.Where(log => log.EntityId == item.EntityId && log.Action == "SocialInsuranceImportCompleted").Select(log => Read<SocialInsuranceImportResult>(log.NewValue)).FirstOrDefault())).ToArray();
    }

    private Task Write(Guid id, string action, object value, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditWriteRequest(action, Entity, id.ToString(), null, null, value, action), cancellationToken);
    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");
}
