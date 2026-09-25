using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.DataEntry;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Domain.Services;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class DataEntryService(
    ApplicationDbContext db,
    ICurrentUserContext user,
    IHrFileStorage storage,
    IHrAuditService audit,
    IWorkingCalendarCalculator calendar) : IDataEntryService
{
    private const string Entity = "DataEntryImport";
    private const string BatchEntity = "DataEntryBatch";
    private const int MaximumDocuments = 40;
    private const int SheetPreviewRows = 400;
    private const long MaximumBytes = ExcelImportLimits.MaximumBytes;
    private static readonly HashSet<string> BlockedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msi", ".scr", ".ps1", ".vbs", ".js", ".jar",
        ".dll", ".hta", ".reg", ".lnk", ".cpl", ".msc", ".app", ".dmg", ".sh"
    };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static readonly string[] Fields =
    [
        "CustomerCode", "CustomerName", "NationalId", "MobileNumber", "Address", "Feedback", "Notes",
        "AccountNumber", "ContractNumber", "OutstandingAmount", "OverdueAmount", "DaysPastDue"
    ];

    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CustomerCode"] = ["customercode", "customerid", "customernumber", "كودالعميل", "رقمالعميل", "رقمالعميل"],
        ["CustomerName"] = ["customername", "name", "fullname", "اسمالعميل", "الاسم", "اسمالعميلبالعربية", "اسمالعميلبالانجليزية"],
        ["NationalId"] = ["nationalid", "nid", "الرقمالقومي", "قومي", "الرقم_القومي"],
        ["MobileNumber"] = ["mobilenumber", "mobile", "phone", "phonenumber", "tell", "tel", "telephone", "موبايل", "رقمالموبايل", "الهاتف", "رقمالهاتف", "تليفون", "تلفون"],
        ["Address"] = ["address", "alladdress", "عنوان", "العنوان", "العنوانبالكامل"],
        ["Feedback"] = ["feedback", "تعليق", "ملاحظاتالعميل", "فيدباك"],
        ["Notes"] = ["notes", "data", "ملاحظات", "ملاحظة", "بيانات", "داتا"],
        ["AccountNumber"] = ["accountnumber", "account", "accountreference", "رقمالحساب", "الحساب"],
        ["ContractNumber"] = ["contractnumber", "contract", "contractreference", "رقمالعقد", "العقد"],
        ["OutstandingAmount"] = ["outstandingamount", "outstanding", "remainingamount", "remaining", "المديونية", "الرصيد", "المبلغالمستحق"],
        ["OverdueAmount"] = ["overdueamount", "overdue", "المتأخر", "المبلغالمتأخر"],
        ["DaysPastDue"] = ["dayspastdue", "dpd", "أيامالتأخر", "ايامالتاخر", "أيامالتاخير"]
    };

    private sealed record Uploaded(string FileName, string StorageKey, string Extension);
    private sealed record PreviewSaved(Guid PreviewId, Guid OrganizationId, Guid PortfolioId, string? PrimaryClassification, string? SubClassification, string StorageKey, int TotalRows);
    private sealed record StagedRow(
        int RowNumber,
        string? CustomerCode,
        string CustomerName,
        string? NationalId,
        string? MobileNumber,
        string? Address,
        string? Feedback,
        string? Notes,
        string? AccountNumber,
        string? ContractNumber,
        decimal? OutstandingAmount,
        decimal? OverdueAmount,
        int? DaysPastDue,
        string Status,
        string? ErrorMessage,
        IReadOnlyList<string> Phones,
        IReadOnlyDictionary<string, string> Fields);
    private sealed record PreviewPayload(DataEntryImportMappingRequest Mapping, Guid PortfolioId, IReadOnlyList<StagedRow> Rows);
    private sealed record RowProfile(IReadOnlyList<string> Phones, IReadOnlyDictionary<string, string> Columns);

    private bool IsAdmin => user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase);
    private bool HasStar => user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase);

    public async Task<DataEntryDashboardDto> GetDashboardAsync(CancellationToken token)
    {
        EnsureAccess();
        var batches = ScopeBatches(db.DataEntryBatches.AsNoTracking());
        var draft = await batches.CountAsync(x => x.Status == DataEntryValues.BatchStatuses.Draft, token);
        var submitted = await batches.CountAsync(x => x.Status == DataEntryValues.BatchStatuses.Submitted, token);
        var accepted = await batches.CountAsync(x => x.Status == DataEntryValues.BatchStatuses.Accepted, token);
        var distributed = await batches.CountAsync(x => x.Status == DataEntryValues.BatchStatuses.Distributed, token);
        var rejected = await batches.CountAsync(x => x.Status == DataEntryValues.BatchStatuses.Rejected, token);
        var myClients = await ScopeClientIds().Distinct().CountAsync(token);
        var unread = await db.DataEntryNotifications.AsNoTracking()
            .CountAsync(x => x.RecipientUserId == user.UserId && !x.IsRead, token);
        return new DataEntryDashboardDto(draft, submitted, accepted, distributed, rejected, myClients, unread);
    }

    public async Task<IReadOnlyList<DataEntryOrganizationDto>> ListOrganizationsAsync(CancellationToken token)
    {
        EnsureAccess();
        return await db.CollectionClientOrganizations.AsNoTracking()
            .Where(x => x.IsActive &&
                (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank
                 || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance))
            .OrderBy(x => x.NameEnglish)
            .Select(x => new DataEntryOrganizationDto(x.Id, x.Code, x.NameArabic, x.NameEnglish, x.OrganizationType))
            .ToArrayAsync(token);
    }

    public async Task<IReadOnlyList<DataEntryPortfolioDto>> ListPortfoliosAsync(Guid organizationId, CancellationToken token)
    {
        EnsureAccess();
        await RequireOrganizationAsync(organizationId, token);
        return await db.CollectionPortfolios.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.IsActive)
            .OrderBy(x => x.PrimaryClassification).ThenBy(x => x.SubClassification).ThenBy(x => x.NameEnglish)
            .Select(x => new DataEntryPortfolioDto(x.Id, x.Code, x.NameArabic, x.NameEnglish, x.PrimaryClassification, x.SubClassification))
            .ToArrayAsync(token);
    }

    public async Task<DataEntryClientPageDto> ListClientsAsync(string? search, bool? hasPhone, bool? hasAddress, bool? hasFeedback, bool? hasData, int page, int pageSize, CancellationToken token)
    {
        EnsureAccess();
        (page, pageSize) = NormalizePage(page, pageSize);
        var query = db.CollectionCustomers.AsNoTracking()
            .Where(c => ScopeClientIds().Contains(c.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var rowMatches = db.DataEntryRows.AsNoTracking()
                .Where(r => r.CollectionCustomerId != null && r.FieldsJson != null && r.FieldsJson.Contains(term))
                .Select(r => r.CollectionCustomerId!.Value);
            query = query.Where(c =>
                c.CustomerCode.Contains(term) ||
                (c.NationalId != null && c.NationalId.Contains(term)) ||
                (c.FullNameArabic != null && c.FullNameArabic.Contains(term)) ||
                (c.FullNameEnglish != null && c.FullNameEnglish.Contains(term)) ||
                (c.PrimaryPhone != null && c.PrimaryPhone.Contains(term)) ||
                (c.AlternatePhone != null && c.AlternatePhone.Contains(term)) ||
                (c.AddressArabic != null && c.AddressArabic.Contains(term)) ||
                (c.AddressEnglish != null && c.AddressEnglish.Contains(term)) ||
                (c.Feedback != null && c.Feedback.Contains(term)) ||
                (c.Notes != null && c.Notes.Contains(term)) ||
                rowMatches.Contains(c.Id));
        }

        if (hasPhone == true)
            query = query.Where(c => c.PrimaryPhone != null || c.AlternatePhone != null
                || db.DataEntryRows.Any(r => r.CollectionCustomerId == c.Id && r.FieldsJson != null && r.FieldsJson.Contains("\"phones\":[\"")));
        if (hasAddress == true)
            query = query.Where(c => c.AddressArabic != null || c.AddressEnglish != null);
        if (hasFeedback == true)
            query = query.Where(c => c.Feedback != null && c.Feedback != "");
        if (hasData == true)
            query = query.Where(c => c.Notes != null && c.Notes != "");

        var arabic = ApiTextLocalizer.IsArabic;
        var total = await query.CountAsync(token);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new DataEntryClientListItemDto(
                c.Id,
                c.CustomerCode,
                c.FullNameArabic ?? c.FullNameEnglish ?? c.CustomerCode,
                c.PrimaryPhone,
                arabic ? c.Organization.NameArabic : c.Organization.NameEnglish,
                c.DataEntrySource,
                db.CollectionCases.Where(x => x.CustomerId == c.Id).OrderByDescending(x => x.CreatedAt).Select(x => x.CaseNumber).FirstOrDefault(),
                db.CollectionCases.Where(x => x.CustomerId == c.Id).OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefault(),
                db.CollectionCases.Where(x => x.CustomerId == c.Id).OrderByDescending(x => x.CreatedAt).Select(x => x.Status).FirstOrDefault(),
                db.DataEntryRows.Where(r => r.CollectionCustomerId == c.Id).OrderByDescending(r => r.CreatedAt).Select(r => r.Batch.Status).FirstOrDefault()))
            .ToArrayAsync(token);
        var profiles = await LoadProfilesAsync(items.Select(item => item.Id).ToArray(), token);
        var enriched = items.Select(item =>
        {
            profiles.TryGetValue(item.Id, out var profile);
            var phones = profile?.Phones ?? [];
            var columns = profile?.Columns ?? new Dictionary<string, string>();
            return item with
            {
                Phones = phones,
                NationalId = FirstColumn(columns, "ID", "National ID"),
                Address = FirstColumn(columns, "All Address", "Address"),
                Feedback = FirstColumn(columns, "FEEDBACK", "Feedback"),
                Data = FirstColumn(columns, "Data", "Notes"),
                Fields = columns,
            };
        }).ToArray();
        return new DataEntryClientPageDto(enriched, page, pageSize, total);
    }

    public async Task<DataEntryClientDetailsDto> GetClientAsync(Guid customerId, CancellationToken token)
    {
        EnsureAccess();
        if (!await db.DataEntryRows.AsNoTracking().AnyAsync(r => r.CollectionCustomerId == customerId, token))
            throw new HrNotFoundException("Client was not found.");
        if (!await ScopeClientIds().AnyAsync(id => id == customerId, token))
            throw new HrForbiddenException("You do not have permission to view this client.");

        var customer = await db.CollectionCustomers.AsNoTracking()
            .Include(x => x.Organization)
            .Include(x => x.CreatedByUser)
            .SingleOrDefaultAsync(x => x.Id == customerId, token)
            ?? throw new HrNotFoundException("Client was not found.");

        var row = await db.DataEntryRows.AsNoTracking()
            .Include(x => x.Batch).ThenInclude(b => b.Portfolio)
            .Include(x => x.Batch).ThenInclude(b => b.Organization)
            .Where(x => x.CollectionCustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(token);

        var collectionCase = await db.CollectionCases.AsNoTracking()
            .Include(x => x.Portfolio)
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(token);

        var portfolio = row?.Batch.Portfolio ?? collectionCase?.Portfolio;
        return new DataEntryClientDetailsDto(
            customer.Id,
            customer.CustomerCode,
            customer.FullNameArabic,
            customer.FullNameEnglish,
            customer.NationalId,
            customer.PrimaryPhone,
            customer.AlternatePhone,
            customer.AddressArabic ?? customer.AddressEnglish,
            customer.Feedback,
            customer.Notes,
            customer.DataEntrySource,
            customer.CreatedByUser?.FullName ?? customer.CreatedByUser?.Username,
            customer.CreatedAt,
            customer.OrganizationId,
            ApiTextLocalizer.IsArabic ? customer.Organization.NameArabic : customer.Organization.NameEnglish,
            customer.Organization.OrganizationType,
            portfolio?.Code,
            portfolio is null ? null : (ApiTextLocalizer.IsArabic ? portfolio.NameArabic : portfolio.NameEnglish),
            portfolio?.PrimaryClassification ?? row?.Batch.PrimaryClassification,
            portfolio?.SubClassification ?? row?.Batch.SubClassification,
            collectionCase?.CaseNumber,
            collectionCase?.Id,
            collectionCase?.AccountReference ?? row?.AccountNumber,
            collectionCase?.ContractReference ?? row?.ContractNumber,
            collectionCase?.OutstandingBalance ?? row?.OutstandingBalance,
            collectionCase?.OverdueBalance ?? row?.OverdueBalance,
            collectionCase?.DaysPastDue ?? row?.DaysPastDue,
            collectionCase?.Status,
            row?.BatchId,
            row?.Batch.BatchNumber,
            row?.Batch.Status,
            ReadProfile(row?.FieldsJson)?.Phones,
            ReadProfile(row?.FieldsJson)?.Columns);
    }

    public async Task<DataEntryClientDetailsDto> CreateManualClientAsync(CreateDataEntryClientRequest request, CancellationToken token)
    {
        EnsureManage();
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new HrValidationException("Customer name is required.");

        await RequireOrganizationAsync(request.OrganizationId, token);
        var portfolio = await ResolvePortfolioAsync(
            request.OrganizationId, request.PortfolioId, request.PrimaryClassification, request.SubClassification, token);

        string? nationalId = null;
        if (!string.IsNullOrWhiteSpace(request.NationalId))
        {
            try { nationalId = EgyptianHrDataValidator.NormalizeNationalId(request.NationalId, null, null); }
            catch (HrValidationException ex) { throw new HrValidationException(ex.Message); }
        }

        string? mobile = null;
        if (!string.IsNullOrWhiteSpace(request.MobileNumber))
            mobile = EgyptianHrDataValidator.NormalizePhone(request.MobileNumber, "Mobile number");

        var now = DateTimeOffset.UtcNow;
        var customerCode = !string.IsNullOrWhiteSpace(nationalId)
            ? nationalId!
            : BuildCustomerCode(nationalId, mobile, request.CustomerName.Trim(), request.AccountNumber ?? Guid.NewGuid().ToString("N"));

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var customer = await FindExistingCustomerAsync(request.OrganizationId, nationalId, customerCode, token);
        var createdCustomer = false;
        if (customer is null)
        {
            customer = new CollectionCustomer(request.OrganizationId, customerCode, request.CustomerName.Trim(), request.CustomerName.Trim(), now);
            customer.ApplyImportedContact(request.CustomerName.Trim(), request.CustomerName.Trim(), nationalId, mobile);
            if (!string.IsNullOrWhiteSpace(request.Address))
                customer.UpdatePortfolioContact(customer.PrimaryPhone, customer.AlternatePhone, request.Address, ApiTextLocalizer.IsArabic);
            customer.ApplyDataEntryDetails(request.Feedback, request.Notes, DataEntryValues.Sources.Manual, user.UserId);
            db.CollectionCustomers.Add(customer);
            createdCustomer = true;
        }
        else
        {
            // Duplicate default = SKIP identity overwrite; still attach data-entry metadata when empty.
            customer.ApplyDataEntryDetails(request.Feedback, request.Notes, DataEntryValues.Sources.Manual, user.UserId);
        }

        var batch = new DataEntryBatch(
            await NextBatchNumberAsync(token),
            request.OrganizationId,
            portfolio.Id,
            DataEntryValues.Sources.Manual,
            user.UserId,
            now,
            portfolio.PrimaryClassification ?? PortfolioClassification.Normalize(request.PrimaryClassification),
            portfolio.SubClassification ?? PortfolioClassification.Normalize(request.SubClassification),
            fileName: "manual-entry");
        db.DataEntryBatches.Add(batch);

        var rowStatus = createdCustomer ? DataEntryValues.RowStatuses.Ready : DataEntryValues.RowStatuses.ExistingCustomer;
        var row = new DataEntryRow(
            batch.Id,
            1,
            request.CustomerName.Trim(),
            rowStatus,
            customer.CustomerCode,
            nationalId ?? customer.NationalId,
            mobile ?? customer.PrimaryPhone,
            request.Address,
            request.Feedback,
            request.Notes,
            request.AccountNumber,
            request.ContractNumber,
            request.OutstandingBalance ?? 0m,
            null,
            null);
        row.LinkCustomer(customer.Id);
        db.DataEntryRows.Add(row);
        batch.SetCounts(1, 1, 0, now);
        if (createdCustomer) batch.AddCreatedCounts(1, 0, now);
        await audit.WriteAsync(new AuditWriteRequest(
            "ManualClientCreated", BatchEntity, batch.Id.ToString(), null, null,
            new { CustomerId = customer.Id, BatchId = batch.Id, batch.BatchNumber, SkippedExisting = !createdCustomer },
            $"Manual data-entry client {(createdCustomer ? "created" : "linked")} for batch {batch.BatchNumber}."), token);
        await audit.WriteAsync(new AuditWriteRequest(
            "BatchSaved", BatchEntity, batch.Id.ToString(), null, null,
            new { batch.Id, batch.BatchNumber, batch.Status },
            $"Manual data-entry batch {batch.BatchNumber} saved."), token);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return await GetClientAsync(customer.Id, token);
    }

    public async Task DeleteClientAsync(Guid customerId, CancellationToken token)
    {
        EnsureManage();
        var customer = await db.CollectionCustomers.SingleOrDefaultAsync(x => x.Id == customerId, token)
            ?? throw new HrNotFoundException("Data-entry client was not found.");
        if (await db.CollectionCases.AnyAsync(x => x.CustomerId == customerId, token))
            throw new HrConflictException("This client cannot be deleted.");

        var rows = await db.DataEntryRows.Include(x => x.Batch)
            .Where(x => x.CollectionCustomerId == customerId)
            .ToListAsync(token);
        if (rows.Any(row => row.CollectionCaseId is not null))
            throw new HrConflictException("This client cannot be deleted.");
        if (rows.Any(row => row.Batch.Status is DataEntryValues.BatchStatuses.Accepted or DataEntryValues.BatchStatuses.Distributed))
            throw new HrConflictException("This client cannot be deleted.");

        var batchIds = rows.Select(row => row.BatchId).Distinct().ToArray();
        db.DataEntryRows.RemoveRange(rows);
        await db.SaveChangesAsync(token);

        var emptyBatches = await db.DataEntryBatches
            .Include(batch => batch.Rows)
            .Where(batch => batchIds.Contains(batch.Id) && !batch.Rows.Any())
            .ToListAsync(token);
        db.DataEntryBatches.RemoveRange(emptyBatches);
        db.CollectionCustomers.Remove(customer);
        await audit.WriteAsync(new AuditWriteRequest(
            "ManualClientDeleted",
            nameof(CollectionCustomer),
            customerId.ToString(),
            null,
            new { customer.CustomerCode, customer.FullNameEnglish },
            null,
            $"Deleted unused data-entry client {customer.CustomerCode}."), token);
        await db.SaveChangesAsync(token);
    }

    public async Task<DataEntryImportUploadDto> UploadImportAsync(HrUploadFile file, CancellationToken token)
    {
        EnsureManage();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length <= 0 || file.Length > MaximumBytes || extension is not (".csv" or ".xls" or ".xlsx"))
            throw new HrValidationException("Choose a CSV, XLS, or XLSX file no larger than 50 MB.");

        var stored = await storage.SaveAsync($"data-entry-imports/{user.UserId:N}", file.FileName, file.ContentType, file.Content, MaximumBytes, token);
        try
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, token);
            await HrAttendanceImportService.ValidateSignatureAsync(stream, extension, token);
            var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, extension, token);
            var id = Guid.NewGuid();
            await audit.WriteAsync(new AuditWriteRequest(
                "ImportUploaded", Entity, id.ToString(), null, null,
                new Uploaded(stored.OriginalFileName, stored.StorageKey, extension),
                "Data entry import uploaded."), token);
            return new DataEntryImportUploadDto(id, stored.OriginalFileName,
                sheets.Select(s => new DataEntryImportSheetDto(s.SheetName ?? "Sheet1", s.SuggestedHeaderRowNumber, s.DetectedColumns.ToArray())).ToArray());
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<DataEntrySheetPreviewDto> ReadSheetAsync(Guid uploadId, string? sheetName, CancellationToken token)
    {
        EnsureManage();
        var upload = Read<Uploaded>((await OwnedUpload(uploadId, token)).NewValue);
        await using var stream = await storage.OpenReadAsync(upload.StorageKey, token);
        var sheets = await new AttendanceImportParser(calendar).InspectAsync(stream, upload.Extension, token);
        var selected = sheets.FirstOrDefault(sheet =>
                !string.IsNullOrWhiteSpace(sheetName) && string.Equals(sheet.SheetName, sheetName, StringComparison.OrdinalIgnoreCase))
            ?? sheets.FirstOrDefault()
            ?? throw new HrValidationException("The file has no readable sheet.");
        var displayName = string.IsNullOrWhiteSpace(selected.SheetName) ? "Sheet1" : selected.SheetName;
        try
        {
            var table = await AttendanceImportParser.ReadTableAsync(
                stream,
                upload.Extension,
                selected.SheetName,
                selected.SuggestedHeaderRowNumber,
                selected.SuggestedHeaderRowNumber + 1,
                token);
            var rows = table.Rows
                .Take(SheetPreviewRows)
                .Select(row => (IReadOnlyList<string>)row)
                .ToArray();
            return new DataEntrySheetPreviewDto(
                upload.FileName,
                displayName,
                table.Headers,
                rows,
                table.Rows.Count,
                table.Rows.Count > SheetPreviewRows);
        }
        catch (HrValidationException ex) when (ex.Message.Contains("No employee data", StringComparison.OrdinalIgnoreCase))
        {
            return new DataEntrySheetPreviewDto(upload.FileName, displayName, selected.DetectedColumns.ToArray(), [], 0, false);
        }
    }

    public async Task<DataEntryImportPreviewDto> PreviewImportAsync(Guid uploadId, DataEntryImportMappingRequest mapping, CancellationToken token)
    {
        EnsureManage();
        if (mapping.OrganizationId == Guid.Empty)
            throw new HrValidationException("Organization is required.");
        await RequireOrganizationAsync(mapping.OrganizationId, token);

        var upload = Read<Uploaded>((await OwnedUpload(uploadId, token)).NewValue);
        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == uploadId && log.Action == "ImportConfirmed", token))
            throw new HrConflictException("This data entry import is already complete.");

        if (mapping.Columns is null || mapping.Columns.Keys.Except(Fields, StringComparer.OrdinalIgnoreCase).Any())
            throw new HrValidationException("Unsupported data-entry field mapping.");

        var portfolio = await ResolvePortfolioAsync(
            mapping.OrganizationId, mapping.PortfolioId, mapping.PrimaryClassification, mapping.SubClassification, token);

        await using var stream = await storage.OpenReadAsync(upload.StorageKey, token);
        var table = await AttendanceImportParser.ReadTableAsync(stream, upload.Extension, mapping.SheetName, mapping.HeaderRow, mapping.FirstDataRow, token, sheetNames: mapping.SheetNames);
        var columns = SuggestColumns(table.Headers, mapping.Columns);
        if (!columns.TryGetValue("CustomerName", out var mappedName) || string.IsNullOrWhiteSpace(mappedName))
            throw new HrValidationException("Map the customer name column.");
        mapping = mapping with { Columns = columns };

        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in columns.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            var index = Array.FindIndex(table.Headers, column => column == pair.Value);
            if (index < 0) throw new HrValidationException("A mapped source column was not found.");
            indexes[pair.Key] = index;
        }

        var existingByNational = await db.CollectionCustomers.AsNoTracking()
            .Where(x => x.OrganizationId == mapping.OrganizationId && x.NationalId != null)
            .Select(x => x.NationalId!)
            .ToListAsync(token);
        var nationalSet = existingByNational.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingCodes = (await db.CollectionCustomers.AsNoTracking()
            .Where(x => x.OrganizationId == mapping.OrganizationId)
            .Select(x => x.CustomerCode)
            .ToArrayAsync(token)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenNational = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var staged = new List<StagedRow>();

        foreach (var cells in table.Rows)
        {
            string V(string key) => indexes.TryGetValue(key, out var i) && i < cells.Length ? cells[i].Trim() : "";
            var customerName = V("CustomerName");
            var rawNational = V("NationalId");
            var phones = SplitPhones(V("MobileNumber"));
            string? national = null;
            string? mobile = phones.FirstOrDefault();
            string? status = null;
            string? error = null;

            if (string.IsNullOrWhiteSpace(customerName))
            {
                status = DataEntryValues.RowStatuses.Error;
                error = "Customer name is required.";
            }

            if (!string.IsNullOrWhiteSpace(rawNational))
            {
                try { national = EgyptianHrDataValidator.NormalizeNationalId(rawNational, null, null); }
                catch (HrValidationException)
                {
                    var digits = new string(rawNational.Where(char.IsDigit).ToArray());
                    national = string.IsNullOrWhiteSpace(digits) ? rawNational.Trim() : digits;
                }
            }

            if (!string.IsNullOrWhiteSpace(mobile))
            {
                try { mobile = EgyptianHrDataValidator.NormalizePhone(mobile, "Mobile number"); }
                catch (HrValidationException) { /* keep the number as written in the sheet */ }
            }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0; columnIndex < table.Headers.Length; columnIndex++)
            {
                var header = table.Headers[columnIndex];
                if (string.IsNullOrWhiteSpace(header) || columnIndex >= cells.Length) continue;
                var value = cells[columnIndex].Trim();
                if (value.Length > 0) fields[header] = value;
            }

            var rawMobile = V("MobileNumber");
            var customerCode = string.IsNullOrWhiteSpace(V("CustomerCode"))
                ? BuildCustomerCode(national ?? rawNational, mobile ?? rawMobile, customerName, V("AccountNumber"))
                : V("CustomerCode").Trim();

            if (status is null)
            {
                var dupKey = !string.IsNullOrWhiteSpace(national) ? national : customerCode;
                if (!string.IsNullOrWhiteSpace(national) && !seenNational.Add(national!))
                {
                    status = DataEntryValues.RowStatuses.DuplicateInFile;
                    error = "Duplicate national ID in this file.";
                }
                else if (string.IsNullOrWhiteSpace(national) && !string.IsNullOrWhiteSpace(customerCode) && !seenCodes.Add(customerCode))
                {
                    status = DataEntryValues.RowStatuses.DuplicateInFile;
                    error = "Duplicate customer code in this file.";
                }
                else if ((!string.IsNullOrWhiteSpace(national) && nationalSet.Contains(national!))
                         || existingCodes.Contains(customerCode))
                {
                    status = DataEntryValues.RowStatuses.ExistingCustomer;
                }
                else
                {
                    status = DataEntryValues.RowStatuses.Ready;
                }
                _ = dupKey;
            }

            staged.Add(new StagedRow(
                staged.Count + mapping.FirstDataRow,
                NullIfEmpty(customerCode),
                customerName,
                NullIfEmpty(national),
                NullIfEmpty(mobile),
                NullIfEmpty(Clip(V("Address"), 600)),
                NullIfEmpty(Clip(V("Feedback"), 2000)),
                NullIfEmpty(Clip(V("Notes"), 2000)),
                NullIfEmpty(V("AccountNumber")),
                NullIfEmpty(V("ContractNumber")),
                ParseMoney(V("OutstandingAmount")),
                ParseMoney(V("OverdueAmount")),
                ParseInt(V("DaysPastDue")),
                status!,
                error,
                phones,
                fields));
        }

        var previewId = Guid.NewGuid();
        var ready = staged.Count(x => x.Status == DataEntryValues.RowStatuses.Ready);
        var existing = staged.Count(x => x.Status == DataEntryValues.RowStatuses.ExistingCustomer);
        var invalid = staged.Count - ready - existing;
        var preview = new DataEntryImportPreviewDto(
            uploadId,
            previewId,
            staged.Count,
            ready,
            existing,
            invalid,
            staged.Select(ToPreviewRow).ToArray());

        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new PreviewPayload(mapping, portfolio.Id, staged), Json));
        var stored = await storage.SaveAsync($"data-entry-import-previews/{mapping.OrganizationId:N}", "preview.json", "application/json", content, MaximumBytes, token);
        try
        {
            await audit.WriteAsync(new AuditWriteRequest(
                "ImportPreviewed", Entity, uploadId.ToString(), null, null,
                new PreviewSaved(previewId, mapping.OrganizationId, portfolio.Id, portfolio.PrimaryClassification, portfolio.SubClassification, stored.StorageKey, staged.Count),
                $"Data entry preview built with {staged.Count} row(s)."), token);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        return preview;
    }

    public async Task<DataEntryBatchListItemDto> ConfirmImportAsync(ConfirmDataEntryImportRequest request, CancellationToken token)
    {
        EnsureManage();
        await OwnedUpload(request.UploadId, token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(request.UploadId.ToByteArray(), 0)})", token);

        if (await db.HrAuditLogs.AnyAsync(log => log.EntityType == Entity && log.EntityId == request.UploadId && log.Action == "ImportConfirmed", token))
            throw new HrConflictException("This data entry import is already complete.");

        var events = await db.HrAuditLogs.AsNoTracking()
            .Where(log => log.EntityType == Entity && log.EntityId == request.UploadId && log.Action == "ImportPreviewed")
            .OrderByDescending(log => log.Timestamp).ToArrayAsync(token);
        var saved = events.Select(log => Read<PreviewSaved>(log.NewValue)).FirstOrDefault();
        if (saved is null || saved.PreviewId != request.PreviewId)
            throw new HrValidationException("Build and review the import preview before confirming.");

        await using var stream = await storage.OpenReadAsync(saved.StorageKey, token);
        var payload = await JsonSerializer.DeserializeAsync<PreviewPayload>(stream, Json, token)
            ?? throw new HrValidationException("The import preview could not be read.");

        var upload = Read<Uploaded>((await OwnedUpload(request.UploadId, token)).NewValue);
        var portfolio = await db.CollectionPortfolios.Include(x => x.Organization)
            .SingleAsync(x => x.Id == saved.PortfolioId && x.OrganizationId == saved.OrganizationId, token);

        var now = DateTimeOffset.UtcNow;
        var batch = new DataEntryBatch(
            await NextBatchNumberAsync(token),
            saved.OrganizationId,
            portfolio.Id,
            DataEntryValues.Sources.Imported,
            user.UserId,
            now,
            saved.PrimaryClassification ?? portfolio.PrimaryClassification,
            saved.SubClassification ?? portfolio.SubClassification,
            upload.FileName,
            upload.StorageKey);
        db.DataEntryBatches.Add(batch);

        var customersByCode = await db.CollectionCustomers
            .Where(x => x.OrganizationId == saved.OrganizationId)
            .ToDictionaryAsync(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase, token);
        var customersByNational = customersByCode.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.NationalId))
            .GroupBy(x => x.NationalId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var createdCustomers = 0;
        var valid = 0;
        var invalid = 0;
        var excluded = (request.ExcludedRowNumbers ?? []).ToHashSet();
        foreach (var staged in payload.Rows)
        {
            if (excluded.Contains(staged.RowNumber)) continue;
            var isImportable = DataEntryValues.RowStatuses.Importable.Contains(staged.Status);
            var row = new DataEntryRow(
                batch.Id,
                staged.RowNumber,
                string.IsNullOrWhiteSpace(staged.CustomerName) ? $"Row {staged.RowNumber}" : staged.CustomerName,
                staged.Status,
                staged.CustomerCode,
                staged.NationalId,
                staged.MobileNumber,
                staged.Address,
                staged.Feedback,
                staged.Notes,
                staged.AccountNumber,
                staged.ContractNumber,
                staged.OutstandingAmount,
                staged.OverdueAmount,
                staged.DaysPastDue,
                staged.ErrorMessage);
            row.RememberFields(ProfileJson(staged.Phones, staged.Fields));
            db.DataEntryRows.Add(row);

            if (!isImportable)
            {
                invalid++;
                continue;
            }

            valid++;
            CollectionCustomer? customer = null;
            if (!string.IsNullOrWhiteSpace(staged.NationalId) && customersByNational.TryGetValue(staged.NationalId, out var byNational))
                customer = byNational;
            else if (!string.IsNullOrWhiteSpace(staged.CustomerCode) && customersByCode.TryGetValue(staged.CustomerCode, out var byCode))
                customer = byCode;

            if (customer is null)
            {
                var code = string.IsNullOrWhiteSpace(staged.CustomerCode)
                    ? BuildCustomerCode(staged.NationalId, staged.MobileNumber, staged.CustomerName, staged.AccountNumber ?? staged.RowNumber.ToString())
                    : staged.CustomerCode!;
                customer = new CollectionCustomer(saved.OrganizationId, code, staged.CustomerName, staged.CustomerName, now);
                customer.ApplyImportedContact(staged.CustomerName, staged.CustomerName, staged.NationalId, staged.MobileNumber);
                if (!string.IsNullOrWhiteSpace(staged.Address) || staged.Phones.Count > 1)
                    customer.UpdatePortfolioContact(customer.PrimaryPhone, staged.Phones.ElementAtOrDefault(1), staged.Address, ApiTextLocalizer.IsArabic);
                customer.ApplyDataEntryDetails(staged.Feedback, staged.Notes, DataEntryValues.Sources.Imported, user.UserId);
                db.CollectionCustomers.Add(customer);
                customersByCode[customer.CustomerCode] = customer;
                if (!string.IsNullOrWhiteSpace(customer.NationalId))
                    customersByNational[customer.NationalId!] = customer;
                createdCustomers++;
            }
            else
            {
                // SKIP: do not overwrite existing customer identity data.
                customer.ApplyDataEntryDetails(staged.Feedback, staged.Notes, DataEntryValues.Sources.Imported, user.UserId);
            }

            row.LinkCustomer(customer.Id);
        }

        batch.SetCounts(valid + invalid, valid, invalid, now);
        if (valid <= 0)
            throw new HrValidationException("The file has no clients that can be saved.");
        if (createdCustomers > 0) batch.AddCreatedCounts(createdCustomers, 0, now);
        await audit.WriteAsync(new AuditWriteRequest(
            "ImportConfirmed", Entity, request.UploadId.ToString(), null, null,
            new { batch.Id, batch.BatchNumber, createdCustomers, valid, invalid },
            $"Data entry import confirmed into batch {batch.BatchNumber}."), token);
        await audit.WriteAsync(new AuditWriteRequest(
            "BatchSaved", BatchEntity, batch.Id.ToString(), null, null,
            new { batch.Id, batch.BatchNumber, batch.Status },
            $"Data entry batch {batch.BatchNumber} saved."), token);

        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return await ToBatchListItemAsync(batch.Id, token);
    }

    public Task<DataEntryBatchPageDto> ListMyBatchesAsync(string? status, int page, int pageSize, CancellationToken token)
    {
        EnsureAccess();
        return ListBatchesInternalAsync(status, page, pageSize, mineOnly: true, token);
    }

    public Task<DataEntryBatchPageDto> ListSupervisorBatchesAsync(string? status, int page, int pageSize, CancellationToken token)
    {
        EnsureReview();
        return ListBatchesInternalAsync(status, page, pageSize, mineOnly: false, token);
    }

    public async Task<DataEntryBatchDetailsDto> GetBatchAsync(Guid batchId, CancellationToken token)
    {
        EnsureAccess();
        var batch = await db.DataEntryBatches.AsNoTracking()
            .Include(x => x.Organization)
            .Include(x => x.UploadedByUser)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == batchId, token)
            ?? throw new HrNotFoundException("Batch was not found.");

        if (batch.UploadedByUserId != user.UserId && !CanReview())
            throw new HrForbiddenException("You do not have permission to view this batch.");

        var summary = ToBatchListItem(batch);
        var customerIds = batch.Rows.Where(r => r.CollectionCustomerId.HasValue).Select(r => r.CollectionCustomerId!.Value).ToArray();
        var documents = await ProjectDocuments(db.DataEntryDocuments.AsNoTracking()
            .Where(x => x.BatchId == batchId || customerIds.Contains(x.CustomerId))
            .OrderByDescending(x => x.UploadedAt), token);
        return new DataEntryBatchDetailsDto(
            summary,
            batch.RejectionReason,
            batch.SubmittedAt,
            batch.ReviewedByUser?.FullName ?? batch.ReviewedByUser?.Username,
            batch.ReviewedAt,
            batch.DistributedAt,
            batch.Rows.OrderBy(r => r.RowNumber).Select(r => new DataEntryImportPreviewRowDto(
                r.RowNumber, r.CustomerCode, r.CustomerName, r.NationalId, r.MobileNumber, r.Status, r.ErrorMessage, r.CollectionCustomerId, r.CollectionCaseId)).ToArray(),
            documents);
    }

    public async Task<DataEntryBatchListItemDto> AcceptBatchAsync(Guid batchId, CancellationToken token)
    {
        EnsureReview();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var batch = await db.DataEntryBatches
            .Include(x => x.Organization)
            .Include(x => x.Portfolio)
            .Include(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == batchId, token)
            ?? throw new HrNotFoundException("Batch was not found.");

        var now = DateTimeOffset.UtcNow;
        batch.Accept(user.UserId, now);

        var buckets = await db.CollectionBucketDefinitions
            .Where(x => x.OrganizationId == batch.OrganizationId && x.IsActive && (x.PortfolioId == null || x.PortfolioId == batch.PortfolioId))
            .OrderByDescending(x => x.PortfolioId != null).ThenBy(x => x.SortOrder)
            .ToArrayAsync(token);
        if (buckets.Length == 0)
            throw new HrConflictException("No delinquency bucket is configured for this organization.");

        var existingAccounts = (await db.CollectionCases
            .Where(x => x.PortfolioId == batch.PortfolioId)
            .Select(x => x.AccountReference)
            .ToArrayAsync(token)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var createdCases = 0;
        foreach (var row in batch.Rows.Where(r => r.IsValid && r.CollectionCustomerId.HasValue && r.CollectionCaseId is null))
        {
            var account = NullIfEmpty(row.AccountNumber) ?? NullIfEmpty(row.CustomerCode)
                ?? throw new HrValidationException($"Row {row.RowNumber} is missing an account number and customer code.");
            if (existingAccounts.Contains(account))
                continue;

            var outstanding = row.OutstandingBalance ?? 0m;
            if (outstanding < 0) outstanding = 0m;
            var overdue = row.OverdueBalance ?? outstanding;
            if (overdue < 0) overdue = 0m;
            var dpd = row.DaysPastDue ?? 0;
            if (dpd < 0) dpd = 0;

            var bucket = buckets.FirstOrDefault(x => x.MinimumDays.HasValue && dpd >= x.MinimumDays && (!x.MaximumDays.HasValue || dpd <= x.MaximumDays))
                ?? buckets.OrderBy(x => x.SortOrder).First();

            var collectionCase = new CollectionCase(
                batch.PortfolioId,
                row.CollectionCustomerId!.Value,
                BuildCaseNumber(batch.Organization.Code, batch.Portfolio.Code, account),
                account,
                outstanding,
                outstanding,
                overdue,
                dpd,
                bucket.Id,
                now);
            collectionCase.ApplyImportedReferences(row.ContractNumber, ClassifiedPortfolio.ProductTypeOrDefault(batch.Portfolio, null), now);
            db.CollectionCases.Add(collectionCase);
            db.CollectionCaseBucketHistory.Add(new CaseBucketHistory(
                collectionCase.Id, null, bucket.Id, "Data entry accept", CollectionsValues.AssignmentSources.Import, user.UserId, now));
            var priority = CollectionRules.CalculatePriority(collectionCase.OutstandingBalance, collectionCase.DaysPastDue, false, false, 999);
            collectionCase.SetPriority(priority.Score, string.Join(" + ", priority.Reasons), now);
            row.LinkCase(collectionCase.Id);
            existingAccounts.Add(account);
            createdCases++;
        }

        var linkedCustomerIds = batch.Rows.Where(r => r.CollectionCustomerId.HasValue).Select(r => r.CollectionCustomerId!.Value).Distinct().ToArray();
        var documents = await db.DataEntryDocuments.Where(x => linkedCustomerIds.Contains(x.CustomerId)).ToArrayAsync(token);
        foreach (var document in documents)
        {
            document.AttachBatch(batch.Id);
            var caseId = batch.Rows.FirstOrDefault(r => r.CollectionCustomerId == document.CustomerId)?.CollectionCaseId;
            if (caseId.HasValue) document.AttachCase(caseId.Value);
        }

        if (createdCases > 0) batch.AddCreatedCounts(0, createdCases, now);

        await audit.WriteAsync(new AuditWriteRequest(
            "BatchAccepted", BatchEntity, batch.Id.ToString(), null, null,
            new { batch.Id, batch.BatchNumber, createdCases },
            $"Data entry batch {batch.BatchNumber} accepted."), token);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return await ToBatchListItemAsync(batch.Id, token);
    }

    public async Task<DataEntryBatchListItemDto> RejectBatchAsync(Guid batchId, RejectDataEntryBatchRequest request, CancellationToken token)
    {
        EnsureReview();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new HrValidationException("Rejection reason is required.");

        var batch = await db.DataEntryBatches.SingleOrDefaultAsync(x => x.Id == batchId, token)
            ?? throw new HrNotFoundException("Batch was not found.");
        var now = DateTimeOffset.UtcNow;
        batch.Reject(user.UserId, request.Reason, now);
        await audit.WriteAsync(new AuditWriteRequest(
            "BatchRejected", BatchEntity, batch.Id.ToString(), null, null,
            new { batch.Id, batch.BatchNumber, request.Reason },
            $"Data entry batch {batch.BatchNumber} rejected."), token);
        await db.SaveChangesAsync(token);
        return await ToBatchListItemAsync(batch.Id, token);
    }

    public async Task<DataEntryBatchListItemDto> SendToDistributionAsync(Guid batchId, CancellationToken token)
    {
        EnsureReview();
        var batch = await db.DataEntryBatches.SingleOrDefaultAsync(x => x.Id == batchId, token)
            ?? throw new HrNotFoundException("Batch was not found.");
        var now = DateTimeOffset.UtcNow;
        batch.MarkDistributed(now);
        await audit.WriteAsync(new AuditWriteRequest(
            "SentToDistribution", BatchEntity, batch.Id.ToString(), null, null,
            new { batch.Id, batch.BatchNumber, batch.Status },
            $"Data entry batch {batch.BatchNumber} sent to distribution."), token);
        await db.SaveChangesAsync(token);
        return await ToBatchListItemAsync(batch.Id, token);
    }

    public async Task<IReadOnlyList<DataEntryNotificationDto>> ListNotificationsAsync(CancellationToken token)
    {
        EnsureAccess();
        return await db.DataEntryNotifications.AsNoTracking()
            .Include(x => x.Batch)
            .Where(x => x.RecipientUserId == user.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new DataEntryNotificationDto(
                x.Id, x.BatchId, x.Batch.BatchNumber, x.Kind, x.MessageArabic, x.MessageEnglish, x.IsRead, x.CreatedAt))
            .ToArrayAsync(token);
    }

    public async Task MarkNotificationReadAsync(Guid notificationId, CancellationToken token)
    {
        EnsureAccess();
        var notification = await db.DataEntryNotifications
            .SingleOrDefaultAsync(x => x.Id == notificationId && x.RecipientUserId == user.UserId, token)
            ?? throw new HrNotFoundException("Notification was not found.");
        notification.MarkRead(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(token);
    }

    public Task<IReadOnlyList<DataEntryDocumentDto>> ListClientDocumentsAsync(Guid customerId, CancellationToken token)
    {
        EnsureAccess();
        return ListDocumentsAsync(db.DataEntryDocuments.AsNoTracking().Where(x => x.CustomerId == customerId), customerId, token);
    }

    public async Task<IReadOnlyList<DataEntryDocumentDto>> ListBatchDocumentsAsync(Guid batchId, CancellationToken token)
    {
        EnsureAccess();
        var batch = await db.DataEntryBatches.AsNoTracking().Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == batchId, token)
            ?? throw new HrNotFoundException("Batch was not found.");
        if (batch.UploadedByUserId != user.UserId && !CanReview())
            throw new HrForbiddenException("You do not have permission to view this batch.");
        var customerIds = batch.Rows.Where(r => r.CollectionCustomerId.HasValue).Select(r => r.CollectionCustomerId!.Value).ToArray();
        return await ProjectDocuments(db.DataEntryDocuments.AsNoTracking()
            .Where(x => x.BatchId == batchId || customerIds.Contains(x.CustomerId))
            .OrderByDescending(x => x.UploadedAt), token);
    }

    public async Task<IReadOnlyList<DataEntryDocumentDto>> ListCaseDocumentsAsync(Guid caseId, CancellationToken token)
    {
        EnsureReview();
        if (!await db.CollectionCases.AsNoTracking().AnyAsync(x => x.Id == caseId, token))
            throw new HrNotFoundException("Collection case was not found.");
        return await ProjectDocuments(db.DataEntryDocuments.AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.UploadedAt), token);
    }

    public async Task<DataEntryDocumentDto> UploadClientDocumentAsync(Guid customerId, HrUploadFile file, string? note, CancellationToken token)
    {
        EnsureManage();
        if (!await ScopeClientIds().AnyAsync(id => id == customerId, token))
            throw new HrForbiddenException("You do not have permission to attach files to this client.");
        if (file.Length <= 0 || file.Length > MaximumBytes)
            throw new HrValidationException("Supporting files must be between 1 byte and 50 MB.");
        var label = note?.Trim();
        if (string.IsNullOrWhiteSpace(label))
            throw new HrValidationException("Write what this file is before uploading it.");
        if (label.Length > 500)
            throw new HrValidationException("The file description cannot exceed 500 characters.");

        var count = await db.DataEntryDocuments.CountAsync(x => x.CustomerId == customerId, token);
        if (count >= MaximumDocuments)
            throw new HrValidationException("A client can have at most 40 supporting files.");

        await using var buffer = await BufferUploadAsync(file.Content, token);
        var detectedType = await DetectDocumentTypeAsync(buffer, file.FileName, token);
        buffer.Position = 0;
        var row = await db.DataEntryRows.AsNoTracking()
            .Where(x => x.CollectionCustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.BatchId, x.CollectionCaseId })
            .FirstOrDefaultAsync(token);
        var stored = await storage.SaveAsync("data-entry-documents", file.FileName, detectedType, buffer, MaximumBytes, token);
        var document = new DataEntryDocument(customerId, row?.BatchId, row?.CollectionCaseId, stored.OriginalFileName, detectedType, stored.Length, stored.Sha256Hash, stored.StorageKey, user.UserId, DateTimeOffset.UtcNow, label);
        db.DataEntryDocuments.Add(document);
        try
        {
            await audit.WriteAsync(new AuditWriteRequest(
                "DocumentUploaded", "DataEntryDocument", document.Id.ToString(), null, null,
                new { document.CustomerId, document.OriginalFileName, document.FileSize, document.Note },
                $"Data entry supporting file {document.OriginalFileName} uploaded."), token);
            await db.SaveChangesAsync(token);
        }
        catch { await storage.DeleteAsync(stored.StorageKey, token); throw; }
        return (await ProjectDocuments(db.DataEntryDocuments.AsNoTracking().Where(x => x.Id == document.Id), token)).Single();
    }

    public async Task<DataEntryDocumentDownloadDto> DownloadDocumentAsync(Guid documentId, CancellationToken token)
    {
        EnsureAccess();
        var document = await db.DataEntryDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId, token)
            ?? throw new HrNotFoundException("Document was not found.");
        if (!CanReview() && !await ScopeClientIds().AnyAsync(id => id == document.CustomerId, token))
            throw new HrForbiddenException("You do not have permission to open this file.");
        var content = await storage.OpenReadAsync(document.StorageKey, token);
        return new DataEntryDocumentDownloadDto(content, document.ContentType, document.OriginalFileName);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken token)
    {
        var document = await db.DataEntryDocuments.SingleOrDefaultAsync(x => x.Id == documentId, token)
            ?? throw new HrNotFoundException("Document was not found.");
        var reviewer = CanReview();
        if (!reviewer)
        {
            EnsureManage();
            if (document.UploadedByUserId != user.UserId)
                throw new HrForbiddenException("You can only remove files you uploaded.");
            if (document.CaseId.HasValue)
                throw new HrForbiddenException("Files already attached to a collections case can only be removed by a supervisor.");
        }

        var storageKey = document.StorageKey;
        await audit.WriteAsync(new AuditWriteRequest(
            "DocumentDeleted", "DataEntryDocument", document.Id.ToString(), null,
            new { document.CustomerId, document.OriginalFileName }, null,
            $"Data entry supporting file {document.OriginalFileName} removed."), token);
        db.DataEntryDocuments.Remove(document);
        await db.SaveChangesAsync(token);
        try { await storage.DeleteAsync(storageKey, token); } catch { /* row removal is authoritative */ }
    }

    private async Task<IReadOnlyList<DataEntryDocumentDto>> ListDocumentsAsync(IQueryable<DataEntryDocument> query, Guid customerId, CancellationToken token)
    {
        if (!await ScopeClientIds().AnyAsync(id => id == customerId, token))
            throw new HrForbiddenException("You do not have permission to view this client.");
        return await ProjectDocuments(query.OrderByDescending(x => x.UploadedAt), token);
    }

    private async Task<IReadOnlyList<DataEntryDocumentDto>> ProjectDocuments(IQueryable<DataEntryDocument> query, CancellationToken token)
    {
        var rows = await query.Select(x => new
        {
            x.Id, x.CustomerId, x.BatchId, x.CaseId, x.OriginalFileName, x.ContentType, x.FileSize, x.Note,
            UploadedBy = x.UploadedByUser.FullName, x.UploadedAt
        }).ToArrayAsync(token);
        return rows.Select(x => new DataEntryDocumentDto(
            x.Id, x.CustomerId, x.BatchId, x.CaseId, x.OriginalFileName, x.ContentType, x.FileSize, x.Note,
            x.UploadedBy, x.UploadedAt, true)).ToArray();
    }

    private static Task<string> DetectDocumentTypeAsync(Stream stream, string fileName, CancellationToken token)
    {
        if (!stream.CanSeek) throw new HrValidationException("The uploaded file stream must be seekable.");
        token.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (BlockedDocumentExtensions.Contains(extension))
            throw new HrValidationException("Programs and scripts cannot be stored. Upload the document, scan, or spreadsheet itself.");
        stream.Position = 0;
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".heic" or ".heif" => "image/heic",
            ".svg" => "image/svg+xml",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            ".7z" => "application/x-7z-compressed",
            _ => "application/octet-stream",
        };
        return Task.FromResult(contentType);
    }

    private async Task<DataEntryBatchPageDto> ListBatchesInternalAsync(string? status, int page, int pageSize, bool mineOnly, CancellationToken token)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var query = db.DataEntryBatches.AsNoTracking()
            .Include(x => x.Organization)
            .Include(x => x.UploadedByUser)
            .AsQueryable();
        if (mineOnly) query = query.Where(x => x.UploadedByUserId == user.UserId);
        else query = query.Where(x => x.Status != DataEntryValues.BatchStatuses.Draft);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            query = query.Where(x => x.Status == normalized);
        }

        var total = await query.CountAsync(token);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(token);
        return new DataEntryBatchPageDto(items.Select(ToBatchListItem).ToArray(), page, pageSize, total);
    }

    private IQueryable<DataEntryBatch> ScopeBatches(IQueryable<DataEntryBatch> query)
        => CanReview() ? query : query.Where(x => x.UploadedByUserId == user.UserId);

    private IQueryable<Guid> ScopeClientIds()
    {
        var rows = db.DataEntryRows.AsNoTracking().Where(r => r.CollectionCustomerId != null);
        if (!CanReview())
            rows = rows.Where(r => r.Batch.UploadedByUserId == user.UserId);
        return rows.Select(r => r.CollectionCustomerId!.Value);
    }

    private async Task<CollectionPortfolio> ResolvePortfolioAsync(
        Guid organizationId, Guid? portfolioId, string? primary, string? sub, CancellationToken token)
    {
        var mappedPrimary = PortfolioClassification.Normalize(primary);
        var mappedSub = PortfolioClassification.Normalize(sub);
        if (mappedPrimary is not null && mappedSub is not null)
            return await ClassifiedPortfolio.ResolveAsync(db, organizationId, mappedPrimary, mappedSub, token);

        if (!portfolioId.HasValue)
            throw new HrValidationException("Choose the collections desk (primary and sub classification).");

        return await db.CollectionPortfolios.Include(x => x.Organization)
            .SingleOrDefaultAsync(x => x.Id == portfolioId && x.OrganizationId == organizationId && x.IsActive, token)
            ?? throw new HrValidationException("A valid active portfolio is required.");
    }

    private static async Task<MemoryStream> BufferUploadAsync(Stream content, CancellationToken token)
    {
        var buffer = new MemoryStream();
        if (content.CanSeek) content.Position = 0;
        await content.CopyToAsync(buffer, token);
        buffer.Position = 0;
        return buffer;
    }

    private async Task RequireOrganizationAsync(Guid organizationId, CancellationToken token)
    {
        var ok = await db.CollectionClientOrganizations.AsNoTracking().AnyAsync(x =>
            x.Id == organizationId && x.IsActive &&
            (x.OrganizationType == CollectionsValues.OrganizationTypes.Bank
             || x.OrganizationType == CollectionsValues.OrganizationTypes.ConsumerFinance), token);
        if (!ok) throw new HrNotFoundException("Organization was not found.");
    }

    private async Task<CollectionCustomer?> FindExistingCustomerAsync(Guid organizationId, string? nationalId, string customerCode, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(nationalId))
        {
            var byNational = await db.CollectionCustomers
                .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.NationalId == nationalId, token);
            if (byNational is not null) return byNational;
        }

        return await db.CollectionCustomers
            .FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.CustomerCode == customerCode, token);
    }

    private async Task<string> NextBatchNumberAsync(CancellationToken token)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4", CultureInfo.InvariantCulture);
            var number = $"DE-{date}-{suffix}";
            if (!await db.DataEntryBatches.AnyAsync(x => x.BatchNumber == number, token))
                return number;
        }
        return $"DE-{date}-{Guid.NewGuid():N}"[..19].ToUpperInvariant();
    }

    private async Task<HrAuditLog> OwnedUpload(Guid id, CancellationToken token) =>
        await db.HrAuditLogs.AsNoTracking().SingleOrDefaultAsync(log =>
            log.EntityType == Entity && log.EntityId == id && log.Action == "ImportUploaded" && log.UserId == user.UserId, token)
        ?? throw new HrNotFoundException("Data entry import was not found.");

    private async Task<DataEntryBatchListItemDto> ToBatchListItemAsync(Guid batchId, CancellationToken token)
    {
        var batch = await db.DataEntryBatches.AsNoTracking()
            .Include(x => x.Organization)
            .Include(x => x.UploadedByUser)
            .SingleAsync(x => x.Id == batchId, token);
        return ToBatchListItem(batch);
    }

    private static DataEntryBatchListItemDto ToBatchListItem(DataEntryBatch batch) =>
        new(
            batch.Id,
            batch.BatchNumber,
            batch.FileName ?? batch.Source,
            batch.UploadedByUser?.FullName ?? batch.UploadedByUser?.Username ?? string.Empty,
            batch.CreatedAt,
            batch.TotalRows,
            batch.ValidRows,
            batch.InvalidRows,
            batch.CreatedCustomerCount,
            batch.Organization is null ? string.Empty : (ApiTextLocalizer.IsArabic ? batch.Organization.NameArabic : batch.Organization.NameEnglish),
            batch.PrimaryClassification,
            batch.SubClassification,
            batch.Status,
            batch.Source);

    private static DataEntryImportPreviewRowDto ToPreviewRow(StagedRow row) =>
        new(row.RowNumber, row.CustomerCode, row.CustomerName, row.NationalId, row.MobileNumber, row.Status, row.ErrorMessage);

    private async Task<Dictionary<Guid, RowProfile>> LoadProfilesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        if (ids.Count == 0) return [];
        var rows = await db.DataEntryRows.AsNoTracking()
            .Where(row => row.CollectionCustomerId != null && ids.Contains(row.CollectionCustomerId.Value))
            .OrderByDescending(row => row.CreatedAt)
            .Select(row => new { CustomerId = row.CollectionCustomerId!.Value, row.FieldsJson, row.NationalId, row.MobileNumber, row.Address, row.Feedback, row.Notes })
            .ToListAsync(token);
        var result = new Dictionary<Guid, RowProfile>();
        foreach (var row in rows)
        {
            if (result.ContainsKey(row.CustomerId)) continue;
            var profile = ReadProfile(row.FieldsJson);
            var phones = profile?.Phones?.Where(phone => !string.IsNullOrWhiteSpace(phone)).ToArray() ?? SplitPhones(row.MobileNumber);
            var columns = new Dictionary<string, string>(profile?.Columns ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(row.NationalId) && FirstColumn(columns, "ID", "National ID") is null)
                columns["ID"] = row.NationalId;
            if (!string.IsNullOrWhiteSpace(row.Address) && FirstColumn(columns, "All Address", "Address") is null)
                columns["All Address"] = row.Address;
            if (!string.IsNullOrWhiteSpace(row.Feedback) && FirstColumn(columns, "FEEDBACK", "Feedback") is null)
                columns["FEEDBACK"] = row.Feedback;
            if (!string.IsNullOrWhiteSpace(row.Notes) && FirstColumn(columns, "Data", "Notes") is null)
                columns["Data"] = row.Notes;
            result[row.CustomerId] = new RowProfile(phones, columns);
        }
        return result;
    }

    private static string? ProfileJson(IReadOnlyList<string> phones, IReadOnlyDictionary<string, string> columns)
    {
        if (phones.Count == 0 && columns.Count == 0) return null;
        return JsonSerializer.Serialize(new RowProfile(phones, columns), Json);
    }

    private static RowProfile? ReadProfile(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<RowProfile>(json, Json); }
        catch (JsonException) { return null; }
    }

    private static string? FirstColumn(IReadOnlyDictionary<string, string> columns, params string[] names)
    {
        foreach (var name in names)
        {
            if (columns.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }
        foreach (var pair in columns)
        {
            if (names.Any(name => NormalizeHeader(pair.Key) == NormalizeHeader(name)) && !string.IsNullOrWhiteSpace(pair.Value))
                return pair.Value;
        }
        return null;
    }

    private static string[] SplitPhones(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var found = new List<string>();
        foreach (var part in raw.Split(['/', '|', '،', ',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var digits = new string(part.Where(char.IsDigit).ToArray());
            if (digits.Length >= 8 && !found.Contains(digits)) found.Add(digits);
        }
        return found.ToArray();
    }

    private static string Clip(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? "";
        return value[..max];
    }

    private static Dictionary<string, string?> SuggestColumns(string[] headers, Dictionary<string, string?>? existing)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in Fields)
            result[field] = existing is not null && existing.TryGetValue(field, out var mapped) && !string.IsNullOrWhiteSpace(mapped)
                ? mapped
                : null;

        foreach (var field in Fields)
        {
            if (!string.IsNullOrWhiteSpace(result[field])) continue;
            if (field == "NationalId")
            {
                var idHeader = headers.FirstOrDefault(header => NormalizeHeader(header) == "id");
                if (idHeader is not null)
                {
                    result[field] = idHeader;
                    continue;
                }
            }
            if (!HeaderAliases.TryGetValue(field, out var aliases)) continue;
            var match = headers.FirstOrDefault(header =>
            {
                var normalized = NormalizeHeader(header);
                return aliases.Any(alias => normalized == alias || normalized.Contains(alias, StringComparison.Ordinal));
            });
            if (match is not null) result[field] = match;
        }

        return result;
    }

    private static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '_' or '-') builder.Append(c);
        }
        return builder.ToString().Replace("_", string.Empty).Replace("-", string.Empty);
    }

    private void EnsureAccess()
    {
        if (!(IsAdmin || HasStar
              || user.Permissions.Contains(SystemPermissionCodes.DataEntryAccess, StringComparer.OrdinalIgnoreCase)
              || user.Permissions.Contains(SystemPermissionCodes.DataEntryManage, StringComparer.OrdinalIgnoreCase)
              || user.Roles.Contains(SystemRoleNames.DataEntry, StringComparer.OrdinalIgnoreCase)
              || CanReview()))
            throw new HrForbiddenException("You do not have permission to access data entry.");
    }

    private void EnsureManage()
    {
        if (!(IsAdmin || HasStar
              || user.Permissions.Contains(SystemPermissionCodes.DataEntryManage, StringComparer.OrdinalIgnoreCase)
              || user.Permissions.Contains(SystemPermissionCodes.DataEntryAccess, StringComparer.OrdinalIgnoreCase)
              || user.Roles.Contains(SystemRoleNames.DataEntry, StringComparer.OrdinalIgnoreCase)))
            throw new HrForbiddenException("You do not have permission to manage data entry.");
    }

    private void EnsureReview()
    {
        if (!CanReview())
            throw new HrForbiddenException("You do not have permission to review data entry batches.");
    }

    private bool CanReview()
    {
        if (IsAdmin || HasStar
            || user.Permissions.Contains(SystemPermissionCodes.DataEntryBatchReview, StringComparer.OrdinalIgnoreCase)
            || user.Roles.Contains(SystemRoleNames.CollectionsSupervisor, StringComparer.OrdinalIgnoreCase)
            || user.Roles.Contains(SystemRoleNames.CollectionsOperationsManager, StringComparer.OrdinalIgnoreCase))
            return true;

        return db.CollectionTeams.AsNoTracking().Any(t => t.IsActive && t.SupervisorId == user.UserId);
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;
        return (page, pageSize);
    }

    private static T Read<T>(string? value) =>
        JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new HrValidationException("Import metadata is unavailable.");

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string DigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        const string arabic = "٠١٢٣٤٥٦٧٨٩";
        const string eastern = "۰۱۲۳۴۵۶۷۸۹";
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var index = arabic.IndexOf(c);
            if (index < 0) index = eastern.IndexOf(c);
            builder.Append(index >= 0 ? (char)('0' + index) : c);
        }
        return new string(builder.ToString().Where(char.IsDigit).ToArray());
    }

    private static string Digits(string value)
    {
        const string arabic = "٠١٢٣٤٥٦٧٨٩";
        const string eastern = "۰۱۲۳۴۵۶۷۸۹";
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var index = arabic.IndexOf(c);
            if (index < 0) index = eastern.IndexOf(c);
            builder.Append(index >= 0 ? (char)('0' + index) : c);
        }
        return builder.ToString();
    }

    private static string NormalizePhone(string value)
    {
        var digits = DigitsOnly(value);
        return digits.StartsWith("20") && digits.Length == 12 ? "0" + digits[2..] : digits;
    }

    private static decimal? ParseMoney(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = Digits(value).Replace(",", string.Empty).Replace("ج.م", string.Empty).Trim();
        return decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static int? ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = DigitsOnly(value);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static string BuildCustomerCode(string? nationalId, string? mobile, string name, string account)
    {
        var national = DigitsOnly(nationalId);
        if (!string.IsNullOrWhiteSpace(national)) return national;
        var phone = NormalizePhone(mobile ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(phone)) return phone;
        var seed = $"{name}|{account}";
        return "C-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..12];
    }

    private static string BuildCaseNumber(string organization, string portfolio, string account)
    {
        var safe = new string(account.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (safe.Length > 40) safe = safe[..24] + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)))[..12];
        return $"{organization}-{portfolio}-{safe}"[..Math.Min(80, organization.Length + portfolio.Length + safe.Length + 2)];
    }
}
