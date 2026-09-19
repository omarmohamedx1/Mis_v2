using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrAttendanceImportService : IHrAttendanceImportService
{
    private const long MaximumImportBytes = 20 * 1024 * 1024;
    private const int ProcessingBatchSize = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".csv", ".xls", ".xlsx" };
    private readonly ApplicationDbContext _dbContext;
    private readonly IWorkingCalendarCalculator _calendar;
    private readonly ICurrentUserContext _currentUser;
    private readonly IHrAuditService _audit;
    private readonly IHrFileStorage _fileStorage;
    private readonly AttendanceImportParser _parser;

    public HrAttendanceImportService(
        ApplicationDbContext dbContext,
        IWorkingCalendarCalculator calendar,
        ICurrentUserContext currentUser,
        IHrAuditService audit,
        IHrFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _calendar = calendar;
        _currentUser = currentUser;
        _audit = audit;
        _fileStorage = fileStorage;
        _parser = new AttendanceImportParser(calendar);
    }

    public Task<HrImportFileTemplate> BuildTemplateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HrImportFileTemplate(
            HrImportWorkbookBuilder.BuildAttendance(),
            "Attendance_Import_Template.xlsx",
            HrImportWorkbookBuilder.ExcelContentType));
    }

    public async Task<AttendanceImportUploadDto> UploadAsync(
        AttendanceImportFile file,
        CancellationToken cancellationToken)
    {
        var extension = ValidateUpload(file);
        StoredFileDto? stored = null;
        var persisted = false;
        try
        {
            stored = await _fileStorage.SaveAsync(
                "attendance-imports",
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                file.Content,
                MaximumImportBytes,
                cancellationToken);
            await using var storedStream = await _fileStorage.OpenReadAsync(stored.StorageKey, cancellationToken);
            await ValidateSignatureAsync(storedStream, extension, cancellationToken);
            var sheets = await _parser.InspectAsync(storedStream, extension, cancellationToken);

            if (await _dbContext.AttendanceImportBatches.AsNoTracking().AnyAsync(
                    item => item.FileHash == stored.Sha256Hash &&
                            item.Status != AttendanceValues.FailedBatchStatus &&
                            item.Status != AttendanceValues.CancelledBatchStatus,
                    cancellationToken))
                throw new HrConflictException("This attendance file has already been uploaded.");

            var batch = new AttendanceImportBatch(
                stored.OriginalFileName,
                stored.ContentType,
                stored.Length,
                stored.Sha256Hash,
                stored.StorageKey,
                _currentUser.UserId,
                DateTimeOffset.UtcNow);
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            _dbContext.AttendanceImportBatches.Add(batch);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(new AuditWriteRequest(
                "AttendanceImportUploaded",
                nameof(AttendanceImportBatch),
                batch.Id.ToString(),
                null,
                null,
                new { batch.FileName, batch.FileHash, batch.FileSize, batch.Status },
                "Uploaded attendance source file."), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            persisted = true;
            return new AttendanceImportUploadDto(
                batch.Id,
                batch.FileName,
                batch.FileSize,
                batch.FileHash,
                batch.Status,
                sheets,
                batch.UploadedAt);
        }
        finally
        {
            if (stored is not null && !persisted)
            {
                await _fileStorage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            }
        }
    }

    public async Task<AttendanceImportBatchDto> BuildPreviewAsync(
        Guid batchId,
        AttendanceImportColumnMappingRequest mapping,
        CancellationToken cancellationToken)
    {
        var batch = await GetTrackedBatchAsync(batchId, cancellationToken);
        if (batch.Status is AttendanceValues.ConfirmedBatchStatus or AttendanceValues.CancelledBatchStatus)
            throw new HrConflictException("A confirmed or cancelled import cannot be previewed again.");
        if (batch.Status == AttendanceValues.FailedBatchStatus)
            throw new HrConflictException("A failed import must be uploaded again.");

        try
        {
            var extension = Path.GetExtension(batch.FileName).ToLowerInvariant();
            await using var stream = await _fileStorage.OpenReadAsync(batch.StorageKey, cancellationToken);
            var parsed = await _parser.ParseAsync(stream, extension, mapping, cancellationToken);
            var employeeMatches = await MatchEmployeesAsync(parsed.Groups, cancellationToken);
            var existingAttendance = await LoadExistingAttendanceAsync(employeeMatches.Values.SelectMany(item => item).Select(item => item.Id), parsed.Groups, cancellationToken);
            var previewRows = BuildPreviewRows(batch.Id, parsed, mapping, employeeMatches, existingAttendance, DateTimeOffset.UtcNow);
            var summary = BuildSummary(previewRows);
            var mappingJson = JsonSerializer.Serialize(mapping, JsonOptions);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await _dbContext.AttendanceImportRows.Where(item => item.BatchId == batch.Id).ExecuteDeleteAsync(cancellationToken);
            _dbContext.AttendanceImportRows.AddRange(previewRows);
            batch.SetPreview(
                mappingJson,
                previewRows.Count,
                summary.ValidRows,
                summary.InvalidRows,
                summary.EmployeeNotFoundRows,
                summary.DuplicateRows,
                summary.MissingCheckInRows,
                summary.MissingCheckOutRows,
                DateTimeOffset.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var result = MapBatch(batch);
            await _audit.WriteAsync(new AuditWriteRequest(
                "AttendanceImportPreviewed",
                nameof(AttendanceImportBatch),
                batch.Id.ToString(),
                null,
                null,
                summary,
                $"Validated {previewRows.Count:N0} employee/day attendance groups."), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HrValidationException)
        {
            // Mapping and format validation errors are correctable by the user; keep the batch retryable.
            throw;
        }
        catch (Exception exception)
        {
            await TryMarkFailedAsync(batch.Id, exception.Message);
            throw;
        }
    }

    public async Task<AttendanceImportBatchDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.AttendanceImportBatches.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new HrNotFoundException("Attendance import batch was not found.");
        return MapBatch(batch);
    }

    public async Task<PagedAttendanceImportPreviewDto> GetPreviewAsync(
        Guid batchId,
        AttendanceImportPreviewFilterDto filter,
        CancellationToken cancellationToken)
    {
        ValidatePage(filter.Page, filter.PageSize);
        var isArabic = ApiTextLocalizer.IsArabic;
        var batch = await _dbContext.AttendanceImportBatches.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken)
            ?? throw new HrNotFoundException("Attendance import batch was not found.");
        if (batch.Status == AttendanceValues.UploadedBatchStatus)
            throw new HrConflictException("Build the import preview first.");

        var query = _dbContext.AttendanceImportRows.AsNoTracking().Where(item => item.BatchId == batchId);
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = NormalizeCategory(filter.Category)
                ?? throw new HrValidationException("Attendance preview category is invalid.");
            var categoryJson = JsonSerializer.Serialize(new[] { category }, JsonOptions);
            query = query.Where(item => EF.Functions.JsonContains(item.CategoriesJson, categoryJson));
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(item =>
                (item.SourceEmployeeNumber != null && EF.Functions.ILike(item.SourceEmployeeNumber, pattern)) ||
                (item.SourceEmployeeName != null && EF.Functions.ILike(item.SourceEmployeeName, pattern)) ||
                (item.Employee != null &&
                    (EF.Functions.ILike(item.Employee.EmployeeNumber, pattern) ||
                     EF.Functions.ILike(item.Employee.FullName, pattern) ||
                     (item.Employee.FullNameArabic != null && EF.Functions.ILike(item.Employee.FullNameArabic, pattern)) ||
                     (item.Employee.FullNameEnglish != null && EF.Functions.ILike(item.Employee.FullNameEnglish, pattern)))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.AttendanceDate).ThenBy(item => item.SourceEmployeeNumber).ThenBy(item => item.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(item => new PreviewProjection(
                item.Id,
                item.BatchId,
                item.SourceRowNumbersJson,
                item.SourceEmployeeNumber,
                item.SourceEmployeeName,
                item.EmployeeId,
                item.Employee == null ? null : item.Employee.EmployeeNumber,
                item.Employee == null
                    ? null
                    : isArabic
                        ? item.Employee.FullNameArabic ?? item.Employee.FullName
                        : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                item.AttendanceDate,
                item.CheckIn,
                item.CheckOut,
                item.PunchesJson,
                item.CanImport,
                item.CategoriesJson,
                item.ErrorsJson,
                item.SourceRowsJson))
            .ToListAsync(cancellationToken);

        return new PagedAttendanceImportPreviewDto(
            rows.Select(MapPreview).ToArray(),
            Summary(batch),
            totalCount,
            filter.Page,
            filter.PageSize,
            Pages(totalCount, filter.PageSize));
    }

    public async Task<AttendanceImportConfirmResultDto> ConfirmAsync(
        Guid batchId,
        ConfirmAttendanceImportRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var batch = await GetTrackedBatchAsync(batchId, cancellationToken);
        if (batch.Status != AttendanceValues.PreviewReadyBatchStatus)
            throw new HrConflictException("Only a preview-ready attendance import can be confirmed.");
        if (await _dbContext.AttendanceImportBatches.AsNoTracking().AnyAsync(
                item => item.Id != batch.Id && item.FileHash == batch.FileHash && item.Status == AttendanceValues.ConfirmedBatchStatus,
                cancellationToken))
            throw new HrConflictException("This attendance file has already been confirmed.");

        var totalPreviewRows = await _dbContext.AttendanceImportRows.CountAsync(item => item.BatchId == batch.Id, cancellationToken);
        var scheduleCache = new Dictionary<DateOnly, WorkDaySchedule>();
        var imported = 0;
        var newlyDuplicated = 0;
        var processedPreviewRows = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = await _dbContext.AttendanceImportRows.AsNoTracking()
                .Where(item => item.BatchId == batch.Id && item.CanImport)
                .OrderBy(item => item.Id)
                .Skip(processedPreviewRows)
                .Take(ProcessingBatchSize)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0) break;
            processedPreviewRows += rows.Count;

            var accepted = rows.Where(row => request.IncludeRowsWithWarnings || !HasMissingPunchWarning(row.CategoriesJson)).ToArray();
            await EnsureEmployeeLifecycleStillValidAsync(accepted, cancellationToken);
            var existing = await LoadExistingKeysAsync(accepted, cancellationToken);
            var approvedLeaves = await LoadApprovedLeaveKeysAsync(accepted, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            foreach (var row in accepted)
            {
                if (!row.EmployeeId.HasValue || !row.AttendanceDate.HasValue) continue;
                var key = new AttendanceKey(row.EmployeeId.Value, row.AttendanceDate.Value);
                if (existing.Contains(key))
                {
                    newlyDuplicated++;
                    continue;
                }

                if (!scheduleCache.TryGetValue(row.AttendanceDate.Value, out var schedule))
                {
                    schedule = await _calendar.GetScheduleAsync(row.AttendanceDate.Value, cancellationToken);
                    scheduleCache[row.AttendanceDate.Value] = schedule;
                }
                var calculation = CalculateImported(schedule, row.CheckIn, row.CheckOut, approvedLeaves.Contains(key));
                var record = new AttendanceRecord(
                    row.EmployeeId.Value,
                    row.AttendanceDate.Value,
                    row.CheckIn,
                    row.CheckOut,
                    calculation.WorkingMinutes,
                    calculation.LateMinutes,
                    calculation.EarlyLeaveMinutes,
                    calculation.OvertimeMinutes,
                    calculation.Status,
                    AttendanceValues.ExcelImportSource,
                    null,
                    batch.Id,
                    false,
                    _currentUser.UserId,
                    now);
                _dbContext.AttendanceRecords.Add(record);
                AddImportedPunches(record.Id, row, now);
                existing.Add(key);
                imported++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            DetachProcessedAttendance();
        }

        batch = await GetTrackedBatchAsync(batchId, cancellationToken);
        var confirmedAt = DateTimeOffset.UtcNow;
        batch.Confirm(imported, request.Notes, confirmedAt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var duplicateRows = batch.DuplicateRows + newlyDuplicated;
        var failedRows = Math.Max(0, batch.InvalidRows - batch.DuplicateRows);
        var skippedRows = Math.Max(0, totalPreviewRows - imported);
        var result = new AttendanceImportConfirmResultDto(
            batch.Id,
            imported,
            skippedRows,
            duplicateRows,
            failedRows,
            confirmedAt);
        await _audit.WriteAsync(new AuditWriteRequest(
            "AttendanceImported",
            nameof(AttendanceImportBatch),
            batch.Id.ToString(),
            null,
            null,
            result,
            $"Imported {imported:N0} attendance records from {batch.FileName}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PagedAttendanceImportHistoryDto> GetHistoryAsync(
        AttendanceImportHistoryFilterDto filter,
        CancellationToken cancellationToken)
    {
        ValidatePage(filter.Page, filter.PageSize);
        if (filter.UploadedFrom.HasValue && filter.UploadedTo.HasValue && filter.UploadedTo < filter.UploadedFrom)
            throw new HrValidationException("Uploaded to cannot be before uploaded from.");
        var query = _dbContext.AttendanceImportBatches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(item => EF.Functions.ILike(item.FileName, pattern) || EF.Functions.ILike(item.FileHash, pattern));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = AttendanceValues.NormalizeBatchStatus(filter.Status)
                ?? throw new HrValidationException("Attendance import status is invalid.");
            query = query.Where(item => item.Status == status);
        }
        if (filter.UploadedFrom.HasValue)
        {
            var from = new DateTimeOffset(filter.UploadedFrom.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(item => item.UploadedAt >= from);
        }
        if (filter.UploadedTo.HasValue)
        {
            var to = new DateTimeOffset(filter.UploadedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(item => item.UploadedAt < to);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var records = await query.OrderByDescending(item => item.UploadedAt).ThenByDescending(item => item.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(item => new HistoryProjection(
                item.Id,
                item.FileName,
                item.FileHash,
                item.Status,
                item.TotalRows,
                item.ValidRows,
                item.InvalidRows,
                item.EmployeeNotFoundRows,
                item.DuplicateRows,
                item.MissingCheckInRows,
                item.MissingCheckOutRows,
                item.MappingJson != null,
                item.ImportedRecords,
                item.UploadedByUserId,
                item.UploadedByUser.Username,
                item.UploadedAt,
                item.ConfirmedAt))
            .ToListAsync(cancellationToken);
        var items = records.Select(item => new AttendanceImportHistoryItemDto(
            item.Id,
            item.FileName,
            item.FileHash,
            item.Status,
            item.HasSummary ? new AttendanceImportSummaryDto(
                item.TotalRows, item.ValidRows, item.InvalidRows, item.EmployeeNotFoundRows,
                item.DuplicateRows, item.MissingCheckInRows, item.MissingCheckOutRows) : null,
            item.ImportedRecords,
            item.UploadedByUserId,
            item.UploadedByUsername,
            item.UploadedAt,
            item.ConfirmedAt)).ToArray();
        return new PagedAttendanceImportHistoryDto(items, totalCount, filter.Page, filter.PageSize, Pages(totalCount, filter.PageSize));
    }

    public async Task<AttendanceImportBatchDto> CancelAsync(
        Guid batchId,
        CancelAttendanceImportRequest request,
        CancellationToken cancellationToken)
    {
        var batch = await GetTrackedBatchAsync(batchId, cancellationToken);
        if (batch.Status == AttendanceValues.ConfirmedBatchStatus)
            throw new HrConflictException("A confirmed attendance import cannot be cancelled.");
        if (batch.Status == AttendanceValues.CancelledBatchStatus)
            throw new HrConflictException("The attendance import is already cancelled.");
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        batch.Cancel(notes, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = MapBatch(batch);
        await _audit.WriteAsync(new AuditWriteRequest(
            "AttendanceImportCancelled",
            nameof(AttendanceImportBatch),
            batch.Id.ToString(),
            null,
            null,
            new { batch.Status, batch.CancelledAt, batch.Notes },
            notes ?? "Cancelled attendance import."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<Dictionary<string, List<EmployeeMatch>>> MatchEmployeesAsync(
        IReadOnlyCollection<ParsedAttendanceGroup> groups,
        CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var numbers = groups.Select(item => item.SourceEmployeeNumber?.Trim().ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
        var result = new Dictionary<string, List<EmployeeMatch>>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in numbers.Chunk(1_000))
        {
            var matches = await _dbContext.Employees.AsNoTracking()
                .Where(item => chunk.Contains(item.EmployeeNumber.ToLower()))
                .Select(item => new EmployeeMatch(
                    item.Id,
                    item.EmployeeNumber,
                    isArabic ? item.FullNameArabic ?? item.FullName : item.FullNameEnglish ?? item.FullName,
                    item.HireDate,
                    item.TerminationDate))
                .ToListAsync(cancellationToken);
            foreach (var match in matches)
            {
                var key = match.EmployeeNumber.Trim().ToLowerInvariant();
                if (!result.TryGetValue(key, out var list)) result[key] = list = [];
                list.Add(match);
            }
        }
        return result;
    }

    private async Task<HashSet<AttendanceKey>> LoadExistingAttendanceAsync(
        IEnumerable<Guid> employeeIds,
        IReadOnlyCollection<ParsedAttendanceGroup> groups,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds.Distinct().ToArray();
        var dates = groups.Where(item => item.AttendanceDate.HasValue).Select(item => item.AttendanceDate!.Value).ToArray();
        var result = new HashSet<AttendanceKey>();
        if (ids.Length == 0 || dates.Length == 0) return result;
        var minimum = dates.Min();
        var maximum = dates.Max();
        foreach (var chunk in ids.Chunk(1_000))
        {
            var records = await _dbContext.AttendanceRecords.AsNoTracking()
                .Where(item => chunk.Contains(item.EmployeeId) && item.AttendanceDate >= minimum && item.AttendanceDate <= maximum && !item.IsDeleted)
                .Select(item => new AttendanceKey(item.EmployeeId, item.AttendanceDate))
                .ToListAsync(cancellationToken);
            result.UnionWith(records);
        }
        return result;
    }

    private static List<AttendanceImportRow> BuildPreviewRows(
        Guid batchId,
        ParsedAttendanceImport parsed,
        AttendanceImportColumnMappingRequest mapping,
        IReadOnlyDictionary<string, List<EmployeeMatch>> employees,
        IReadOnlySet<AttendanceKey> existingAttendance,
        DateTimeOffset createdAt)
    {
        var rows = new List<AttendanceImportRow>(parsed.Groups.Count);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(mapping.TimeZoneId);
        var companyToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        foreach (var group in parsed.Groups)
        {
            var errors = group.Errors.ToList();
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var checkInUtc = group.CheckIn?.ToUniversalTime();
            var checkOutUtc = group.CheckOut?.ToUniversalTime();
            var punchesUtc = group.Punches.Select(item => item.Instant.ToUniversalTime()).ToArray();
            if (mapping.Layout == HrAttendanceImportLayouts.CheckInCheckOutColumns && group.SourceRowNumbers.Count > 1)
            {
                categories.Add(HrAttendanceImportCategories.Duplicate);
                errors.Add("The file contains more than one check-in/check-out row for this employee and date.");
            }
            EmployeeMatch? employee = null;
            if (string.IsNullOrWhiteSpace(group.SourceEmployeeNumber))
            {
                categories.Add(HrAttendanceImportCategories.EmployeeNotFound);
            }
            else if (!employees.TryGetValue(group.SourceEmployeeNumber.Trim().ToLowerInvariant(), out var matches) || matches.Count == 0)
            {
                categories.Add(HrAttendanceImportCategories.EmployeeNotFound);
                errors.Add("Employee ID was not found.");
            }
            else if (matches.Count > 1)
            {
                categories.Add(HrAttendanceImportCategories.EmployeeNotFound);
                errors.Add("Employee ID matched more than one employee because of case-insensitive duplicates.");
            }
            else
            {
                employee = matches[0];
            }

            if (employee is not null && group.AttendanceDate.HasValue)
            {
                if (employee.HireDate.HasValue && group.AttendanceDate.Value < employee.HireDate.Value)
                    errors.Add("Attendance date is before the employee hire date.");
                if (employee.TerminationDate.HasValue && group.AttendanceDate.Value > employee.TerminationDate.Value)
                    errors.Add("Attendance date is after the employee termination date.");
                if (group.AttendanceDate.Value > companyToday)
                    errors.Add("Attendance cannot be imported for a future date.");
            }

            if (employee is not null && group.AttendanceDate.HasValue && existingAttendance.Contains(new AttendanceKey(employee.Id, group.AttendanceDate.Value)))
            {
                categories.Add(HrAttendanceImportCategories.Duplicate);
                errors.Add("Attendance already exists for this employee and date.");
            }
            if (!group.CheckIn.HasValue) categories.Add(HrAttendanceImportCategories.MissingCheckIn);
            if (!group.CheckOut.HasValue) categories.Add(HrAttendanceImportCategories.MissingCheckOut);

            var fatal = errors.Count > 0 || employee is null || !group.AttendanceDate.HasValue || categories.Contains(HrAttendanceImportCategories.Duplicate);
            categories.Add(fatal ? HrAttendanceImportCategories.Invalid : HrAttendanceImportCategories.Valid);
            rows.Add(new AttendanceImportRow(
                batchId,
                JsonSerializer.Serialize(group.SourceRowNumbers, JsonOptions),
                JsonSerializer.Serialize(group.SourceRows, JsonOptions),
                group.SourceEmployeeNumber,
                group.SourceEmployeeName,
                employee?.Id,
                group.AttendanceDate,
                checkInUtc,
                checkOutUtc,
                JsonSerializer.Serialize(punchesUtc, JsonOptions),
                JsonSerializer.Serialize(categories.Order(StringComparer.OrdinalIgnoreCase), JsonOptions),
                JsonSerializer.Serialize(errors.Distinct(StringComparer.OrdinalIgnoreCase), JsonOptions),
                !fatal,
                createdAt));
        }
        return rows;
    }

    private static AttendanceImportSummaryDto BuildSummary(IReadOnlyCollection<AttendanceImportRow> rows) => new(
        rows.Count,
        rows.Count(item => item.CanImport),
        rows.Count(item => !item.CanImport),
        rows.Count(item => ContainsCategory(item.CategoriesJson, HrAttendanceImportCategories.EmployeeNotFound)),
        rows.Count(item => ContainsCategory(item.CategoriesJson, HrAttendanceImportCategories.Duplicate)),
        rows.Count(item => ContainsCategory(item.CategoriesJson, HrAttendanceImportCategories.MissingCheckIn)),
        rows.Count(item => ContainsCategory(item.CategoriesJson, HrAttendanceImportCategories.MissingCheckOut)));

    private async Task<HashSet<AttendanceKey>> LoadExistingKeysAsync(
        IReadOnlyCollection<AttendanceImportRow> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Where(item => item.EmployeeId.HasValue).Select(item => item.EmployeeId!.Value).Distinct().ToArray();
        var dates = rows.Where(item => item.AttendanceDate.HasValue).Select(item => item.AttendanceDate!.Value).ToArray();
        var result = new HashSet<AttendanceKey>();
        if (ids.Length == 0 || dates.Length == 0) return result;
        var minimum = dates.Min();
        var maximum = dates.Max();
        var existing = await _dbContext.AttendanceRecords.AsNoTracking()
            .Where(item => ids.Contains(item.EmployeeId) && item.AttendanceDate >= minimum && item.AttendanceDate <= maximum && !item.IsDeleted)
            .Select(item => new AttendanceKey(item.EmployeeId, item.AttendanceDate))
            .ToListAsync(cancellationToken);
        result.UnionWith(existing);
        return result;
    }

    private async Task<HashSet<AttendanceKey>> LoadApprovedLeaveKeysAsync(
        IReadOnlyCollection<AttendanceImportRow> rows,
        CancellationToken cancellationToken)
    {
        var employeeIds = rows.Where(item => item.EmployeeId.HasValue)
            .Select(item => item.EmployeeId!.Value)
            .Distinct()
            .ToArray();
        var dates = rows.Where(item => item.AttendanceDate.HasValue)
            .Select(item => item.AttendanceDate!.Value)
            .ToArray();
        var result = new HashSet<AttendanceKey>();
        if (employeeIds.Length == 0 || dates.Length == 0) return result;

        var minimum = dates.Min();
        var maximum = dates.Max();
        var leaves = await _dbContext.LeaveRequests.AsNoTracking()
            .Where(item => employeeIds.Contains(item.EmployeeId) &&
                           item.Status == LeaveRequestStatuses.Approved &&
                           item.StartDate <= maximum &&
                           item.EndDate >= minimum)
            .Select(item => new { item.EmployeeId, item.StartDate, item.EndDate })
            .ToListAsync(cancellationToken);

        var relevantDates = dates.Distinct().ToArray();
        foreach (var leave in leaves)
        foreach (var date in relevantDates)
        {
            if (date >= leave.StartDate && date <= leave.EndDate)
                result.Add(new AttendanceKey(leave.EmployeeId, date));
        }
        return result;
    }

    private async Task EnsureEmployeeLifecycleStillValidAsync(
        IReadOnlyCollection<AttendanceImportRow> rows,
        CancellationToken cancellationToken)
    {
        var employeeIds = rows.Where(item => item.EmployeeId.HasValue)
            .Select(item => item.EmployeeId!.Value)
            .Distinct()
            .ToArray();
        var employees = await _dbContext.Employees.AsNoTracking()
            .Where(item => employeeIds.Contains(item.Id))
            .Select(item => new { item.Id, item.HireDate, item.TerminationDate })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var row in rows)
        {
            if (!row.EmployeeId.HasValue || !row.AttendanceDate.HasValue ||
                !employees.TryGetValue(row.EmployeeId.Value, out var employee) ||
                (employee.HireDate.HasValue && row.AttendanceDate.Value < employee.HireDate.Value) ||
                (employee.TerminationDate.HasValue && row.AttendanceDate.Value > employee.TerminationDate.Value))
                throw new HrConflictException("Employee employment data changed after the attendance preview. Rebuild the preview before confirming.");
        }
    }

    private static ImportedCalculation CalculateImported(
        WorkDaySchedule schedule,
        DateTimeOffset? checkIn,
        DateTimeOffset? checkOut,
        bool hasApprovedLeave)
    {
        if (hasApprovedLeave)
            return new ImportedCalculation(0, 0, 0, 0, AttendanceValues.LeaveStatus);
        if (!schedule.IsWorkingDay)
        {
            var nonWorkingStatus = string.IsNullOrWhiteSpace(schedule.ExceptionType)
                ? AttendanceValues.WeekendStatus
                : AttendanceValues.HolidayStatus;
            return new ImportedCalculation(0, 0, 0, 0, nonWorkingStatus);
        }
        if (!checkIn.HasValue && !checkOut.HasValue)
            return new ImportedCalculation(0, 0, 0, 0, AttendanceValues.AbsentStatus);

        var working = checkIn.HasValue && checkOut.HasValue
            ? Math.Max(0, (int)Math.Floor((checkOut.Value - checkIn.Value).TotalMinutes) - schedule.BreakMinutes)
            : 0;
        var late = 0;
        var early = 0;
        var overtime = 0;
        if (schedule.StartTime.HasValue && schedule.EndTime.HasValue)
        {
            var plannedStart = ToInstant(schedule.Date, schedule.StartTime.Value, schedule.TimeZoneId);
            var endDate = schedule.EndTime.Value <= schedule.StartTime.Value ? schedule.Date.AddDays(1) : schedule.Date;
            var plannedEnd = ToInstant(endDate, schedule.EndTime.Value, schedule.TimeZoneId);
            if (checkIn > plannedStart.AddMinutes(schedule.LateGraceMinutes))
                late = Math.Max(0, (int)Math.Ceiling((checkIn.Value - plannedStart.AddMinutes(schedule.LateGraceMinutes)).TotalMinutes));
            if (checkOut < plannedEnd.Subtract(TimeSpan.FromMinutes(schedule.EarlyLeaveGraceMinutes)))
                early = Math.Max(0, (int)Math.Ceiling((plannedEnd.Subtract(TimeSpan.FromMinutes(schedule.EarlyLeaveGraceMinutes)) - checkOut!.Value).TotalMinutes));
            if (checkOut > plannedEnd)
            {
                var candidate = Math.Max(0, (int)Math.Floor((checkOut.Value - plannedEnd).TotalMinutes));
                overtime = candidate >= schedule.MinimumOvertimeMinutes ? candidate : 0;
            }
        }
        return new ImportedCalculation(working, late, early, overtime, late > 0 ? AttendanceValues.LateStatus : AttendanceValues.PresentStatus);
    }

    private static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private void AddImportedPunches(Guid recordId, AttendanceImportRow row, DateTimeOffset createdAt)
    {
        var punches = ReadJson<DateTimeOffset[]>(row.PunchesJson) ?? [];
        var sourceRows = ReadJson<int[]>(row.SourceRowNumbersJson) ?? [];
        var ordered = punches.Distinct().Order().ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var punch = ordered[index].ToUniversalTime();
            var type = row.CheckIn == punch
                ? AttendanceValues.CheckInPunch
                : row.CheckOut == punch
                    ? AttendanceValues.CheckOutPunch
                    : AttendanceValues.UnknownPunch;
            int? sourceRow = sourceRows.Length == 0 ? null : sourceRows[Math.Min(index, sourceRows.Length - 1)];
            _dbContext.AttendancePunches.Add(new AttendancePunch(
                recordId,
                punch,
                type,
                AttendanceValues.ExcelImportSource,
                sourceRow,
                null,
                null,
                createdAt));
        }
    }

    private void DetachProcessedAttendance()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries()
                     .Where(entry => (entry.Entity is AttendanceRecord or AttendancePunch) && entry.State == EntityState.Unchanged)
                     .ToArray())
            entry.State = EntityState.Detached;
    }

    private async Task<AttendanceImportBatch> GetTrackedBatchAsync(Guid id, CancellationToken cancellationToken) =>
        await _dbContext.AttendanceImportBatches.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new HrNotFoundException("Attendance import batch was not found.");

    private async Task TryMarkFailedAsync(Guid batchId, string failureMessage)
    {
        try
        {
            _dbContext.ChangeTracker.Clear();
            var failedBatch = await _dbContext.AttendanceImportBatches.SingleOrDefaultAsync(
                item => item.Id == batchId,
                CancellationToken.None);
            if (failedBatch is null || failedBatch.Status is AttendanceValues.ConfirmedBatchStatus or AttendanceValues.CancelledBatchStatus)
                return;

            failedBatch.Fail(TrimFailure(failureMessage), DateTimeOffset.UtcNow);
            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // Preserve the original import-processing exception if failure-state persistence also fails.
        }
    }

    private static AttendanceImportBatchDto MapBatch(AttendanceImportBatch batch) => new(
        batch.Id,
        batch.FileName,
        batch.FileHash,
        batch.Status,
        ReadJson<AttendanceImportColumnMappingRequest>(batch.MappingJson),
        batch.MappingJson is null ? null : Summary(batch),
        batch.FailureReason is null ? null : ApiTextLocalizer.Localize(batch.FailureReason, true),
        batch.UploadedAt,
        batch.PreviewedAt,
        batch.ConfirmedAt);

    private static AttendanceImportSummaryDto Summary(AttendanceImportBatch batch) => new(
        batch.TotalRows,
        batch.ValidRows,
        batch.InvalidRows,
        batch.EmployeeNotFoundRows,
        batch.DuplicateRows,
        batch.MissingCheckInRows,
        batch.MissingCheckOutRows);

    private static AttendanceImportPreviewRowDto MapPreview(PreviewProjection item) => new(
        item.Id,
        item.BatchId,
        ReadJson<int[]>(item.SourceRowNumbersJson) ?? [],
        item.SourceEmployeeNumber,
        item.SourceEmployeeName,
        item.EmployeeId,
        item.EmployeeNumber,
        item.EmployeeName,
        item.AttendanceDate,
        item.CheckIn,
        item.CheckOut,
        ReadJson<DateTimeOffset[]>(item.PunchesJson) ?? [],
        item.CanImport,
        ReadJson<string[]>(item.CategoriesJson) ?? [],
        ApiTextLocalizer.LocalizeErrors(ReadJson<string[]>(item.ErrorsJson)),
        ReadJson<Dictionary<string, string?>[]>(item.SourceRowsJson) ?? []);

    private static T? ReadJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new HrValidationException("Stored attendance import JSON is invalid.", [exception.Message]);
        }
    }

    private static bool HasMissingPunchWarning(string categoriesJson) =>
        ContainsCategory(categoriesJson, HrAttendanceImportCategories.MissingCheckIn) ||
        ContainsCategory(categoriesJson, HrAttendanceImportCategories.MissingCheckOut);

    private static bool ContainsCategory(string json, string category) =>
        (ReadJson<string[]>(json) ?? []).Contains(category, StringComparer.OrdinalIgnoreCase);

    private static string? NormalizeCategory(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "valid" => HrAttendanceImportCategories.Valid,
        "invalid" => HrAttendanceImportCategories.Invalid,
        "employeenotfound" or "employee_not_found" => HrAttendanceImportCategories.EmployeeNotFound,
        "duplicate" => HrAttendanceImportCategories.Duplicate,
        "missingcheckin" or "missing_check_in" => HrAttendanceImportCategories.MissingCheckIn,
        "missingcheckout" or "missing_check_out" => HrAttendanceImportCategories.MissingCheckOut,
        _ => null
    };

    private static string ValidateUpload(AttendanceImportFile file)
    {
        if (file.Content is null || !file.Content.CanRead) throw new HrValidationException("Attendance file content is unavailable.");
        if (string.IsNullOrWhiteSpace(file.FileName)) throw new HrValidationException("Attendance file name is required.");
        if (file.Length <= 0) throw new HrValidationException("Select a non-empty attendance file.");
        if (file.Length > MaximumImportBytes) throw new HrValidationException("Attendance import files cannot exceed 20 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension)) throw new HrValidationException("Only CSV, XLS, and XLSX attendance files are supported.");
        return extension;
    }

    internal static async Task ValidateSignatureAsync(Stream stream, string extension, CancellationToken cancellationToken)
    {
        if (!stream.CanSeek) throw new HrValidationException("Stored attendance files must be seekable.");
        stream.Position = 0;
        var header = new byte[1_024];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        stream.Position = 0;
        var valid = extension switch
        {
            ".xlsx" => read >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04,
            ".xls" => read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            ".csv" => read > 0 && !header.AsSpan(0, read).Contains((byte)0),
            _ => false
        };
        if (!valid) throw new HrValidationException("The uploaded file content does not match its CSV or Excel extension.");
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 200) throw new HrValidationException("Invalid pagination values.");
    }

    private static string TrimFailure(string message) => string.IsNullOrWhiteSpace(message)
        ? "Attendance preview failed."
        : message.Trim()[..Math.Min(message.Trim().Length, 2_000)];

    private static int Pages(int count, int size) => count == 0 ? 0 : (int)Math.Ceiling(count / (double)size);

    private sealed record EmployeeMatch(
        Guid Id,
        string EmployeeNumber,
        string EmployeeName,
        DateOnly? HireDate,
        DateOnly? TerminationDate);
    private sealed record AttendanceKey(Guid EmployeeId, DateOnly Date);
    private sealed record ImportedCalculation(int WorkingMinutes, int LateMinutes, int EarlyLeaveMinutes, int OvertimeMinutes, string Status);
    private sealed record PreviewProjection(
        Guid Id,
        Guid BatchId,
        string SourceRowNumbersJson,
        string? SourceEmployeeNumber,
        string? SourceEmployeeName,
        Guid? EmployeeId,
        string? EmployeeNumber,
        string? EmployeeName,
        DateOnly? AttendanceDate,
        DateTimeOffset? CheckIn,
        DateTimeOffset? CheckOut,
        string PunchesJson,
        bool CanImport,
        string CategoriesJson,
        string ErrorsJson,
        string SourceRowsJson);
    private sealed record HistoryProjection(
        Guid Id,
        string FileName,
        string FileHash,
        string Status,
        int TotalRows,
        int ValidRows,
        int InvalidRows,
        int EmployeeNotFoundRows,
        int DuplicateRows,
        int MissingCheckInRows,
        int MissingCheckOutRows,
        bool HasSummary,
        int ImportedRecords,
        Guid UploadedByUserId,
        string UploadedByUsername,
        DateTimeOffset UploadedAt,
        DateTimeOffset? ConfirmedAt);
}
