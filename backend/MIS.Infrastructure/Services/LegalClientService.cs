using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.DTOs.Legal;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class LegalClientService(
    ApplicationDbContext db,
    ICurrentUserContext user,
    IHrFileStorage files) : ILegalClientService
{
    private const int MaximumImportRows = 5_000;
    private const long MaximumDocumentBytes = 10 * 1024 * 1024;
    private static readonly string[] ClientStatuses = ["ACTIVE", "LEGAL", "ON_HOLD", "CLOSED"];

    private sealed record EvaluatedRow(
        LegalClientImportSourceRow Source,
        string Status,
        ClientOrganization? Organization,
        CollectionCustomer? Customer,
        IReadOnlyCollection<string> Errors);

    public async Task<LegalClientDashboardDto> GetDashboardAsync(CancellationToken token)
    {
        var clients = LegalClients();
        var since = DateTimeOffset.UtcNow.AddDays(-30);
        var total = await clients.CountAsync(token);
        var recentAdded = await clients.CountAsync(customer =>
            customer.CreatedAt >= since
            || db.LegalClientAssignments.Any(x => x.CustomerId == customer.Id && x.CreatedAt >= since)
            || db.LegalCaseFiles.Any(x => x.CollectionCase.CustomerId == customer.Id && x.ReceivedAt >= since)
            || db.CollectionCases.Any(x => x.CustomerId == customer.Id && x.Status == CollectionsValues.CaseStatuses.Legal
                && !x.IsArchived && x.UpdatedAt >= since), token);
        var transferred = await db.CollectionCases.AsNoTracking()
            .Where(x => !x.IsArchived && x.Status == CollectionsValues.CaseStatuses.Legal)
            .Select(x => x.CustomerId).Distinct().CountAsync(token);
        var updatedFiles = await clients.CountAsync(customer =>
            db.LegalClientAssignments.Any(x => x.CustomerId == customer.Id && x.UpdatedAt >= since)
            || db.LegalCaseFiles.Any(x => x.CollectionCase.CustomerId == customer.Id && x.UpdatedAt >= since)
            || db.LegalClientDocuments.Any(x => x.CustomerId == customer.Id && x.UploadedAt >= since), token);
        var recent = await clients
            .Where(customer => customer.CreatedAt >= since
                || db.LegalClientAssignments.Any(x => x.CustomerId == customer.Id && x.CreatedAt >= since)
                || db.LegalCaseFiles.Any(x => x.CollectionCase.CustomerId == customer.Id && x.ReceivedAt >= since)
                || db.CollectionCases.Any(x => x.CustomerId == customer.Id && x.Status == CollectionsValues.CaseStatuses.Legal
                    && !x.IsArchived && x.UpdatedAt >= since))
            .OrderByDescending(customer =>
                db.LegalClientAssignments.Where(x => x.CustomerId == customer.Id).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefault()
                ?? db.LegalCaseFiles.Where(x => x.CollectionCase.CustomerId == customer.Id)
                    .Select(x => (DateTimeOffset?)x.ReceivedAt).FirstOrDefault()
                ?? customer.CreatedAt)
            .Take(8)
            .Select(ClientProjection())
            .ToArrayAsync(token);

        return new LegalClientDashboardDto(total, recentAdded, transferred, updatedFiles, recent, CanManage());
    }

    public async Task<LegalClientPageDto> ListAsync(string? search, int page, int pageSize, CancellationToken token)
    {
        if (page < 1) page = 1;
        if (pageSize is not (20 or 50 or 100)) pageSize = 20;

        var query = LegalClients();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            if (term.Length > 160) throw new HrValidationException("Search cannot exceed 160 characters.");
            query = query.Where(customer =>
                (customer.FullNameArabic != null && customer.FullNameArabic.ToLower().Contains(term))
                || (customer.FullNameEnglish != null && customer.FullNameEnglish.ToLower().Contains(term))
                || customer.CustomerCode.ToLower().Contains(term)
                || (customer.NationalId != null && customer.NationalId.ToLower().Contains(term))
                || (customer.PrimaryPhone != null && customer.PrimaryPhone.ToLower().Contains(term)));
        }

        var count = await query.CountAsync(token);
        var items = await query.OrderBy(customer => ApiTextLocalizer.IsArabic
                ? customer.FullNameArabic ?? customer.FullNameEnglish
                : customer.FullNameEnglish ?? customer.FullNameArabic)
            .ThenBy(customer => customer.CustomerCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ClientProjection())
            .ToArrayAsync(token);
        return new LegalClientPageDto(items, page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize));
    }

    public async Task<LegalClientDetailsDto> GetAsync(Guid customerId, CancellationToken token)
    {
        var customer = await LegalClients().Include(x => x.Organization).Include(x => x.CreatedByUser)
            .SingleOrDefaultAsync(x => x.Id == customerId, token)
            ?? throw new HrNotFoundException("Legal client was not found.");
        return await MapDetailsAsync(customer, token);
    }

    public async Task<LegalClientDetailsDto> UpdateAsync(Guid customerId, UpdateLegalClientRequest request, CancellationToken token)
    {
        RequireManage();
        var customer = await LegalClients().Include(x => x.Organization).Include(x => x.CreatedByUser)
            .SingleOrDefaultAsync(x => x.Id == customerId, token)
            ?? throw new HrNotFoundException("Legal client was not found.");
        if (request.Status is not null && !ClientStatuses.Contains(request.Status.Trim().ToUpperInvariant()))
            throw new HrValidationException("Client status is invalid.");
        if (request.Notes?.Length > 2000) throw new HrValidationException("Notes cannot exceed 2,000 characters.");

        var now = DateTimeOffset.UtcNow;
        var assignment = await db.LegalClientAssignments.SingleOrDefaultAsync(x => x.CustomerId == customerId, token);
        var before = assignment is null ? null : new { assignment.Status, assignment.Notes };
        if (assignment is null)
        {
            assignment = new LegalClientAssignment(customerId, user.UserId, now);
            db.LegalClientAssignments.Add(assignment);
        }
        var status = request.Status ?? (await HasLegalCaseAsync(customerId, token) ? "LEGAL" : assignment.Status);
        assignment.Update(status, request.Notes, user.UserId, now);
        AddAudit("LegalClientUpdated", customerId, before, request);
        await db.SaveChangesAsync(token);
        return await MapDetailsAsync(customer, token);
    }

    public async Task<LegalClientWorkbookDto> InspectImportAsync(string fileName, long length, Stream content, CancellationToken token)
    {
        RequireManage();
        var extension = ValidateImportFile(fileName, length);
        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, token);
        var sheets = await LegalClientWorkbookReader.InspectAsync(buffered, extension, token);
        return new LegalClientWorkbookDto(SafeFileName(fileName), sheets);
    }

    private static string ValidateImportFile(string fileName, long length)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (length is <= 0 or > ExcelImportLimits.MaximumBytes) throw new HrValidationException(ExcelImportLimits.SizeMessage);
        if (extension is not (".xlsx" or ".xls" or ".csv"))
            throw new HrValidationException("Only XLSX, XLS, and CSV client files are supported.");
        return extension;
    }

    public async Task<LegalClientImportPreviewDto> PreviewImportAsync(
        string fileName,
        long length,
        Stream content,
        Guid organizationId,
        CancellationToken token,
        LegalClientColumnMapping? mapping = null)
    {
        RequireManage();
        var extension = ValidateImportFile(fileName, length);

        // Uploaded request streams are not guaranteed to support seeking, which the
        // workbook reader needs for signature checks and reading the ZIP directory.
        using var bufferedContent = new MemoryStream();
        await content.CopyToAsync(bufferedContent, token);
        var parsed = mapping is null
            ? await CollectionImportParser.ParseAsync(bufferedContent, extension, token)
            : await LegalClientWorkbookReader.ParseAsync(bufferedContent, extension, mapping, token);
        if (parsed.Count > MaximumImportRows)
            throw new HrValidationException($"Client imports cannot exceed {MaximumImportRows:N0} rows.");

        var rowOffset = extension == ".csv" ? 0 : 1;
        var sources = parsed.Select(row => mapping is null ? MapImportRow(row, rowOffset) : MapSelectedColumns(row, mapping)).ToArray();
        var evaluated = await EvaluateRowsAsync(sources, organizationId, token);
        var previewRows = evaluated.Select(row => new LegalClientImportPreviewRowDto(
            row.Source, row.Status, LocalizedCustomerName(row.Customer), row.Errors)).ToArray();
        var valid = previewRows.Count(row => row.Status is "VALID" or "EXISTING");
        var invalid = previewRows.Count(row => row.Status == "INVALID");
        return new LegalClientImportPreviewDto(
            SafeFileName(fileName),
            previewRows.Length,
            valid,
            invalid,
            evaluated.Where(row => row.Customer is not null).Select(row => row.Customer!.Id).Distinct().Count(),
            previewRows.Count(row => row.Status == "DUPLICATE"),
            previewRows);
    }

    public async Task<LegalClientImportResultDto> ImportAsync(LegalClientImportRequest request, CancellationToken token)
    {
        RequireManage();
        if (request.Rows is null || request.Rows.Count == 0)
            throw new HrValidationException("The import request must contain previewed client rows.");
        if (request.Rows.Count > MaximumImportRows)
            throw new HrValidationException($"Client imports cannot exceed {MaximumImportRows:N0} rows.");
        if (request.Rows.Select(row => row.RowNumber).Distinct().Count() != request.Rows.Count
            || request.Rows.Any(row => row.RowNumber < 2))
            throw new HrValidationException("Import row numbers must be unique and start at row 2 or later.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var evaluated = await EvaluateRowsAsync(request.Rows, request.OrganizationId, token);
        var results = new List<LegalClientImportRowResultDto>(evaluated.Count);
        var newCustomers = new Dictionary<string, CollectionCustomer>(StringComparer.OrdinalIgnoreCase);
        var assignments = new Dictionary<Guid, LegalClientAssignment>();
        var succeeded = 0;
        foreach (var row in evaluated)
        {
            if (row.Status is not ("VALID" or "EXISTING") || row.Organization is null)
            {
                results.Add(new LegalClientImportRowResultDto(row.Source.RowNumber, row.Status, row.Errors));
                continue;
            }

            var customerKey = ImportCustomerKey(row);
            var customer = row.Customer;
            if (customer is null && customerKey is not null && newCustomers.TryGetValue(customerKey, out var createdCustomer))
                customer = createdCustomer;
            var isNewCustomer = customer is null;
            customer ??= CreateCustomer(row);
            if (isNewCustomer)
            {
                db.CollectionCustomers.Add(customer);
                if (customerKey is not null) newCustomers[customerKey] = customer;
            }
            else
            {
                var (nameArabic, nameEnglish) = ImportNames(row.Source);
                var (addressArabic, addressEnglish) = ImportAddresses(row.Source);
                customer.FillMissingLegalImportDetails(
                    nameArabic,
                    nameEnglish,
                    Normalize(row.Source.NationalId),
                    Normalize(row.Source.MobileNumber),
                    addressArabic,
                    addressEnglish);
            }
            var now = DateTimeOffset.UtcNow;
            if (!assignments.TryGetValue(customer.Id, out var assignment))
            {
                assignment = await db.LegalClientAssignments.SingleOrDefaultAsync(x => x.CustomerId == customer.Id, token);
                if (assignment is not null) assignments[customer.Id] = assignment;
            }
            if (assignment is null)
            {
                assignment = new LegalClientAssignment(customer.Id, user.UserId, now);
                if (!string.IsNullOrWhiteSpace(row.Source.Notes))
                    assignment.Update("ACTIVE", row.Source.Notes, user.UserId, now);
                db.LegalClientAssignments.Add(assignment);
                assignments[customer.Id] = assignment;
            }
            var importedRecordsJson = AppendImportedRecord(assignment.ImportedFieldsJson, row.Source.OriginalFields);
            assignment.SetImportedData(
                row.Source.ExternalIdentifier,
                importedRecordsJson);
            AddAudit("LegalClientImported", customer.Id, null, new { request.FileName, row.Source.RowNumber });
            results.Add(new LegalClientImportRowResultDto(row.Source.RowNumber, "IMPORTED", []));
            succeeded++;
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new LegalClientImportResultDto(results.Count, succeeded, results.Count - succeeded, results);
    }

    public async Task<LegalClientDocumentDto> UploadDocumentAsync(
        Guid customerId,
        string fileName,
        string contentType,
        long length,
        Stream content,
        CancellationToken token)
    {
        RequireManage();
        await EnsureLegalClientAsync(customerId, token);
        if (length is <= 0 or > MaximumDocumentBytes)
            throw new HrValidationException("Documents must be between 1 byte and 10 MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var detectedType = await ValidateDocumentContentAsync(content, extension, token);
        var stored = await files.SaveAsync("legal-client-documents", fileName, detectedType, content, MaximumDocumentBytes, token);
        var document = new LegalClientDocument(customerId, stored.OriginalFileName, stored.ContentType, stored.Length,
            stored.Sha256Hash, stored.StorageKey, user.UserId, DateTimeOffset.UtcNow);
        db.LegalClientDocuments.Add(document);
        AddAudit("LegalClientDocumentUploaded", customerId, null, new { document.OriginalFileName, document.ContentType, document.FileSize });
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch
        {
            await files.DeleteAsync(stored.StorageKey, token);
            throw;
        }
        return await ProjectDocument(db.LegalClientDocuments.AsNoTracking().Where(x => x.Id == document.Id)).SingleAsync(token);
    }

    public async Task<LegalClientDocumentDownloadDto> DownloadDocumentAsync(Guid documentId, CancellationToken token)
    {
        var document = await db.LegalClientDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId, token)
            ?? throw new HrNotFoundException("Legal client document was not found.");
        await EnsureLegalClientAsync(document.CustomerId, token);
        var stream = await files.OpenReadAsync(document.StorageKey, token);
        AddAudit("LegalClientDocumentDownloaded", document.CustomerId, null, new { document.OriginalFileName });
        await db.SaveChangesAsync(token);
        return new LegalClientDocumentDownloadDto(stream, document.ContentType, document.OriginalFileName);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken token)
    {
        RequireManage();
        var document = await db.LegalClientDocuments.SingleOrDefaultAsync(x => x.Id == documentId, token)
            ?? throw new HrNotFoundException("Legal client document was not found.");
        await EnsureLegalClientAsync(document.CustomerId, token);
        await files.DeleteAsync(document.StorageKey, token);
        db.LegalClientDocuments.Remove(document);
        AddAudit("LegalClientDocumentDeleted", document.CustomerId,
            new { document.OriginalFileName, document.ContentType, document.FileSize }, null);
        await db.SaveChangesAsync(token);
    }

    private IQueryable<CollectionCustomer> LegalClients() =>
        db.CollectionCustomers.Where(customer =>
            db.LegalClientAssignments.Any(assignment => assignment.CustomerId == customer.Id)
            || db.LegalCaseFiles.Any(file => file.CollectionCase.CustomerId == customer.Id)
            || db.CollectionCases.Any(collectionCase =>
                collectionCase.CustomerId == customer.Id
                && !collectionCase.IsArchived
                && collectionCase.Status == CollectionsValues.CaseStatuses.Legal));

    private System.Linq.Expressions.Expression<Func<CollectionCustomer, LegalClientListItemDto>> ClientProjection()
    {
        var ar = ApiTextLocalizer.IsArabic;
        return customer => new LegalClientListItemDto(
            customer.Id,
            ar ? customer.FullNameArabic ?? customer.FullNameEnglish ?? customer.CustomerCode
                : customer.FullNameEnglish ?? customer.FullNameArabic ?? customer.CustomerCode,
            customer.NationalId,
            customer.PrimaryPhone,
            ar ? customer.Organization.NameArabic : customer.Organization.NameEnglish,
            ar ? customer.AddressArabic ?? customer.AddressEnglish : customer.AddressEnglish ?? customer.AddressArabic,
            db.CollectionCases.Any(collectionCase => collectionCase.CustomerId == customer.Id
                && !collectionCase.IsArchived && collectionCase.Status == CollectionsValues.CaseStatuses.Legal)
                ? "LEGAL"
                : db.LegalClientAssignments.Where(assignment => assignment.CustomerId == customer.Id)
                    .Select(assignment => assignment.Status).FirstOrDefault() ?? "ACTIVE",
            db.CollectionCases.Any(collectionCase => collectionCase.CustomerId == customer.Id
                && !collectionCase.IsArchived && collectionCase.Status == CollectionsValues.CaseStatuses.Legal),
            db.LegalClientAssignments.Where(assignment => assignment.CustomerId == customer.Id)
                .Select(assignment => assignment.UpdatedAt).FirstOrDefault() != default
                ? db.LegalClientAssignments.Where(assignment => assignment.CustomerId == customer.Id)
                    .Select(assignment => assignment.UpdatedAt).FirstOrDefault()
                : db.CollectionCases.Where(collectionCase => collectionCase.CustomerId == customer.Id)
                    .OrderByDescending(collectionCase => collectionCase.UpdatedAt)
                    .Select(collectionCase => collectionCase.UpdatedAt).FirstOrDefault());
    }

    private async Task<LegalClientDetailsDto> MapDetailsAsync(CollectionCustomer customer, CancellationToken token)
    {
        var item = await db.CollectionCustomers.AsNoTracking().Where(x => x.Id == customer.Id)
            .Select(ClientProjection()).SingleAsync(token);
        var caseNumbers = await db.CollectionCases.AsNoTracking()
            .Where(x => x.CustomerId == customer.Id && !x.IsArchived)
            .OrderBy(x => x.CaseNumber)
            .Select(x => x.CaseNumber)
            .ToArrayAsync(token);
        var assignment = await db.LegalClientAssignments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CustomerId == customer.Id, token);
        var documents = await ProjectDocument(db.LegalClientDocuments.AsNoTracking()
            .Where(x => x.CustomerId == customer.Id).OrderByDescending(x => x.UploadedAt)).ToArrayAsync(token);
        return new LegalClientDetailsDto(
            item,
            customer.CustomerCode,
            customer.AlternatePhone,
            customer.TertiaryPhone,
            customer.Governorate,
            customer.Area,
            customer.City,
            customer.Employer,
            customer.JobTitle,
            customer.SecondaryAddress,
            assignment?.Notes,
            customer.Notes,
            customer.Feedback,
            customer.DataEntrySource,
            customer.CreatedByUser?.FullName,
            customer.CreatedAt,
            caseNumbers,
            documents,
            CanManage(),
            assignment?.ImportedFieldsJson is { Length: > 0 } importedFieldsJson
                ? JsonSerializer.Deserialize<List<Dictionary<string, string>>>(importedFieldsJson)
                : null);
    }

    private async Task<IReadOnlyList<EvaluatedRow>> EvaluateRowsAsync(
        IReadOnlyCollection<LegalClientImportSourceRow> rows,
        Guid organizationId,
        CancellationToken token)
    {
        if (organizationId == Guid.Empty)
            throw new HrValidationException("Select the MIS organization for the imported clients.");
        var organization = await db.CollectionClientOrganizations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == organizationId && x.IsActive, token)
            ?? throw new HrValidationException("The selected MIS organization is not active.");
        var evaluated = new List<EvaluatedRow>(rows.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in rows)
        {
            var errors = new List<string>();
            var sourceName = ImportName(source);
            if (sourceName is null) errors.Add("Client name is required.");
            if (string.IsNullOrWhiteSpace(source.NationalId) && string.IsNullOrWhiteSpace(source.MobileNumber)
                && string.IsNullOrWhiteSpace(source.ExternalIdentifier))
                errors.Add("National ID, mobile number, or installment unique ID is required.");
            if (source.Name?.Length > 200) errors.Add("Client name cannot exceed 200 characters.");
            if (source.NameArabic?.Length > 200 || source.NameEnglish?.Length > 200)
                errors.Add("Client name cannot exceed 200 characters.");
            if (source.NationalId?.Length > 32) errors.Add("National ID cannot exceed 32 characters.");
            if (source.MobileNumber?.Length > 32) errors.Add("Mobile number cannot exceed 32 characters.");
            if (source.Address?.Length > 600) errors.Add("Address cannot exceed 600 characters.");
            if (source.AddressArabic?.Length > 600 || source.AddressEnglish?.Length > 600)
                errors.Add("Address cannot exceed 600 characters.");
            if (source.Notes?.Length > 2000) errors.Add("Notes cannot exceed 2,000 characters.");
            if (source.ExternalIdentifier?.Length > 128) errors.Add("Installment Unique ID cannot exceed 128 characters.");
            if (source.OriginalFields is { Count: > 100 }
                || source.OriginalFields?.Any(field => string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > 100
                    || field.Value is null || field.Value.Length > 4000) == true)
                errors.Add("Original workbook fields contain an invalid header or value.");
            else if (source.OriginalFields?.Sum(field => field.Key.Length + field.Value.Length) > 20_000)
                errors.Add("Original workbook fields exceed the supported size.");

            var duplicateKeys = ImportDuplicateKeys(source, organizationId).ToArray();
            if (duplicateKeys.Any(key => seen.Contains(key)))
                errors.Add("This client appears more than once in the uploaded file.");
            foreach (var duplicateKey in duplicateKeys)
                seen.Add(duplicateKey);

            CollectionCustomer? customer = null;
            if (Normalize(source.ExternalIdentifier) is { } externalIdentifier)
            {
                var matchingAssignments = await db.LegalClientAssignments
                    .Where(assignment => assignment.ExternalIdentifier == externalIdentifier
                        && assignment.Customer.OrganizationId == organization.Id)
                    .Take(2)
                    .Select(assignment => assignment.Customer)
                    .ToArrayAsync(token);
                if (matchingAssignments.Length > 1)
                    errors.Add("Installment Unique ID matches more than one customer.");
                else if (matchingAssignments.Length == 1)
                {
                    if (HasMatchingName(matchingAssignments[0], source))
                        customer = matchingAssignments[0];
                    else
                        errors.Add("Installment Unique ID does not match the customer name.");
                }
            }

            if (Normalize(source.NationalId) is { } nationalId && errors.Count == 0)
            {
                var matchingCustomers = await db.CollectionCustomers
                    .Where(customer => customer.OrganizationId == organization.Id && customer.NationalId == nationalId)
                    .Take(2)
                    .ToArrayAsync(token);
                if (matchingCustomers.Length > 1)
                    errors.Add("National ID matches more than one customer.");
                else if (matchingCustomers.Length == 1)
                {
                    if (HasMatchingName(matchingCustomers[0], source))
                    {
                        if (customer is not null && customer.Id != matchingCustomers[0].Id)
                            errors.Add("National ID and installment ID refer to different customers.");
                        else
                            customer = matchingCustomers[0];
                    }
                    else
                        errors.Add("National ID does not match the customer name.");
                }
            }

            if (customer is null && errors.Count == 0)
                customer = await FindCustomerAsync(organization.Id, source, token);
            if (customer is not null && Normalize(source.NationalId) is { } importedNationalId
                && Normalize(customer.NationalId) is { } existingNationalId
                && !string.Equals(importedNationalId, existingNationalId, StringComparison.Ordinal))
                errors.Add("National ID does not match the identified customer.");
            if (customer is not null && Normalize(source.ExternalIdentifier) is not null
                && Normalize(source.NationalId) is null && !HasMatchingName(customer, source))
                errors.Add("Installment Unique ID does not match the customer name.");
            var status = errors.Count > 0 ? (errors.Any(error => error.Contains("appears more than once", StringComparison.Ordinal)
                || error.Contains("already assigned", StringComparison.Ordinal)) ? "DUPLICATE" : "INVALID")
                : customer is null ? "VALID" : "EXISTING";
            evaluated.Add(new EvaluatedRow(source, status, organization, customer, errors));
        }
        return evaluated;
    }

    private async Task<CollectionCustomer?> FindCustomerAsync(Guid organizationId, LegalClientImportSourceRow source, CancellationToken token)
    {
        var phone = Normalize(source.MobileNumber);
        if (phone is null) return null;
        var (nameArabic, nameEnglish) = ImportNames(source);
        var names = new[] { nameArabic, nameEnglish }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.ToLowerInvariant())
            .Distinct()
            .ToArray();
        if (names.Length == 0) return null;
        return await db.CollectionCustomers.Where(x => x.OrganizationId == organizationId && x.PrimaryPhone == phone)
            .Where(x => (x.FullNameArabic != null && names.Contains(x.FullNameArabic.ToLower()))
                || (x.FullNameEnglish != null && names.Contains(x.FullNameEnglish.ToLower())))
            .FirstOrDefaultAsync(token);
    }

    private CollectionCustomer CreateCustomer(EvaluatedRow row)
    {
        var (nameArabic, nameEnglish) = ImportNames(row.Source);
        var (addressArabic, addressEnglish) = ImportAddresses(row.Source);
        var customer = new CollectionCustomer(
            row.Organization!.Id,
            $"LEGAL-{Guid.NewGuid():N}",
            nameArabic ?? string.Empty,
            nameEnglish ?? string.Empty,
            DateTimeOffset.UtcNow);
        customer.ApplyImportedContact(
            nameArabic,
            nameEnglish,
            Normalize(row.Source.NationalId),
            Normalize(row.Source.MobileNumber));
        if (addressArabic is not null)
            customer.ApplyImportedProfile(null, null, null, null, null, addressArabic, null, null, null, true);
        if (addressEnglish is not null)
            customer.ApplyImportedProfile(null, null, null, null, null, addressEnglish, null, null, null, false);
        customer.ApplyDataEntryDetails(null, null, "LEGAL", user.UserId);
        return customer;
    }

    private async Task EnsureLegalClientAsync(Guid customerId, CancellationToken token)
    {
        if (!await LegalClients().AnyAsync(x => x.Id == customerId, token))
            throw new HrNotFoundException("Legal client was not found.");
    }

    private Task<bool> HasLegalCaseAsync(Guid customerId, CancellationToken token) =>
        db.CollectionCases.AnyAsync(x => x.CustomerId == customerId
            && !x.IsArchived && x.Status == CollectionsValues.CaseStatuses.Legal, token);

    private bool CanManage() =>
        user.Roles.Contains(SystemRoleNames.Admin, StringComparer.OrdinalIgnoreCase)
        || user.Roles.Contains(SystemRoleNames.LegalOfficer, StringComparer.OrdinalIgnoreCase)
        || user.Permissions.Contains(SystemPermissionCodes.LegalCaseManage, StringComparer.OrdinalIgnoreCase)
        || user.Permissions.Contains("*", StringComparer.OrdinalIgnoreCase);

    private void RequireManage()
    {
        if (!CanManage()) throw new HrForbiddenException("You do not have permission to manage legal clients.");
    }

    private void AddAudit(string action, Guid customerId, object? before, object? after) =>
        db.CollectionAuditLogs.Add(new CollectionAuditLog(user.UserId, action, nameof(LegalClientAssignment),
            customerId, null, before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after), "LEGAL", DateTimeOffset.UtcNow));

    private static IQueryable<LegalClientDocumentDto> ProjectDocument(IQueryable<LegalClientDocument> query) =>
        query.Select(document => new LegalClientDocumentDto(
            document.Id, document.OriginalFileName, document.ContentType, document.FileSize,
            document.UploadedByUser.FullName, document.UploadedAt));

    internal static LegalClientImportSourceRow MapSelectedColumns(ParsedCollectionRow row, LegalClientColumnMapping mapping)
    {
        var retainedColumns = mapping.ExtraColumns is null ? null : mapping.ExtraColumns
            .Concat(mapping.Columns.Values.Where(column => !string.IsNullOrWhiteSpace(column)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var retainedFields = retainedColumns is null ? row.OriginalValues : row.OriginalValues
            .Where(field => retainedColumns.Contains(field.Key))
            .ToDictionary(field => field.Key, field => field.Value, StringComparer.OrdinalIgnoreCase);
        string? Value(string field) => mapping.Columns.TryGetValue(field, out var column)
            && !string.IsNullOrWhiteSpace(column) && row.OriginalValues.TryGetValue(column, out var value)
            && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        return new LegalClientImportSourceRow(row.RowNumber, Value("name"), null, null,
            Value("nationalId"), Value("mobileNumber"), null, Value("address"), null, null,
            Value("notes"), Value("externalIdentifier"), retainedFields);
    }

    internal static LegalClientImportSourceRow MapImportRow(ParsedCollectionRow row, int rowOffset)
    {
        var nameArabic = Get(row.Values, "namearabic", "arabicname", "clientnamearabic", "customernamearabic", "اسم العميل بالعربي", "الاسم بالعربي");
        var nameEnglish = Get(row.Values, "nameenglish", "englishname", "clientnameenglish", "customernameenglish", "اسم العميل بالإنجليزي", "الاسم بالإنجليزي");
        var addressArabic = Get(row.Values, "addressarabic", "arabicaddress", "العنوان بالعربي");
        var addressEnglish = Get(row.Values, "addressenglish", "englishaddress", "العنوان بالإنجليزي");
        return new LegalClientImportSourceRow(
            Math.Max(row.RowNumber + rowOffset, 2),
            Get(row.Values, "name", "clientname", "customername", "fullname", "customerfullname", "clientfullname", "custfullname", "الاسم", "اسم العميل", "الاسم الكامل") ?? nameArabic ?? nameEnglish,
            nameArabic,
            nameEnglish,
            Get(row.Values, "nationalid", "nationalidnumber", "nationalnumber", "identitynumber", "idnumber", "nationalidentitynumber", "الرقم القومي", "رقم قومي", "الرقم الوطني", "رقم الهوية"),
            Get(row.Values, "mobilenumber", "mobile", "mobilephone", "phone", "phonenumber", "telephone", "contactnumber", "رقم الموبايل", "الموبايل", "رقم الهاتف", "الهاتف"),
            Get(row.Values, "bankname", "bank", "organization", "organizationname", "entitybank", "entity", "اسم البنك", "البنك", "الجهة"),
            Get(row.Values, "address", "clientaddress", "العنوان") ?? addressArabic ?? addressEnglish,
            addressArabic,
            addressEnglish,
            Get(row.Values, "notes", "note", "ملاحظات"),
            Get(row.Values, "installmentuniqueid", "externalidentifier", "customeruniqueid", "installmentid"),
            IsTargetLegalWorkbook(row.OriginalValues) ? row.OriginalValues : null);
    }

    private static bool IsTargetLegalWorkbook(IReadOnlyDictionary<string, string> originalValues)
    {
        string[] requiredHeaders =
        [
            "#", "Branches", "ID Number", "Installment Unique ID", "Cust Full Name", "serial",
            "تاريخ المحضر", "رقم المحضر", "النيابة", "تاريخ الحكم", "الحكم", "Agency",
        ];
        var actual = originalValues.Keys.Select(CollectionImportParser.NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return originalValues.Count == requiredHeaders.Length
            && actual.Count == requiredHeaders.Length
            && requiredHeaders.All(header => actual.Contains(CollectionImportParser.NormalizeHeader(header)));
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, params string[] aliases)
    {
        var normalizedAliases = aliases.Select(CollectionImportParser.NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return values.FirstOrDefault(pair => normalizedAliases.Contains(CollectionImportParser.NormalizeHeader(pair.Key))).Value is { Length: > 0 } value
            ? value.Trim()
            : null;
    }

    internal static IEnumerable<string> ImportDuplicateKeys(LegalClientImportSourceRow row, Guid organizationId)
    {
        var nationalId = Normalize(row.NationalId);
        var externalIdentifier = Normalize(row.ExternalIdentifier);

        if (nationalId is not null) yield return $"{organizationId}:N:{nationalId}";
        if (externalIdentifier is not null) yield return $"{organizationId}:E:{externalIdentifier}";
        if (nationalId is null && externalIdentifier is null
            && Normalize(row.MobileNumber) is { } phone && ImportName(row) is { } name)
            yield return $"{organizationId}:P:{phone}:{name}";
    }

    private static string? ImportCustomerKey(EvaluatedRow row)
    {
        if (row.Organization is null) return null;
        var organizationId = row.Customer?.OrganizationId ?? row.Organization.Id;
        var identity = Normalize(row.Source.NationalId)
            ?? Normalize(row.Source.ExternalIdentifier)
            ?? (Normalize(row.Source.MobileNumber) is { } phone && ImportName(row.Source) is { } name
                ? $"{phone}:{name.ToLowerInvariant()}"
                : null);
        return identity is null ? null : $"{organizationId}:{identity}";
    }

    private static string? AppendImportedRecord(
        string? existingJson,
        IReadOnlyDictionary<string, string>? importedFields)
    {
        if (importedFields is not { Count: > 0 }) return existingJson;
        List<Dictionary<string, string>> records = string.IsNullOrWhiteSpace(existingJson)
            ? []
            : JsonSerializer.Deserialize<List<Dictionary<string, string>>>(existingJson) ?? [];
        var record = importedFields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var alreadySaved = records.Any(saved => saved.Count == record.Count
            && saved.All(pair => record.TryGetValue(pair.Key, out var value)
                && string.Equals(pair.Value, value, StringComparison.Ordinal)));
        if (!alreadySaved) records.Add(record);
        return JsonSerializer.Serialize(records);
    }

    private static string? ImportName(LegalClientImportSourceRow row) =>
        Normalize(row.Name) ?? Normalize(row.NameArabic) ?? Normalize(row.NameEnglish);

    private static bool HasMatchingName(CollectionCustomer customer, LegalClientImportSourceRow source)
    {
        var (nameArabic, nameEnglish) = ImportNames(source);
        var hasComparableName = false;
        var matchesComparableName = false;
        if (nameArabic is not null && Normalize(customer.FullNameArabic) is { } existingArabic)
        {
            hasComparableName = true;
            matchesComparableName |= string.Equals(nameArabic, existingArabic, StringComparison.OrdinalIgnoreCase);
        }
        if (nameEnglish is not null && Normalize(customer.FullNameEnglish) is { } existingEnglish)
        {
            hasComparableName = true;
            matchesComparableName |= string.Equals(nameEnglish, existingEnglish, StringComparison.OrdinalIgnoreCase);
        }
        return !hasComparableName || matchesComparableName;
    }

    private static (string? Arabic, string? English) ImportNames(LegalClientImportSourceRow row)
    {
        var nameArabic = Normalize(row.NameArabic);
        var nameEnglish = Normalize(row.NameEnglish);
        var name = Normalize(row.Name);
        if (name is not null)
        {
            if (ContainsArabic(name)) nameArabic ??= name;
            else nameEnglish ??= name;
        }
        return (nameArabic, nameEnglish);
    }

    private static (string? Arabic, string? English) ImportAddresses(LegalClientImportSourceRow row)
    {
        var addressArabic = Normalize(row.AddressArabic);
        var addressEnglish = Normalize(row.AddressEnglish);
        var address = Normalize(row.Address);
        if (address is not null)
        {
            if (ContainsArabic(address)) addressArabic ??= address;
            else addressEnglish ??= address;
        }
        return (addressArabic, addressEnglish);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string SafeFileName(string fileName) => Path.GetFileName(fileName).Trim();
    private static bool ContainsArabic(string value) => value.Any(character => character is >= '\u0600' and <= '\u06ff');
    private static string? LocalizedCustomerName(CollectionCustomer? customer) =>
        customer is null ? null : ApiTextLocalizer.IsArabic
            ? customer.FullNameArabic ?? customer.FullNameEnglish ?? customer.CustomerCode
            : customer.FullNameEnglish ?? customer.FullNameArabic ?? customer.CustomerCode;

    private static async Task<string> ValidateDocumentContentAsync(Stream stream, string extension, CancellationToken token)
    {
        if (!stream.CanSeek) throw new HrValidationException("Document stream must be seekable.");
        var header = new byte[8];
        var read = await stream.ReadAsync(header, token);
        stream.Position = 0;
        var pdf = read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        var jpeg = read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
        var png = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
        return extension switch
        {
            ".pdf" when pdf => "application/pdf",
            ".jpg" or ".jpeg" when jpeg => "image/jpeg",
            ".png" when png => "image/png",
            _ => throw new HrValidationException("Only content-validated PDF, JPEG, and PNG documents are supported.")
        };
    }
}
