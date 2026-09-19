using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Collections;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class BankDistributionImportService(
    ApplicationDbContext db,
    IHrFileStorage storage,
    IHrAuditService audit,
    ICurrentUserContext user,
    IWorkingCalendarCalculator calendar,
    ICollectionsClassificationContext classification) : IBankDistributionImportService
{
    private const string Entity = "BankDistributionImport";
    private const long MaximumBytes = 20 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static readonly string[] Fields =
    [
        "CaseNumber", "AccountReference", "ContractNumber", "CustomerCode", "CustomerName",
        "CollectorEmployeeNumber", "CollectorUsername", "CollectorEmail", "CollectorName"
    ];

    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, string StorageKey, string Mode, bool ReassignExisting, int TotalRows);
    private sealed record PreviewPayload(BankDistributionImportPreview Preview, BankDistributionImportMapping Mapping);
    private sealed record MatchedCollector(Guid Id, string Name);
    private sealed record MatchedCase(Guid Id, string CaseNumber, string? AccountReference, string? ContractReference, string CustomerName, Guid? CollectorId, string? CollectorName, bool Assigned);

    private bool Has(string role) => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    private bool Global => Has(SystemRoleNames.Admin) || Has(SystemRoleNames.CollectionsOperationsManager);
    private bool Manager => Global || Has(SystemRoleNames.CollectionsSupervisor);

    public async Task<IReadOnlyCollection<DistributionCollectorDto>> GetCollectorsAsync(Guid organizationId, CancellationToken token)
    {
        EnsurePermission();
        await RequireAccessAsync(organizationId, token);
        var scoped = ScopedCases(organizationId);
        return await AuthorizedCollectors(organizationId).AsNoTracking().OrderBy(x => x.FullName)
            .Select(x => new DistributionCollectorDto(x.Id, x.FullName,
                scoped.Count(c => c.AssignedCollectorId == x.Id),
                scoped.Where(c => c.AssignedCollectorId == x.Id).Sum(c => (decimal?)c.OutstandingBalance) ?? 0))
            .ToArrayAsync(token);
    }

    public async Task<BankDistributionImportUpload> UploadAsync(Guid organizationId, HrUploadFile file, CancellationToken token)
    {
        EnsurePermission();
        await RequireAccessAsync(organizationId, token);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 20 MB.");

        var stored = await storage.SaveAsync($"bank-distribution-imports/{organizationId:N}", file.FileName, file.ContentType, file.Content, MaximumBytes, token);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, token);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, token);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, token);
            var id = Guid.NewGuid();
            await audit.WriteAsync(new AuditWriteRequest(
                "BankDistributionImportUploaded", Entity, id.ToString(), null, null,
                new Uploaded(stored.OriginalFileName, stored.StorageKey, extension),
                $"Distribution import uploaded for organization {organizationId:N}."), token);
            return new BankDistributionImportUpload(id, stored.OriginalFileName,
                sheets.Select(s => new BankDistributionImportSheet(s.SheetName, s.SuggestedHeaderRowNumber, s.DetectedColumns)).ToArray());
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<BankDistributionImportPreview> PreviewAsync(Guid organizationId, Guid id, BankDistributionImportMapping mapping, CancellationToken token)
    {
        EnsurePermission();
        await RequireAccessAsync(organizationId, token);
        var upload = Read<Uploaded>((await OwnedUpload(id, token)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "BankDistributionImportCompleted", token))
            throw new HrConflictException("This distribution import is already complete.");

        var mode = (mapping.Mode ?? "FILE").Trim().ToUpperInvariant();
        if (mode is not ("FILE" or "AUTO")) throw new HrValidationException("Distribution mode must be FILE or AUTO.");
        if (mapping.Columns is null || mapping.Columns.Keys.Except(Fields).Any())
            throw new HrValidationException("Unsupported distribution field mapping.");
        if (!HasAny(mapping, "CaseNumber", "AccountReference", "ContractNumber"))
            throw new HrValidationException("Map at least one case identifier column.");
        if (mode == "FILE" && !HasAny(mapping, "CollectorEmployeeNumber", "CollectorUsername", "CollectorEmail", "CollectorName"))
            throw new HrValidationException("Map at least one collector identifier column.");

        var org = await db.CollectionClientOrganizations.AsNoTracking().SingleAsync(x => x.Id == organizationId, token);
        var orgName = ApiTextLocalizer.IsArabic ? org.NameArabic : org.NameEnglish;

        await using var stream = await storage.OpenReadAsync(upload.StorageKey, token);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, token);
        var indexes = mapping.Columns.Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .ToDictionary(p => p.Key, p => Array.FindIndex(table.Headers, h => h == p.Value));
        if (indexes.Values.Any(i => i < 0)) throw new HrValidationException("A mapped source column was not found.");

        var collectors = await LoadCollectorsAsync(organizationId, token);
        var cases = await LoadCasesAsync(organizationId, token);
        var batchCases = new HashSet<Guid>();
        var batchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<BankDistributionImportRow>();

        foreach (var cells in table.Rows)
        {
            string V(string key) => indexes.TryGetValue(key, out var i) && i >= 0 && i < cells.Length ? cells[i].Trim() : "";
            var errors = new List<string>();
            var caseMatch = MatchCase(cases, V("CaseNumber"), V("AccountReference"), V("ContractNumber"), V("CustomerCode"), errors);
            MatchedCollector? collector = null;
            if (mode == "FILE")
                collector = MatchCollector(collectors, V("CollectorEmployeeNumber"), V("CollectorUsername"), V("CollectorEmail"), V("CollectorName"), errors);

            var status = "Ready";
            if (caseMatch is null && errors.Any(e => e.Contains("Case not found", StringComparison.OrdinalIgnoreCase)))
                status = "CaseNotFound";
            else if (errors.Any(e => e.Contains("Ambiguous", StringComparison.OrdinalIgnoreCase)))
                status = "Invalid";
            else if (mode == "FILE" && collector is null)
                status = "CollectorNotFound";
            else if (caseMatch is not null)
            {
                var key = caseMatch.Id.ToString();
                if (!batchKeys.Add(key) || !batchCases.Add(caseMatch.Id))
                {
                    errors.Add("Duplicate case in file. / الحالة مكررة داخل الملف");
                    status = "DuplicateCaseInFile";
                }
                else if (caseMatch.Assigned)
                    status = "AlreadyAssigned";
            }
            else status = "Invalid";

            if (errors.Count > 0 && status == "Ready") status = "Invalid";

            rows.Add(new BankDistributionImportRow(
                rows.Count + mapping.FirstDataRow,
                caseMatch?.Id,
                caseMatch?.CaseNumber ?? NullIfEmpty(V("CaseNumber")),
                caseMatch?.AccountReference ?? NullIfEmpty(V("AccountReference")),
                caseMatch?.CustomerName ?? NullIfEmpty(V("CustomerName")),
                caseMatch?.CollectorName,
                collector?.Id,
                collector?.Name,
                orgName,
                status,
                errors.Select(e => ApiTextLocalizer.Localize(e) ?? e).ToArray()));
        }

        IReadOnlyCollection<AutoDistributionCollectorDto> autoPlan = Array.Empty<AutoDistributionCollectorDto>();
        if (mode == "AUTO")
        {
            var collectorIds = (mapping.CollectorIds ?? []).Where(x => x != Guid.Empty).Distinct().ToArray();
            if (collectorIds.Length is < 1 or > 100) throw new HrValidationException("Select between 1 and 100 collectors for auto distribution.");
            var selected = await AuthorizedCollectors(organizationId).Where(x => collectorIds.Contains(x.Id)).OrderBy(x => x.FullName).ToArrayAsync(token);
            if (selected.Length != collectorIds.Length) throw new HrForbiddenException("One or more selected collectors are outside your authorized scope.");

            var eligible = rows.Where(r => r.CaseId.HasValue && (r.Status == "Ready" || (r.Status == "AlreadyAssigned" && mapping.ReassignExisting))).Select(r => r.CaseId!.Value).Distinct().ToArray();
            if (mapping.ReassignExisting)
                eligible = rows.Where(r => r.CaseId.HasValue && (r.Status is "Ready" or "AlreadyAssigned")).Select(r => r.CaseId!.Value).Distinct().ToArray();
            else
                eligible = rows.Where(r => r.CaseId.HasValue && r.Status == "Ready").Select(r => r.CaseId!.Value).Distinct().ToArray();

            var counts = selected.ToDictionary(x => x.Id, _ => 0);
            var assignedRows = new List<BankDistributionImportRow>();
            var index = 0;
            foreach (var row in rows)
            {
                if (row.CaseId is null || (row.Status != "Ready" && !(mapping.ReassignExisting && row.Status == "AlreadyAssigned")))
                {
                    assignedRows.Add(row);
                    continue;
                }
                var target = selected[index % selected.Length];
                counts[target.Id]++;
                index++;
                assignedRows.Add(row with
                {
                    NewCollectorId = target.Id,
                    NewCollectorName = target.FullName,
                    Status = row.Status == "AlreadyAssigned" ? "AlreadyAssigned" : "Ready",
                    Errors = row.Errors
                });
            }
            rows = assignedRows;
            autoPlan = selected.Select(x => new AutoDistributionCollectorDto(x.Id, x.FullName, counts[x.Id], 0)).ToArray();
        }

        var preview = new BankDistributionImportPreview(
            id, Guid.NewGuid(), mode, mapping.ReassignExisting,
            rows.Count(r => r.Status == "Ready" || (mapping.ReassignExisting && r.Status == "AlreadyAssigned" && r.NewCollectorId.HasValue)),
            rows.Count(r => r.Status == "AlreadyAssigned"),
            rows.Count(r => r.Status is not ("Ready" or "AlreadyAssigned")),
            rows, autoPlan);

        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new PreviewPayload(preview, mapping), Json));
        var stored = await storage.SaveAsync($"bank-distribution-import-previews/{organizationId:N}", "preview.json", "application/json", content, MaximumBytes, token);
        try
        {
            await audit.WriteAsync(new AuditWriteRequest(
                "BankDistributionImportPreviewed", Entity, id.ToString(), null, null,
                new PreviewSaved(preview.PreviewId, stored.StorageKey, mode, mapping.ReassignExisting, rows.Count),
                $"Distribution import previewed ({mode})."), token);
        }
        catch { await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }

        return preview;
    }

    public async Task<BankDistributionImportResult> ConfirmAsync(Guid organizationId, Guid id, Guid previewId, bool reassignExisting, CancellationToken token)
    {
        EnsurePermission();
        await RequireAccessAsync(organizationId, token);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", token);

        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == id && log.Action == "BankDistributionImportCompleted", token))
            throw new HrConflictException("This distribution import is already complete.");

        var events = await db.HrAuditLogs.AsNoTracking()
            .Where(log => log.EntityType == Entity && log.EntityId == id && log.Action == "BankDistributionImportPreviewed")
            .OrderByDescending(log => log.Timestamp).ToArrayAsync(token);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault();
        if (saved is null || saved.PreviewId != previewId)
            throw new HrValidationException("Build and review the distribution preview before confirming.");

        await using var stream = await storage.OpenReadAsync(saved.StorageKey, token);
        var payload = await JsonSerializer.DeserializeAsync<PreviewPayload>(stream, Json, token)
            ?? throw new HrValidationException("The distribution preview could not be read.");

        var allowReassign = reassignExisting || payload.Mapping.ReassignExisting || saved.ReassignExisting;
        var reason = string.IsNullOrWhiteSpace(payload.Mapping.Reason) ? "Bulk distribution import" : payload.Mapping.Reason.Trim();
        if (reason.Length is < 2 or > 500) throw new HrValidationException("Reason must contain between 2 and 500 characters.");

        var caseIds = payload.Preview.Rows.Where(r => r.CaseId.HasValue && r.NewCollectorId.HasValue &&
                (r.Status == "Ready" || (allowReassign && r.Status == "AlreadyAssigned")))
            .Select(r => r.CaseId!.Value).Distinct().ToArray();
        var cases = await ScopedCases(organizationId).Where(x => caseIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, token);
        var collectorIds = payload.Preview.Rows.Where(r => r.NewCollectorId.HasValue).Select(r => r.NewCollectorId!.Value).Distinct().ToArray();
        var collectors = await AuthorizedCollectors(organizationId).Where(x => collectorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, token);

        var assigned = 0; var reassigned = 0; var skipped = 0; var failed = 0; var now = DateTimeOffset.UtcNow;
        var teamCache = new Dictionary<Guid, Guid?>();

        foreach (var row in payload.Preview.Rows)
        {
            if (row.Status == "AlreadyAssigned" && !allowReassign) { skipped++; continue; }
            if (row.CaseId is null || row.NewCollectorId is null || row.Status is not ("Ready" or "AlreadyAssigned")) { failed++; continue; }
            if (!cases.TryGetValue(row.CaseId.Value, out var item) || !collectors.TryGetValue(row.NewCollectorId.Value, out var collector))
            { failed++; continue; }

            if (item.AssignedCollectorId == collector.Id) { skipped++; continue; }
            if (item.AssignedCollectorId is not null && !allowReassign) { skipped++; continue; }

            if (!teamCache.TryGetValue(collector.Id, out var teamId))
            {
                teamId = Global ? null : await db.CollectionTeamMembers
                    .Where(x => x.UserId == collector.Id && x.IsActive && x.Team.IsActive && x.Team.SupervisorId == user.UserId)
                    .Select(x => (Guid?)x.TeamId).FirstOrDefaultAsync(token);
                teamCache[collector.Id] = teamId;
            }

            var previous = item.AssignedCollectorId;
            item.Assign(collector.Id, teamId, now);
            var source = payload.Preview.Mode == "AUTO" ? "AUTO" : CollectionsValues.AssignmentSources.Import;
            var rule = payload.Preview.Mode == "AUTO" ? "EQUAL_COUNT" : "BULK_FILE";
            db.CollectionAssignmentHistory.Add(new CollectionAssignmentHistory(item.Id, previous, collector.Id, user.UserId, teamId, reason, source, rule, now));
            db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId,
                previous.HasValue ? "PortfolioCaseReassigned" : "PortfolioCaseAssigned",
                nameof(CollectionCase), item.Id, item.Id,
                JsonSerializer.Serialize(new { AssignedCollectorId = previous }),
                JsonSerializer.Serialize(new { AssignedCollectorId = collector.Id, Reason = reason, Source = source }),
                "WEB", now));
            if (previous.HasValue) reassigned++; else assigned++;
        }

        var result = new BankDistributionImportResult(assigned, reassigned, skipped, failed);
        await audit.WriteAsync(new AuditWriteRequest(
            "BankDistributionImportCompleted", Entity, id.ToString(), null, null, result,
            $"Distribution import completed. Assigned {assigned}, reassigned {reassigned}, skipped {skipped}, failed {failed}."), token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return result;
    }

    private async Task<List<(User User, string? EmployeeNumber)>> LoadCollectorsAsync(Guid organizationId, CancellationToken token)
    {
        var users = await AuthorizedCollectors(organizationId).AsNoTracking().Include(x => x.Employee).ToListAsync(token);
        return users.Select(x => (x, x.Employee?.EmployeeNumber)).ToList();
    }

    private async Task<List<MatchedCase>> LoadCasesAsync(Guid organizationId, CancellationToken token)
    {
        var ar = ApiTextLocalizer.IsArabic;
        return await ScopedCases(organizationId).AsNoTracking()
            .Select(x => new MatchedCase(
                x.Id, x.CaseNumber, x.AccountReference, x.ContractReference,
                ar ? x.Customer.FullNameArabic ?? x.Customer.FullNameEnglish! : x.Customer.FullNameEnglish ?? x.Customer.FullNameArabic!,
                x.AssignedCollectorId,
                x.AssignedCollector == null ? null : x.AssignedCollector.FullName,
                x.AssignedCollectorId != null))
            .ToListAsync(token);
    }

    private static MatchedCase? MatchCase(List<MatchedCase> cases, string caseNumber, string account, string contract, string customerCode, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(caseNumber))
        {
            var hits = cases.Where(x => x.CaseNumber.Equals(caseNumber, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (hits.Length == 1) return hits[0];
            if (hits.Length > 1) { errors.Add("Ambiguous case. / أكثر من حالة مطابقة"); return null; }
        }
        if (!string.IsNullOrWhiteSpace(account))
        {
            var hits = cases.Where(x => x.AccountReference != null && x.AccountReference.Equals(account, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (hits.Length == 1) return hits[0];
            if (hits.Length > 1) { errors.Add("Ambiguous case. / أكثر من حالة مطابقة"); return null; }
        }
        if (!string.IsNullOrWhiteSpace(contract))
        {
            var hits = cases.Where(x => x.ContractReference != null && x.ContractReference.Equals(contract, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (hits.Length == 1) return hits[0];
            if (hits.Length > 1) { errors.Add("Ambiguous case. / أكثر من حالة مطابقة"); return null; }
        }
        if (string.IsNullOrWhiteSpace(caseNumber) && string.IsNullOrWhiteSpace(account) && string.IsNullOrWhiteSpace(contract))
            errors.Add("Case identifier is required. / معرف الحالة مطلوب");
        else
            errors.Add("Case not found. / الحالة غير موجودة");
        _ = customerCode;
        return null;
    }

    private static MatchedCollector? MatchCollector(List<(User User, string? EmployeeNumber)> collectors, string employeeNumber, string username, string email, string name, List<string> errors)
    {
        var directory = collectors.Select(x => new CollectorIdentityCandidate(
            x.User.Id, x.User.FullName, x.User.Username, x.User.Email, x.EmployeeNumber,
            x.User.Employee?.FullName, x.User.Employee?.FullNameArabic, x.User.Employee?.FullNameEnglish)).ToArray();
        var match = CollectorIdentity.Match(directory, employeeNumber, username, email, name);
        if (match.Kind == CollectorMatchKind.Unique)
        {
            var user = collectors.First(x => x.User.Id == match.UserId).User;
            return new(user.Id, user.FullName);
        }
        if (match.Kind == CollectorMatchKind.Ambiguous) errors.Add("Ambiguous collector. / أكثر من محصل مطابق");
        else errors.Add("Collector not found. / المحصل غير موجود");
        return null;
    }

    private IQueryable<CollectionCase> ScopedCases(Guid bankId) => ScopedCasesCore(bankId).Apply(classification);
    private IQueryable<CollectionCase> ScopedCasesCore(Guid bankId)
    {
        var q = db.CollectionCases.Where(x => x.Portfolio.OrganizationId == bankId && !x.IsArchived);
        if (Global) return q;
        return q.Where(x => (x.AssignedTeam != null && x.AssignedTeam.SupervisorId == user.UserId) ||
            (x.AssignedTeamId == null && db.CollectionUserAccess.Any(a => a.UserId == user.UserId && a.OrganizationId == bankId && (a.PortfolioId == null || a.PortfolioId == x.PortfolioId))));
    }

    private IQueryable<User> AuthorizedCollectors(Guid bankId) => db.Users.EligibleCollectors().ForSupervisorScope(db, user.UserId, Global);

    private async Task RequireAccessAsync(Guid bankId, CancellationToken token)
    {
        if (!Manager) throw new HrForbiddenException("Case Distribution is available only to authorized Collections managers.");
        var bank = await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x =>
            x.Id == bankId && x.IsActive &&
            (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance), token);
        if (!bank || (!Global && !await db.CollectionUserAccess.AnyAsync(x => x.UserId == user.UserId && x.OrganizationId == bankId, token) && !await ScopedCases(bankId).AnyAsync(token)))
            throw new HrNotFoundException("Organization was not found or is outside your authorized scope.");
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken token) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log =>
            log.EntityType == Entity && log.EntityId == id && log.Action == "BankDistributionImportUploaded" && log.UserId == user.UserId, token)
        ?? throw new HrNotFoundException("Distribution import was not found.");

    private void EnsurePermission()
    {
        if (!Manager) throw new HrForbiddenException("Only authorized Collections managers can bulk distribute cases.");
    }

    private static bool HasAny(BankDistributionImportMapping mapping, params string[] keys) =>
        keys.Any(key => mapping.Columns.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value));

    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
