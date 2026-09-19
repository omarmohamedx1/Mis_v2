using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Domain.Hr;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrMasterDataService : IHrMasterDataService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHrAuditService _audit;

    public HrMasterDataService(ApplicationDbContext dbContext, IHrAuditService audit)
    {
        _dbContext = dbContext;
        _audit = audit;
    }

    public async Task<PagedMasterDataDto> GetPagedAsync(
        string category,
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = NormalizeCategory(category);
        var query = await VisibleQueryAsync(normalizedCategory, cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(item =>
                item.Code.ToLower().Contains(term) ||
                item.NameEnglish.ToLower().Contains(term) ||
                (item.NameArabic != null && item.NameArabic.ToLower().Contains(term)));
        }

        if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive);

        var totalCount = await query.CountAsync(cancellationToken);
        var isArabic = ApiTextLocalizer.IsArabic;
        var rows = await query
            .OrderBy(item => isArabic ? item.NameArabic ?? item.NameEnglish : item.NameEnglish)
            .ThenBy(item => item.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedMasterDataDto(
            rows.Select(Map).ToArray(),
            totalCount,
            page,
            pageSize,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<IReadOnlyCollection<MasterDataLookupDto>> GetLookupAsync(
        string category,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = await VisibleQueryAsync(NormalizeCategory(category), cancellationToken);
        if (!includeInactive) query = query.Where(item => item.IsActive);

        var isArabic = ApiTextLocalizer.IsArabic;
        return await query
            .OrderBy(item => isArabic ? item.NameArabic ?? item.NameEnglish : item.NameEnglish)
            .Select(item => new MasterDataLookupDto(item.Id, item.Code, item.NameEnglish, item.NameArabic, item.IsActive, item.DepartmentId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MasterDataItemDto> GetByIdAsync(string category, Guid id, CancellationToken cancellationToken)
    {
        var row = await Query(NormalizeCategory(category)).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new HrNotFoundException("The requested master-data record was not found.");
        return Map(row);
    }

    public async Task<MasterDataItemDto> CreateAsync(
        string category,
        SaveMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = NormalizeCategory(category);
        await ValidateAsync(normalizedCategory, request, null, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var entity = CreateEntity(normalizedCategory, request, now);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await GetByIdAsync(normalizedCategory, GetId(entity), cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            "MasterDataCreated",
            GetEntityType(normalizedCategory),
            created.Id.ToString(),
            null,
            null,
            created,
            $"Created {created.NameEnglish} in {normalizedCategory}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<MasterDataItemDto> UpdateAsync(
        string category,
        Guid id,
        SaveMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = NormalizeCategory(category);
        await ValidateAsync(normalizedCategory, request, id, cancellationToken);
        var oldValue = await GetByIdAsync(normalizedCategory, id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var entity = await FindTrackedAsync(normalizedCategory, id, cancellationToken);
        UpdateEntity(normalizedCategory, entity, request, now);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await GetByIdAsync(normalizedCategory, id, cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            "MasterDataUpdated",
            GetEntityType(normalizedCategory),
            id.ToString(),
            null,
            oldValue,
            updated,
            $"Updated {updated.NameEnglish} in {normalizedCategory}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<MasterDataItemDto> SetActiveAsync(
        string category,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var normalizedCategory = NormalizeCategory(category);
        var current = await GetByIdAsync(normalizedCategory, id, cancellationToken);
        if (current.IsActive == isActive) return current;

        var request = new SaveMasterDataRequest
        {
            Code = current.Code,
            NameEnglish = current.NameEnglish,
            NameArabic = current.NameArabic,
            Description = current.Description,
            DepartmentId = current.DepartmentId,
            Address = current.Address,
            DefaultAnnualEntitlement = current.DefaultAnnualEntitlement,
            RequiresAttachment = current.RequiresAttachment,
            RequiresExpiryDate = current.RequiresExpiryDate,
            IsActive = isActive
        };

        return await UpdateAsync(normalizedCategory, id, request, cancellationToken);
    }

    public async Task DeleteAsync(string category, Guid id, CancellationToken cancellationToken)
    {
        var normalizedCategory = NormalizeCategory(category);
        var current = await GetByIdAsync(normalizedCategory, id, cancellationToken);
        if (await IsInUseAsync(normalizedCategory, id, cancellationToken))
            throw new HrConflictException("This master-data record is in use and cannot be deleted. Deactivate it instead.");

        var entity = await FindTrackedAsync(normalizedCategory, id, cancellationToken);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync(new AuditWriteRequest(
            "MasterDataDeleted",
            GetEntityType(normalizedCategory),
            id.ToString(),
            null,
            current,
            null,
            $"Deleted {current.NameEnglish} from {normalizedCategory}."), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> IsInUseAsync(string category, Guid id, CancellationToken cancellationToken) => category switch
    {
        HrMasterDataCategories.Departments =>
            await _dbContext.Employees.AnyAsync(item => item.DepartmentId == id, cancellationToken)
            || await _dbContext.Users.AnyAsync(item => item.DepartmentId == id, cancellationToken)
            || await _dbContext.Positions.AnyAsync(item => item.DepartmentId == id, cancellationToken),
        HrMasterDataCategories.Positions =>
            await _dbContext.Employees.AnyAsync(item => item.PositionId == id, cancellationToken),
        HrMasterDataCategories.Branches =>
            await _dbContext.Employees.AnyAsync(item => item.BranchId == id, cancellationToken),
        HrMasterDataCategories.EmploymentTypes =>
            await _dbContext.Employees.AnyAsync(item => item.EmploymentTypeId == id, cancellationToken),
        HrMasterDataCategories.ContractTypes =>
            await _dbContext.EmployeeContracts.AnyAsync(item => item.ContractTypeId == id, cancellationToken),
        HrMasterDataCategories.LeaveTypes =>
            await _dbContext.LeaveRequests.AnyAsync(item => item.LeaveTypeId == id, cancellationToken)
            || await _dbContext.EmployeeLeaveEntitlements.AnyAsync(item => item.LeaveTypeId == id, cancellationToken),
        HrMasterDataCategories.DocumentTypes =>
            await _dbContext.EmployeeDocuments.AnyAsync(item => item.DocumentTypeId == id, cancellationToken),
        HrMasterDataCategories.DelegationTypes =>
            await _dbContext.EmployeeDelegations.AnyAsync(item => item.DelegationTypeId == id, cancellationToken),
        _ => true
    };

    private async Task<IQueryable<MasterRow>> VisibleQueryAsync(string category, CancellationToken cancellationToken)
    {
        var query = Query(category);
        if (category != HrMasterDataCategories.Departments) return query;

        var hiddenIds = await HiddenDepartmentIdsAsync(cancellationToken);
        return hiddenIds.Length == 0 ? query : query.Where(item => !hiddenIds.Contains(item.Id));
    }

    private async Task<Guid[]> HiddenDepartmentIdsAsync(CancellationToken cancellationToken)
    {
        var organizations = await LoadOrganizationLookupsAsync(cancellationToken);
        var departments = await _dbContext.Departments.AsNoTracking()
            .Select(department => new { department.Id, department.Code, Name = department.Name, department.NameArabic })
            .ToListAsync(cancellationToken);
        return departments
            .Where(department => HrDepartmentCatalog.IsClientOrLegacyDepartment(
                department.Code, department.Name, department.NameArabic, organizations))
            .Select(department => department.Id)
            .ToArray();
    }

    private async Task EnsureInternalDepartmentAsync(SaveMasterDataRequest request, CancellationToken cancellationToken)
    {
        var organizations = await LoadOrganizationLookupsAsync(cancellationToken);
        if (HrDepartmentCatalog.IsClientOrLegacyDepartment(request.Code, request.NameEnglish, request.NameArabic, organizations))
            throw new HrValidationException("Banks and companies are not HR departments.");
    }

    private async Task<IReadOnlyList<(string Code, string NameEnglish, string NameArabic)>> LoadOrganizationLookupsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.CollectionClientOrganizations.AsNoTracking()
            .Select(organization => new { organization.Code, organization.NameEnglish, organization.NameArabic })
            .ToListAsync(cancellationToken);
        return rows.Select(row => (row.Code, row.NameEnglish, row.NameArabic)).ToList();
    }

    private IQueryable<MasterRow> Query(string category)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        return category switch
        {
        HrMasterDataCategories.Departments => _dbContext.Departments.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, IsActive = item.IsActive,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
        HrMasterDataCategories.Positions => _dbContext.Positions.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, DepartmentId = item.DepartmentId,
            DepartmentName = item.Department == null
                ? null
                : isArabic ? item.Department.NameArabic ?? item.Department.Name : item.Department.Name,
            IsActive = item.IsActive,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
        HrMasterDataCategories.Branches => _dbContext.Branches.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, Address = item.Address,
            IsActive = item.IsActive, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
        HrMasterDataCategories.EmploymentTypes => _dbContext.EmploymentTypes.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, IsActive = item.IsActive,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
        HrMasterDataCategories.ContractTypes => _dbContext.ContractTypes.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, IsActive = item.IsActive,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
        HrMasterDataCategories.LeaveTypes => _dbContext.LeaveTypes.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, IsActive = item.IsActive,
            DefaultAnnualEntitlement = item.DefaultAnnualEntitlement, RequiresAttachment = item.RequiresAttachment,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
        HrMasterDataCategories.DocumentTypes => _dbContext.DocumentTypes.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, IsActive = item.IsActive,
            RequiresExpiryDate = item.RequiresExpiryDate, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
        HrMasterDataCategories.DelegationTypes => _dbContext.DelegationTypes.AsNoTracking().Select(item => new MasterRow
        {
            Id = item.Id, Category = category, Code = item.Code, NameEnglish = item.Name,
            NameArabic = item.NameArabic, Description = item.Description, IsActive = item.IsActive,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        }),
            _ => throw new HrValidationException("Unknown master-data category.")
        };
    }

    private async Task ValidateAsync(string category, SaveMasterDataRequest request, Guid? excludingId, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await Query(category).AnyAsync(item => item.Id != excludingId && item.Code == normalizedCode, cancellationToken))
        {
            throw new HrConflictException("A master-data record with this code already exists.");
        }

        if (request.DepartmentId.HasValue &&
            !await _dbContext.Departments.AnyAsync(item => item.Id == request.DepartmentId && item.IsActive, cancellationToken))
        {
            throw new HrValidationException("The selected department does not exist or is inactive.");
        }

        if (category == HrMasterDataCategories.LeaveTypes && request.DefaultAnnualEntitlement is null)
        {
            throw new HrValidationException("Default annual entitlement is required for leave types.");
        }

        if (category == HrMasterDataCategories.Departments)
            await EnsureInternalDepartmentAsync(request, cancellationToken);
    }

    private static object CreateEntity(string category, SaveMasterDataRequest request, DateTimeOffset now) => category switch
    {
        HrMasterDataCategories.Departments => new Department(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now),
        HrMasterDataCategories.Positions => CreatePosition(request, now),
        HrMasterDataCategories.Branches => CreateBranch(request, now),
        HrMasterDataCategories.EmploymentTypes => CreateEmploymentType(request, now),
        HrMasterDataCategories.ContractTypes => CreateContractType(request, now),
        HrMasterDataCategories.LeaveTypes => CreateLeaveType(request, now),
        HrMasterDataCategories.DocumentTypes => CreateDocumentType(request, now),
        HrMasterDataCategories.DelegationTypes => CreateDelegationType(request, now),
        _ => throw new HrValidationException("Unknown master-data category.")
    };

    private static Position CreatePosition(SaveMasterDataRequest request, DateTimeOffset now)
    {
        var entity = new Position(request.NameEnglish, request.Code, request.DepartmentId, now);
        entity.Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.DepartmentId, request.IsActive, now);
        return entity;
    }

    private static Branch CreateBranch(SaveMasterDataRequest request, DateTimeOffset now)
    {
        var entity = new Branch(request.NameEnglish, request.Code, now);
        entity.Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.Address, request.IsActive, now);
        return entity;
    }

    private static EmploymentType CreateEmploymentType(SaveMasterDataRequest request, DateTimeOffset now)
    {
        var entity = new EmploymentType(request.NameEnglish, request.Code, now);
        entity.Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now);
        return entity;
    }

    private static ContractType CreateContractType(SaveMasterDataRequest request, DateTimeOffset now)
    {
        var entity = new ContractType(request.NameEnglish, request.Code, now);
        entity.Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now);
        return entity;
    }

    private static LeaveType CreateLeaveType(SaveMasterDataRequest request, DateTimeOffset now)
    {
        var entity = new LeaveType(request.NameEnglish, request.Code, request.DefaultAnnualEntitlement ?? 0, request.RequiresAttachment ?? false, now);
        entity.Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.DefaultAnnualEntitlement ?? 0, request.RequiresAttachment ?? false, request.IsActive, now);
        return entity;
    }

    private static DocumentType CreateDocumentType(SaveMasterDataRequest request, DateTimeOffset now)
    {
        var entity = new DocumentType(request.NameEnglish, request.Code, request.RequiresExpiryDate ?? false, now);
        entity.Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.RequiresExpiryDate ?? false, request.IsActive, now);
        return entity;
    }

    private static DelegationType CreateDelegationType(SaveMasterDataRequest request, DateTimeOffset now)
    {
        var entity = new DelegationType(request.NameEnglish, request.Code, now);
        entity.Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now);
        return entity;
    }

    private async Task<object> FindTrackedAsync(string category, Guid id, CancellationToken cancellationToken)
    {
        object? entity = category switch
        {
            HrMasterDataCategories.Departments => await _dbContext.Departments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            HrMasterDataCategories.Positions => await _dbContext.Positions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            HrMasterDataCategories.Branches => await _dbContext.Branches.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            HrMasterDataCategories.EmploymentTypes => await _dbContext.EmploymentTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            HrMasterDataCategories.ContractTypes => await _dbContext.ContractTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            HrMasterDataCategories.LeaveTypes => await _dbContext.LeaveTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            HrMasterDataCategories.DocumentTypes => await _dbContext.DocumentTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            HrMasterDataCategories.DelegationTypes => await _dbContext.DelegationTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken),
            _ => null
        };

        return entity ?? throw new HrNotFoundException("The requested master-data record was not found.");
    }

    private static void UpdateEntity(string category, object entity, SaveMasterDataRequest request, DateTimeOffset now)
    {
        switch (category)
        {
            case HrMasterDataCategories.Departments:
                ((Department)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now);
                break;
            case HrMasterDataCategories.Positions:
                ((Position)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.DepartmentId, request.IsActive, now);
                break;
            case HrMasterDataCategories.Branches:
                ((Branch)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.Address, request.IsActive, now);
                break;
            case HrMasterDataCategories.EmploymentTypes:
                ((EmploymentType)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now);
                break;
            case HrMasterDataCategories.ContractTypes:
                ((ContractType)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now);
                break;
            case HrMasterDataCategories.LeaveTypes:
                ((LeaveType)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.DefaultAnnualEntitlement ?? 0, request.RequiresAttachment ?? false, request.IsActive, now);
                break;
            case HrMasterDataCategories.DocumentTypes:
                ((DocumentType)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.RequiresExpiryDate ?? false, request.IsActive, now);
                break;
            case HrMasterDataCategories.DelegationTypes:
                ((DelegationType)entity).Update(request.NameEnglish, request.Code, request.NameArabic, request.Description, request.IsActive, now);
                break;
            default:
                throw new HrValidationException("Unknown master-data category.");
        }
    }

    private static Guid GetId(object entity) => entity switch
    {
        Department item => item.Id,
        Position item => item.Id,
        Branch item => item.Id,
        EmploymentType item => item.Id,
        ContractType item => item.Id,
        LeaveType item => item.Id,
        DocumentType item => item.Id,
        DelegationType item => item.Id,
        _ => throw new HrValidationException("Unknown master-data entity.")
    };

    private static string GetEntityType(string category) => category switch
    {
        HrMasterDataCategories.Departments => nameof(Department),
        HrMasterDataCategories.Positions => nameof(Position),
        HrMasterDataCategories.Branches => nameof(Branch),
        HrMasterDataCategories.EmploymentTypes => nameof(EmploymentType),
        HrMasterDataCategories.ContractTypes => nameof(ContractType),
        HrMasterDataCategories.LeaveTypes => nameof(LeaveType),
        HrMasterDataCategories.DocumentTypes => nameof(DocumentType),
        HrMasterDataCategories.DelegationTypes => nameof(DelegationType),
        _ => "MasterData"
    };

    private static string NormalizeCategory(string category)
    {
        var normalized = category.Trim().ToLowerInvariant();
        if (!HrMasterDataCategories.All.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new HrValidationException("Unknown master-data category.");
        }

        return normalized;
    }

    private static MasterDataItemDto Map(MasterRow item) => new(
        item.Id,
        item.Category,
        item.Code,
        item.NameEnglish,
        item.NameArabic,
        item.Description,
        item.DepartmentId,
        item.DepartmentName,
        item.Address,
        item.IsActive,
        item.DefaultAnnualEntitlement,
        item.RequiresAttachment,
        item.RequiresExpiryDate,
        item.CreatedAt,
        item.UpdatedAt);

    private sealed class MasterRow
    {
        public Guid Id { get; init; }
        public string Category { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string NameEnglish { get; init; } = string.Empty;
        public string? NameArabic { get; init; }
        public string? Description { get; init; }
        public Guid? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public string? Address { get; init; }
        public bool IsActive { get; init; }
        public decimal? DefaultAnnualEntitlement { get; init; }
        public bool? RequiresAttachment { get; init; }
        public bool? RequiresExpiryDate { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
