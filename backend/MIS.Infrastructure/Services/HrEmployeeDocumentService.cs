using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Domain.Services;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrEmployeeDocumentService : IHrEmployeeDocumentService
{
    private const long MaximumFileBytes = 10 * 1024 * 1024;
    private static readonly (string Code, string Name, string ArabicName)[] RequiredDocuments =
    [
        (RequiredEmployeeDocumentCodes.BirthCertificate, "Birth Certificate", "شهادة الميلاد"),
        (RequiredEmployeeDocumentCodes.GraduationCertificate, "Graduation Certificate", "شهادة التخرج"),
        (RequiredEmployeeDocumentCodes.NationalIdCopy, "National ID Copy", "صورة البطاقة"),
        (RequiredEmployeeDocumentCodes.MilitaryStatus, "Military Exemption / Status", "ورق الإعفاء / موقف التجنيد"),
        (RequiredEmployeeDocumentCodes.CriminalRecord, "Criminal Record", "الفيش الجنائي"),
        (RequiredEmployeeDocumentCodes.EmploymentAppointmentPaper, "Employment / Appointment Paper", "ورقة التعيين"),
        (RequiredEmployeeDocumentCodes.LaborOfficeRegistration, "Labor Office Registration (Kaab El Amal)", "كعب العمل")
    ];
    private readonly ApplicationDbContext _dbContext;
    private readonly IHrFileStorage _fileStorage;
    private readonly ICurrentUserContext _currentUser;
    private readonly IHrAuditService _audit;

    public HrEmployeeDocumentService(
        ApplicationDbContext dbContext,
        IHrFileStorage fileStorage,
        ICurrentUserContext currentUser,
        IHrAuditService audit)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<PagedEmployeeDocumentsDto> GetPagedAsync(
        EmployeeDocumentFilterDto filter,
        CancellationToken cancellationToken)
    {
        ValidateFilter(filter);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(filter.ExpiringWithinDays);
        var query = BaseQuery().AsNoTracking().Where(item => !item.IsDeleted);

        if (filter.EmployeeId.HasValue) query = query.Where(item => item.EmployeeId == filter.EmployeeId.Value);
        if (filter.DepartmentId.HasValue) query = query.Where(item => item.Employee.DepartmentId == filter.DepartmentId.Value);
        if (filter.DocumentTypeId.HasValue) query = query.Where(item => item.DocumentTypeId == filter.DocumentTypeId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(item =>
                item.Employee.EmployeeNumber.ToLower().Contains(term) ||
                item.Employee.FullName.ToLower().Contains(term) ||
                (item.Employee.FullNameArabic != null && item.Employee.FullNameArabic.ToLower().Contains(term)) ||
                (item.Employee.FullNameEnglish != null && item.Employee.FullNameEnglish.ToLower().Contains(term)) ||
                item.FileName.ToLower().Contains(term) ||
                item.DocumentType.ToLower().Contains(term) ||
                (item.DocumentTypeDefinition != null && item.DocumentTypeDefinition.NameArabic != null && item.DocumentTypeDefinition.NameArabic.ToLower().Contains(term)));
        }

        query = NormalizeExpiryFilter(filter.ExpiryStatus) switch
        {
            EmployeeDocumentExpiryFilters.Expired => query.Where(item => item.ExpiryDate < today),
            EmployeeDocumentExpiryFilters.ExpiringSoon => query.Where(item => item.ExpiryDate >= today && item.ExpiryDate <= soon),
            EmployeeDocumentExpiryFilters.Valid => query.Where(item => item.ExpiryDate > soon),
            EmployeeDocumentExpiryFilters.NoExpiry => query.Where(item => item.ExpiryDate == null),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        query = ApplySort(query, filter.SortBy, filter.SortDirection);
        var entities = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArrayAsync(cancellationToken);
        var items = entities.Select(item => MapList(item, today, filter.ExpiringWithinDays)).ToArray();
        return new PagedEmployeeDocumentsDto(items, total, filter.Page, filter.PageSize, Pages(total, filter.PageSize));
    }

    public async Task<EmployeeDocumentDetailsDto> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await BaseQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new HrNotFoundException("Employee document was not found.");
        return MapDetails(entity, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<EmployeeDocumentDetailsDto> CreateAsync(
        CreateEmployeeDocumentRequest request,
        HrUploadFile file,
        CancellationToken cancellationToken)
    {
        ValidateDateRange(request.IssueDate, request.ExpiryDate);
        var employee = await _dbContext.Employees.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken)
            ?? throw new HrNotFoundException("Employee was not found.");
        var type = await GetActiveTypeAsync(request.DocumentTypeId, cancellationToken);
        if (type.RequiresExpiryDate && !request.ExpiryDate.HasValue)
            throw new HrValidationException("An expiry date is required for this document type.");

        await using var validated = await ValidateAndBufferAsync(file, cancellationToken);
        var stored = await _fileStorage.SaveAsync(
            "employee-documents",
            file.FileName,
            validated.ContentType,
            validated.Stream,
            MaximumFileBytes,
            cancellationToken);

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var entity = new EmployeeDocument(
                employee.Id,
                type.Id,
                type.Name,
                stored.OriginalFileName,
                stored.StorageKey,
                validated.ContentType,
                stored.Length,
                stored.Sha256Hash,
                request.IssueDate,
                request.ExpiryDate,
                request.Notes,
                _currentUser.UserId,
                DateTimeOffset.UtcNow);
            _dbContext.EmployeeDocuments.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(new AuditWriteRequest(
                "DocumentUploaded",
                nameof(EmployeeDocument),
                entity.Id.ToString(),
                employee.Id,
                null,
                new { type.Name, entity.FileName, entity.IssueDate, entity.ExpiryDate },
                $"Uploaded {type.Name} for employee {employee.EmployeeNumber}."), cancellationToken);
            var created = await GetDetailsAsync(entity.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        }
        catch
        {
            await _fileStorage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<EmployeeDocumentDetailsDto> UpdateAsync(
        Guid id,
        UpdateEmployeeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDateRange(request.IssueDate, request.ExpiryDate);
        var entity = await GetTrackedAsync(id, cancellationToken);
        var type = await GetActiveTypeAsync(request.DocumentTypeId, cancellationToken);
        if (type.RequiresExpiryDate && !request.ExpiryDate.HasValue)
            throw new HrValidationException("An expiry date is required for this document type.");
        var oldValue = new { entity.DocumentTypeId, entity.DocumentType, entity.IssueDate, entity.ExpiryDate, entity.Notes };
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        entity.UpdateMetadata(type.Id, type.Name, request.IssueDate, request.ExpiryDate, request.Notes, _currentUser.UserId, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            "DocumentUpdated",
            nameof(EmployeeDocument),
            entity.Id.ToString(),
            entity.EmployeeId,
            oldValue,
            new { entity.DocumentTypeId, entity.DocumentType, entity.IssueDate, entity.ExpiryDate, entity.Notes },
            "Updated employee document details."), cancellationToken);
        var updated = await GetDetailsAsync(id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<EmployeeDocumentDetailsDto> ReplaceAsync(Guid id, HrUploadFile file, CancellationToken cancellationToken)
    {
        var entity = await GetTrackedAsync(id, cancellationToken);
        await using var validated = await ValidateAndBufferAsync(file, cancellationToken);
        var stored = await _fileStorage.SaveAsync(
            "employee-documents",
            file.FileName,
            validated.ContentType,
            validated.Stream,
            MaximumFileBytes,
            cancellationToken);
        var oldStorageKey = entity.StorageKey;
        var oldValue = new { entity.FileName, entity.MimeType, entity.FileSize, entity.Sha256Hash };
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            entity.ReplaceFile(
                stored.OriginalFileName,
                stored.StorageKey,
                validated.ContentType,
                stored.Length,
                stored.Sha256Hash,
                _currentUser.UserId,
                DateTimeOffset.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(new AuditWriteRequest(
                "DocumentReplaced",
                nameof(EmployeeDocument),
                entity.Id.ToString(),
                entity.EmployeeId,
                oldValue,
                new { entity.FileName, entity.MimeType, entity.FileSize, entity.Sha256Hash },
                "Replaced employee document file."), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        await _fileStorage.DeleteAsync(oldStorageKey, CancellationToken.None);
        return await GetDetailsAsync(id, cancellationToken);
    }

    public async Task<EmployeeDocumentFile> OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.EmployeeDocuments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new HrNotFoundException("Employee document was not found.");
        return new EmployeeDocumentFile(
            await _fileStorage.OpenReadAsync(entity.StorageKey, cancellationToken),
            entity.FileName,
            entity.MimeType);
    }

    public async Task DeleteAsync(Guid id, DeleteEmployeeDocumentRequest request, CancellationToken cancellationToken)
    {
        var entity = await GetTrackedAsync(id, cancellationToken);
        if (await _dbContext.LeaveRequests.AsNoTracking().AnyAsync(
                item => item.AttachmentDocumentId == id,
                cancellationToken))
            throw new HrConflictException("This document is attached to leave history and cannot be deleted. Replace the file if a correction is required.");
        var oldValue = new { entity.DocumentType, entity.FileName, entity.IssueDate, entity.ExpiryDate };
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        entity.Delete(_currentUser.UserId, request.Reason, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            entity.RequiredDocumentCode is null ? "DocumentDeleted" : "EmployeeDocumentDeleted",
            nameof(EmployeeDocument),
            entity.Id.ToString(),
            entity.EmployeeId,
            oldValue,
            null,
            request.Reason ?? $"Employee document deleted: {entity.RequiredDocumentCode ?? entity.DocumentType}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _fileStorage.DeleteAsync(entity.StorageKey, CancellationToken.None);
    }

    public async Task<DocumentExpirySummaryDto> GetExpirySummaryAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _dbContext.EmployeeDocuments.AsNoTracking().Where(item => !item.IsDeleted && item.ExpiryDate != null);
        return new DocumentExpirySummaryDto(
            await query.CountAsync(item => item.ExpiryDate < today, cancellationToken),
            await query.CountAsync(item => item.ExpiryDate >= today && item.ExpiryDate <= today.AddDays(7), cancellationToken),
            await query.CountAsync(item => item.ExpiryDate >= today && item.ExpiryDate <= today.AddDays(15), cancellationToken),
            await query.CountAsync(item => item.ExpiryDate >= today && item.ExpiryDate <= today.AddDays(30), cancellationToken));
    }

    public async Task<PagedEmployeePersonnelFilesDto> GetPersonnelFilesAsync(
        PersonnelFileFilterDto filter,
        CancellationToken cancellationToken)
    {
        if (filter.Page < 1 || filter.PageSize is < 1 or > 200) throw new HrValidationException("Pagination values are invalid.");
        var employeeQuery = _dbContext.Employees.AsNoTracking()
            .Include(item => item.Department)
            .Include(item => item.Position)
            .AsQueryable();

        if (filter.EmployeeId.HasValue) employeeQuery = employeeQuery.Where(item => item.Id == filter.EmployeeId.Value);
        if (filter.DepartmentId.HasValue) employeeQuery = employeeQuery.Where(item => item.DepartmentId == filter.DepartmentId.Value);
        if (filter.PositionId.HasValue) employeeQuery = employeeQuery.Where(item => item.PositionId == filter.PositionId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Gender))
        {
            var gender = filter.Gender.Trim().ToLower();
            employeeQuery = employeeQuery.Where(item => item.Gender != null && item.Gender.ToLower() == gender);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            employeeQuery = employeeQuery.Where(item =>
                item.EmployeeNumber.ToLower().Contains(term) ||
                item.FullName.ToLower().Contains(term) ||
                (item.FullNameArabic != null && item.FullNameArabic.ToLower().Contains(term)) ||
                (item.FullNameEnglish != null && item.FullNameEnglish.ToLower().Contains(term)) ||
                (item.NationalId != null && item.NationalId.Contains(term)));
        }

        var employees = await employeeQuery.OrderBy(item => item.EmployeeNumber).ToListAsync(cancellationToken);
        var employeeIds = employees.Select(item => item.Id).ToArray();
        var documents = await _dbContext.EmployeeDocuments.AsNoTracking()
            .Include(item => item.UploadedByUser)
            .Where(item => employeeIds.Contains(item.EmployeeId) && !item.IsDeleted && item.RequiredDocumentCode != null)
            .ToListAsync(cancellationToken);
        var byEmployee = documents.GroupBy(item => item.EmployeeId).ToDictionary(group => group.Key, group => group.ToArray());
        var files = employees.Select(employee => MapPersonnelFile(employee, byEmployee.GetValueOrDefault(employee.Id) ?? [], false)).ToList();

        var completion = filter.CompletionStatus?.Trim().ToLowerInvariant();
        if (completion == "complete") files = files.Where(item => item.MissingDocuments == 0).ToList();
        else if (completion == "incomplete") files = files.Where(item => item.MissingDocuments > 0).ToList();
        if (!string.IsNullOrWhiteSpace(filter.MissingDocumentCode))
        {
            var code = NormalizeRequiredCode(filter.MissingDocumentCode);
            files = files.Where(item => item.Documents.Any(document => document.Code == code && document.IsRequired && !document.IsUploaded)).ToList();
        }

        var total = files.Count;
        var items = files.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return new PagedEmployeePersonnelFilesDto(items, total, filter.Page, filter.PageSize, Pages(total, filter.PageSize));
    }

    public async Task<EmployeePersonnelFileDto> GetPersonnelFileAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees.AsNoTracking()
            .Include(item => item.Department)
            .Include(item => item.Position)
            .SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken)
            ?? throw new HrNotFoundException("Employee was not found.");
        var documents = await _dbContext.EmployeeDocuments.AsNoTracking()
            .Include(item => item.UploadedByUser)
            .Where(item => item.EmployeeId == employeeId && !item.IsDeleted && item.RequiredDocumentCode != null)
            .ToArrayAsync(cancellationToken);
        return MapPersonnelFile(employee, documents, CanViewNationalId());
    }

    public async Task<PersonnelFileSummaryDto> GetPersonnelFileSummaryAsync(CancellationToken cancellationToken)
    {
        var files = await GetPersonnelFilesAsync(new PersonnelFileFilterDto { Page = 1, PageSize = 200 }, cancellationToken);
        var all = files.Items.ToList();
        if (files.TotalPages > 1)
        {
            for (var page = 2; page <= files.TotalPages; page++)
                all.AddRange((await GetPersonnelFilesAsync(new PersonnelFileFilterDto { Page = page, PageSize = 200 }, cancellationToken)).Items);
        }
        return new PersonnelFileSummaryDto(all.Count, all.Count(item => item.MissingDocuments == 0), all.Count(item => item.MissingDocuments > 0), all.Sum(item => item.MissingDocuments));
    }

    public async Task<EmployeeDocumentDetailsDto> UploadRequiredAsync(
        Guid employeeId,
        string documentCode,
        HrUploadFile file,
        CancellationToken cancellationToken)
    {
        var code = NormalizeRequiredCode(documentCode);
        var employee = await _dbContext.Employees.AsNoTracking().SingleOrDefaultAsync(item => item.Id == employeeId, cancellationToken)
            ?? throw new HrNotFoundException("Employee was not found.");
        if (code == RequiredEmployeeDocumentCodes.MilitaryStatus && !EmployeePersonnelFileRules.IsMilitaryDocumentRequired(employee.Gender))
            throw new HrValidationException("A military-status document is required only for male employees.");
        var type = await _dbContext.DocumentTypes.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code && item.IsActive, cancellationToken)
            ?? throw new HrValidationException("The required document type is not configured.");

        await using var validated = await ValidateAndBufferAsync(file, cancellationToken, allowDocx: false);
        var stored = await _fileStorage.SaveAsync("employee-documents", file.FileName, validated.ContentType, validated.Stream, MaximumFileBytes, cancellationToken);
        var existing = await _dbContext.EmployeeDocuments.SingleOrDefaultAsync(
            item => item.EmployeeId == employeeId && item.RequiredDocumentCode == code && !item.IsDeleted,
            cancellationToken);
        var oldStorageKey = existing?.StorageKey;
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            if (existing is null)
            {
                existing = new EmployeeDocument(employeeId, type.Id, type.Name, stored.OriginalFileName, stored.StorageKey,
                    validated.ContentType, stored.Length, stored.Sha256Hash, null, null, null, _currentUser.UserId,
                    DateTimeOffset.UtcNow, code);
                _dbContext.EmployeeDocuments.Add(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _audit.WriteAsync(new AuditWriteRequest("EmployeeDocumentUploaded", nameof(EmployeeDocument), existing.Id.ToString(), employeeId,
                    null, new { Employee = employee.EmployeeNumber, DocumentType = code, existing.FileName },
                    $"Employee document uploaded: {code} for {employee.EmployeeNumber}."), cancellationToken);
            }
            else
            {
                var oldValue = new { Employee = employee.EmployeeNumber, DocumentType = code, existing.FileName, existing.Sha256Hash };
                existing.ReplaceFile(stored.OriginalFileName, stored.StorageKey, validated.ContentType, stored.Length, stored.Sha256Hash,
                    _currentUser.UserId, DateTimeOffset.UtcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _audit.WriteAsync(new AuditWriteRequest("EmployeeDocumentReplaced", nameof(EmployeeDocument), existing.Id.ToString(), employeeId,
                    oldValue, new { Employee = employee.EmployeeNumber, DocumentType = code, existing.FileName, existing.Sha256Hash },
                    $"Employee document replaced: {code} for {employee.EmployeeNumber}."), cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
        if (oldStorageKey is not null) await _fileStorage.DeleteAsync(oldStorageKey, CancellationToken.None);
        return await GetDetailsAsync(existing.Id, cancellationToken);
    }

    private EmployeePersonnelFileDto MapPersonnelFile(Employee employee, IReadOnlyCollection<EmployeeDocument> documents, bool includeNationalId)
    {
        var militaryRequired = EmployeePersonnelFileRules.IsMilitaryDocumentRequired(employee.Gender);
        var uploaded = documents.Where(item => item.RequiredDocumentCode != null).ToDictionary(item => item.RequiredDocumentCode!, StringComparer.OrdinalIgnoreCase);
        var checklist = RequiredDocuments
            .Where(item => item.Code != RequiredEmployeeDocumentCodes.MilitaryStatus || militaryRequired)
            .Select(item =>
            {
                uploaded.TryGetValue(item.Code, out var document);
                return new PersonnelDocumentChecklistItemDto(item.Code, item.Name, item.ArabicName, true, document is not null,
                    document?.Id, document?.FileName, document?.MimeType, document?.FileSize, document?.UploadedByUser.FullName,
                    document?.UploadedAt, document?.UpdatedAt);
            }).ToArray();
        var completed = checklist.Count(item => item.IsUploaded);
        var required = checklist.Length;
        var isArabic = ApiTextLocalizer.IsArabic;
        return new EmployeePersonnelFileDto(employee.Id, employee.EmployeeNumber,
            isArabic ? employee.FullNameArabic ?? employee.FullName : employee.FullNameEnglish ?? employee.FullName,
            isArabic ? employee.Department.NameArabic ?? employee.Department.Name : employee.Department.Name,
            employee.DepartmentId,
            employee.Position is null ? null : isArabic ? employee.Position.NameArabic ?? employee.Position.Name : employee.Position.Name,
            employee.PositionId, employee.Gender, includeNationalId ? employee.NationalId : null, employee.IsActive, employee.IsArchived,
            completed, required, required - completed, EmployeePersonnelFileRules.CompletionPercentage(completed, employee.Gender),
            completed == required ? PersonnelFileCompletionFilters.Complete : PersonnelFileCompletionFilters.Incomplete, checklist);
    }

    private bool CanViewNationalId() => _currentUser.Roles.Any(role =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "HrManager", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeRequiredCode(string value)
    {
        var code = value?.Trim().ToUpperInvariant().Replace('-', '_') ?? string.Empty;
        if (!RequiredEmployeeDocumentCodes.All.Contains(code)) throw new HrValidationException("The required document type is invalid.");
        return code;
    }

    private IQueryable<EmployeeDocument> BaseQuery() => _dbContext.EmployeeDocuments
        .Include(item => item.Employee).ThenInclude(employee => employee.Department)
        .Include(item => item.UploadedByUser)
        .Include(item => item.DocumentTypeDefinition);

    private async Task<EmployeeDocument> GetTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        await _dbContext.EmployeeDocuments.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new HrNotFoundException("Employee document was not found.");

    private async Task<DocumentType> GetActiveTypeAsync(Guid typeId, CancellationToken cancellationToken)
    {
        if (typeId == Guid.Empty) throw new HrValidationException("Document type is required.");
        return await _dbContext.DocumentTypes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == typeId && item.IsActive, cancellationToken)
            ?? throw new HrValidationException("The selected document type does not exist or is inactive.");
    }

    private static IQueryable<EmployeeDocument> ApplySort(IQueryable<EmployeeDocument> query, string sortBy, string direction)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy.Trim().ToLowerInvariant() switch
        {
            "employee" => descending ? query.OrderByDescending(item => item.Employee.FullName) : query.OrderBy(item => item.Employee.FullName),
            "documenttype" => descending ? query.OrderByDescending(item => item.DocumentType) : query.OrderBy(item => item.DocumentType),
            "expirydate" => descending ? query.OrderByDescending(item => item.ExpiryDate) : query.OrderBy(item => item.ExpiryDate),
            "issuedate" => descending ? query.OrderByDescending(item => item.IssueDate) : query.OrderBy(item => item.IssueDate),
            _ => descending ? query.OrderByDescending(item => item.UploadedAt) : query.OrderBy(item => item.UploadedAt)
        };
    }

    private static EmployeeDocumentListItemDto MapList(EmployeeDocument item, DateOnly today, int soonDays)
    {
        var (status, remaining) = Expiry(item.ExpiryDate, today, soonDays);
        var isArabic = ApiTextLocalizer.IsArabic;
        return new EmployeeDocumentListItemDto(
            item.Id,
            item.EmployeeId,
            item.Employee.EmployeeNumber,
            isArabic ? item.Employee.FullNameArabic ?? item.Employee.FullName : item.Employee.FullNameEnglish ?? item.Employee.FullName,
            isArabic ? item.Employee.Department.NameArabic ?? item.Employee.Department.Name : item.Employee.Department.Name,
            item.DocumentTypeId,
            item.DocumentTypeDefinition is null
                ? ApiTextLocalizer.Localize(item.DocumentType)
                : isArabic ? item.DocumentTypeDefinition.NameArabic ?? item.DocumentTypeDefinition.Name : item.DocumentTypeDefinition.Name,
            item.FileName, item.MimeType, item.FileSize,
            item.IssueDate, item.ExpiryDate, status, remaining, item.UploadedByUser.FullName, item.UploadedAt, item.UpdatedAt);
    }

    private static EmployeeDocumentDetailsDto MapDetails(EmployeeDocument item, DateOnly today)
    {
        var (status, remaining) = Expiry(item.ExpiryDate, today, 30);
        var isArabic = ApiTextLocalizer.IsArabic;
        return new EmployeeDocumentDetailsDto(
            item.Id,
            item.EmployeeId,
            item.Employee.EmployeeNumber,
            isArabic ? item.Employee.FullNameArabic ?? item.Employee.FullName : item.Employee.FullNameEnglish ?? item.Employee.FullName,
            item.DocumentTypeId,
            item.DocumentTypeDefinition is null
                ? ApiTextLocalizer.Localize(item.DocumentType)
                : isArabic ? item.DocumentTypeDefinition.NameArabic ?? item.DocumentTypeDefinition.Name : item.DocumentTypeDefinition.Name,
            item.FileName, item.MimeType,
            item.FileSize, item.Sha256Hash, item.IssueDate, item.ExpiryDate, status, remaining, item.Notes,
            item.UploadedByUserId, item.UploadedByUser.FullName, item.UploadedAt, item.UpdatedAt);
    }

    private static (string Status, int? Days) Expiry(DateOnly? expiryDate, DateOnly today, int soonDays)
    {
        if (!expiryDate.HasValue) return (EmployeeDocumentExpiryFilters.NoExpiry, null);
        var days = expiryDate.Value.DayNumber - today.DayNumber;
        if (days < 0) return (EmployeeDocumentExpiryFilters.Expired, days);
        return days <= soonDays
            ? (EmployeeDocumentExpiryFilters.ExpiringSoon, days)
            : (EmployeeDocumentExpiryFilters.Valid, days);
    }

    private static string NormalizeExpiryFilter(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "expired" => EmployeeDocumentExpiryFilters.Expired,
        "expiringsoon" or "expiring_soon" => EmployeeDocumentExpiryFilters.ExpiringSoon,
        "valid" => EmployeeDocumentExpiryFilters.Valid,
        "noexpiry" or "no_expiry" => EmployeeDocumentExpiryFilters.NoExpiry,
        _ => EmployeeDocumentExpiryFilters.All
    };

    private static void ValidateFilter(EmployeeDocumentFilterDto filter)
    {
        if (filter.Page < 1 || filter.PageSize is < 1 or > 200) throw new HrValidationException("Pagination values are invalid.");
        if (filter.ExpiringWithinDays is < 1 or > 365) throw new HrValidationException("Expiry window must be between 1 and 365 days.");
        if (!string.Equals(filter.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            throw new HrValidationException("Sort direction must be asc or desc.");
    }

    private static void ValidateDateRange(DateOnly? issueDate, DateOnly? expiryDate)
    {
        if (issueDate.HasValue && expiryDate.HasValue && expiryDate < issueDate)
            throw new HrValidationException("Expiry date cannot be before issue date.");
    }

    private static async Task<ValidatedUpload> ValidateAndBufferAsync(HrUploadFile file, CancellationToken cancellationToken, bool allowDocx = true)
    {
        if (string.IsNullOrWhiteSpace(file.FileName) || Path.GetFileName(file.FileName).Length > 255)
            throw new HrValidationException("The document file name is required and cannot exceed 255 characters.");
        if (file.Length <= 0) throw new HrValidationException("The uploaded file is empty.");
        if (file.Length > MaximumFileBytes) throw new HrValidationException("The uploaded file exceeds the 10 MB limit.");
        var buffer = new MemoryStream((int)Math.Min(file.Length, MaximumFileBytes));
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await file.Content.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > MaximumFileBytes)
            {
                await buffer.DisposeAsync();
                throw new HrValidationException("The uploaded file exceeds the 10 MB limit.");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (total == 0)
        {
            await buffer.DisposeAsync();
            throw new HrValidationException("The uploaded file is empty.");
        }

        buffer.Position = 0;
        var contentType = DetectContentType(buffer);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var expectedExtensions = contentType switch
        {
            "application/pdf" => new[] { ".pdf" },
            "image/jpeg" => new[] { ".jpg", ".jpeg" },
            "image/png" => new[] { ".png" },
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" when allowDocx => new[] { ".docx" },
            _ => []
        };
        if (expectedExtensions.Length == 0 || !expectedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            await buffer.DisposeAsync();
            throw new HrValidationException(allowDocx
                ? "Allowed document formats are PDF, JPEG, PNG, and DOCX, and the file extension must match its content."
                : "Allowed personnel-document formats are PDF, JPG, JPEG, and PNG, and the file extension must match its content.");
        }
        buffer.Position = 0;
        return new ValidatedUpload(buffer, contentType);
    }

    private static string DetectContentType(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        stream.Position = 0;
        if (read >= 5 && header[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        if (read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff) return "image/jpeg";
        if (read >= 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) return "image/png";
        if (read >= 4 && header[0] == 0x50 && header[1] == 0x4b)
        {
            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                if (archive.GetEntry("[Content_Types].xml") is not null && archive.Entries.Any(entry => entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)))
                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }
            catch (InvalidDataException)
            {
                // The signature alone is not sufficient; invalid archives are rejected below.
            }
            finally
            {
                stream.Position = 0;
            }
        }
        throw new HrValidationException("The file content is not a supported document format.");
    }

    private static int Pages(int total, int pageSize) => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

    private sealed class ValidatedUpload(MemoryStream stream, string contentType) : IAsyncDisposable
    {
        public MemoryStream Stream { get; } = stream;
        public string ContentType { get; } = contentType;
        public ValueTask DisposeAsync() => Stream.DisposeAsync();
    }
}
