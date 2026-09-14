using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class EmployeeDocumentBulkService(
    ApplicationDbContext db,
    IHrFileStorage storage,
    IHrEmployeeDocumentService documents,
    IHrAuditService audit,
    ICurrentUserContext user) : IEmployeeDocumentBulkService
{
    private const string Entity = "EmployeeDocumentBulk";
    private const string Scope = "employee-document-bulk";
    private const long MaximumFileBytes = 10 * 1024 * 1024;
    private const int MaximumFiles = 50;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record BatchSaved(IReadOnlyCollection<EmployeeDocumentBulkMapper.StoredFile> Files);
    private sealed record PreviewSaved(string StorageKey, int TotalFiles);

    public async Task<EmployeeDocumentBulkUpload> UploadAsync(IReadOnlyCollection<HrUploadFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0) throw new HrValidationException("Select at least one document file.");
        if (files.Count > MaximumFiles) throw new HrValidationException($"Upload at most {MaximumFiles} files at once.");

        var stored = new List<EmployeeDocumentBulkMapper.StoredFile>();
        try
        {
            foreach (var file in files)
            {
                await using var validated = await HrEmployeeDocumentService.ValidateAndBufferAsync(file, cancellationToken, allowDocx: false);
                var saved = await storage.SaveAsync(Scope, file.FileName, validated.ContentType, validated.Stream, MaximumFileBytes, cancellationToken);
                stored.Add(new EmployeeDocumentBulkMapper.StoredFile(Guid.NewGuid(), saved.OriginalFileName, saved.StorageKey, validated.ContentType, saved.Length));
            }

            var id = Guid.NewGuid();
            await audit.WriteAsync(new AuditWriteRequest(
                "EmployeeDocumentBulkStarted",
                Entity,
                id.ToString(),
                null,
                null,
                new BatchSaved(stored),
                $"Bulk employee-document upload started with {stored.Count} file(s)."), cancellationToken);

            var preview = await BuildPreviewAsync(id, stored, [], cancellationToken);
            return preview;
        }
        catch
        {
            foreach (var item in stored)
                await storage.DeleteAsync(item.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<EmployeeDocumentBulkUpload> ApplyCorrectionsAsync(
        Guid id,
        IReadOnlyCollection<EmployeeDocumentBulkCorrection> corrections,
        CancellationToken cancellationToken)
    {
        var batch = await OwnedBatchAsync(id, cancellationToken);
        await EnsureNotCompletedAsync(id, cancellationToken);
        return await BuildPreviewAsync(id, batch.Files, corrections, cancellationToken);
    }

    public async Task<EmployeeDocumentBulkResult> ConfirmAsync(
        Guid id,
        IReadOnlyCollection<EmployeeDocumentBulkCorrection> corrections,
        CancellationToken cancellationToken)
    {
        var batch = await OwnedBatchAsync(id, cancellationToken);
        await EnsureNotCompletedAsync(id, cancellationToken);

        var preview = await BuildPreviewAsync(id, batch.Files, corrections, cancellationToken);
        var correctionMap = corrections.ToDictionary(item => item.FileId);
        var uploaded = 0;
        var replaced = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var item in preview.Items)
        {
            correctionMap.TryGetValue(item.FileId, out var correction);
            var duplicateAction = correction?.DuplicateAction ?? item.DuplicateAction;

            if (item.Status == "DocumentAlreadyExists" && !string.Equals(duplicateAction, "Replace", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                await audit.WriteAsync(new AuditWriteRequest(
                    "EmployeeDocumentBulkSkipped",
                    Entity,
                    id.ToString(),
                    item.EmployeeId,
                    null,
                    new { item.FileName, item.EmployeeNumber, item.DocumentCode, Reason = "DocumentAlreadyExists" },
                    $"Bulk document skipped: {item.FileName}."), cancellationToken);
                continue;
            }

            if (item.Status is not ("Ready" or "DocumentAlreadyExists") ||
                item.EmployeeId is null ||
                string.IsNullOrWhiteSpace(item.DocumentCode))
            {
                skipped++;
                continue;
            }

            var file = batch.Files.First(entry => entry.FileId == item.FileId);
            try
            {
                await using var stream = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
                var wasExisting = item.Status == "DocumentAlreadyExists";
                await documents.UploadRequiredAsync(
                    item.EmployeeId.Value,
                    item.DocumentCode,
                    new HrUploadFile(file.FileName, file.ContentType, file.Length, stream),
                    cancellationToken);

                if (wasExisting)
                {
                    replaced++;
                    await audit.WriteAsync(new AuditWriteRequest(
                        "EmployeeDocumentBulkReplaced",
                        Entity,
                        id.ToString(),
                        item.EmployeeId,
                        null,
                        new { item.FileName, item.EmployeeNumber, item.DocumentCode },
                        $"Bulk document replaced: {item.DocumentCode} for {item.EmployeeNumber}."), cancellationToken);
                }
                else
                {
                    uploaded++;
                }
            }
            catch (Exception ex) when (ex is HrException or ArgumentException or InvalidOperationException)
            {
                failed++;
                await audit.WriteAsync(new AuditWriteRequest(
                    "EmployeeDocumentBulkFailed",
                    Entity,
                    id.ToString(),
                    item.EmployeeId,
                    null,
                    new { item.FileName, item.EmployeeNumber, item.DocumentCode, Error = ex.Message },
                    $"Bulk document failed: {item.FileName}."), cancellationToken);
            }
        }

        var result = new EmployeeDocumentBulkResult(uploaded, replaced, skipped, failed);
        await audit.WriteAsync(new AuditWriteRequest(
            "EmployeeDocumentBulkCompleted",
            Entity,
            id.ToString(),
            null,
            null,
            result,
            $"Bulk employee-document upload completed. Uploaded {uploaded}, replaced {replaced}, skipped {skipped}, failed {failed}."), cancellationToken);

        foreach (var file in batch.Files)
            await storage.DeleteAsync(file.StorageKey, CancellationToken.None);

        return result;
    }

    private async Task EnsureNotCompletedAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "EmployeeDocumentBulkCompleted", cancellationToken))
            throw new HrConflictException("This bulk document upload is already complete.");
    }

    private async Task<BatchSaved> OwnedBatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var log = await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.EntityType == Entity &&
            item.EntityId == id &&
            item.Action == "EmployeeDocumentBulkStarted" &&
            item.UserId == user.UserId, cancellationToken)
            ?? throw new HrNotFoundException("Bulk employee-document upload was not found.");
        return JsonSerializer.Deserialize<BatchSaved>(log.NewValue ?? "{}", Json)
            ?? throw new HrValidationException("Bulk upload metadata could not be read.");
    }

    private async Task<EmployeeDocumentBulkUpload> BuildPreviewAsync(
        Guid id,
        IReadOnlyCollection<EmployeeDocumentBulkMapper.StoredFile> files,
        IReadOnlyCollection<EmployeeDocumentBulkCorrection> corrections,
        CancellationToken cancellationToken)
    {
        var employees = await db.Employees.AsNoTracking()
            .Select(employee => new EmployeeDocumentBulkMapper.EmployeeMatch(
                employee.Id,
                employee.EmployeeNumber,
                employee.NationalId,
                employee.MobileNumber,
                employee.FullName,
                employee.FullNameArabic,
                employee.FullNameEnglish,
                employee.Gender))
            .ToArrayAsync(cancellationToken);

        var existingDocuments = (await db.EmployeeDocuments.AsNoTracking()
            .Where(document => !document.IsDeleted && document.RequiredDocumentCode != null)
            .Select(document => new { document.EmployeeId, document.RequiredDocumentCode })
            .ToArrayAsync(cancellationToken))
            .Select(document => (document.EmployeeId, document.RequiredDocumentCode!))
            .ToHashSet();

        var arabic = ApiTextLocalizer.IsArabic;
        var correctionMap = corrections.ToDictionary(item => item.FileId);
        var batchKeys = new HashSet<(Guid EmployeeId, string DocumentCode)>();
        var items = new List<EmployeeDocumentBulkItem>();

        foreach (var file in files)
        {
            correctionMap.TryGetValue(file.FileId, out var correction);
            var parsed = EmployeeDocumentBulkMapper.ParseFileName(file.FileName);
            items.Add(EmployeeDocumentBulkMapper.MapItem(
                file,
                parsed,
                employees,
                existingDocuments,
                batchKeys,
                correction?.EmployeeId,
                correction?.DocumentCode,
                correction?.DuplicateAction ?? "Skip",
                arabic));
        }

        var preview = new EmployeeDocumentBulkUpload(id, items);
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(preview, Json));
        var stored = await storage.SaveAsync(Scope, $"preview-{id:N}.json", "application/json", content, MaximumFileBytes, cancellationToken);
        try
        {
            await audit.WriteAsync(new AuditWriteRequest(
                "EmployeeDocumentBulkPreviewed",
                Entity,
                id.ToString(),
                null,
                null,
                new PreviewSaved(stored.StorageKey, items.Count),
                $"Bulk employee-document preview built for {items.Count} file(s)."), cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        return preview with
        {
            Items = preview.Items.Select(item => item with
            {
                Errors = item.Errors.Select(message => ApiTextLocalizer.Localize(message)).ToArray()
            }).ToArray()
        };
    }
}
