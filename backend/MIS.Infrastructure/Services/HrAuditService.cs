using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class HrAuditService : IHrAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;

    public HrAuditService(ApplicationDbContext dbContext, ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.EntityId, out var entityId) || entityId == Guid.Empty)
        {
            throw new HrValidationException("Audit entity identifiers must be valid GUID values.");
        }

        var audit = new HrAuditLog(
            _currentUser.UserId,
            request.Action,
            request.EntityType,
            entityId,
            request.EmployeeId,
            Serialize(request.OldValue),
            Serialize(request.NewValue),
            request.Description,
            DateTimeOffset.UtcNow);

        _dbContext.HrAuditLogs.Add(audit);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedAuditLogsDto> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? action,
        string? entityType,
        Guid? employeeId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var isArabic = ApiTextLocalizer.IsArabic;
        var query = _dbContext.HrAuditLogs
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Employee)
            .AsQueryable();

        var canViewExcuses = _currentUser.Roles.Any(r => r is "HrManager" or "HrOfficer") ||
            _currentUser.Permissions.Any(p => p is "hr.excuses.view" or "hr.excuses.manage" or "hr.excuses.approve");
        if (!canViewExcuses) query = query.Where(item => item.EntityType != "HrExcuseMission");

        var canViewInsurance = _currentUser.Roles.Any(r => r is "HrManager" or "HrOfficer") ||
            _currentUser.Permissions.Any(p => p is "hr.social_insurance.view" or "hr.social_insurance.manage");
        if (!canViewInsurance) query = query.Where(item => item.EntityType != "SocialInsuranceRecord" && item.EntityType != "SocialInsuranceImport");

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(item =>
                item.Action.ToLower().Contains(term) ||
                item.EntityType.ToLower().Contains(term) ||
                (item.Description != null && item.Description.ToLower().Contains(term)) ||
                (item.User != null && item.User.Username.ToLower().Contains(term)) ||
                (item.Employee != null &&
                    (item.Employee.FullName.ToLower().Contains(term) ||
                     (item.Employee.FullNameArabic != null && item.Employee.FullNameArabic.ToLower().Contains(term)) ||
                     (item.Employee.FullNameEnglish != null && item.Employee.FullNameEnglish.ToLower().Contains(term)) ||
                     item.Employee.EmployeeNumber.ToLower().Contains(term))));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalized = action.Trim().ToLower();
            query = query.Where(item => item.Action.ToLower() == normalized);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalized = entityType.Trim().ToLower();
            query = query.Where(item => item.EntityType.ToLower() == normalized);
        }

        if (employeeId.HasValue) query = query.Where(item => item.EmployeeId == employeeId);
        if (from.HasValue) query = query.Where(item => item.Timestamp >= from);
        if (to.HasValue) query = query.Where(item => item.Timestamp <= to);

        var totalCount = await query.CountAsync(cancellationToken);
        var records = await query
            .OrderByDescending(item => item.Timestamp)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.UserId,
                Username = item.User == null ? "System" : item.User.Username,
                item.Action,
                item.EntityType,
                item.EntityId,
                item.EmployeeId,
                EmployeeName = item.Employee == null
                    ? null
                    : isArabic
                        ? item.Employee.FullNameArabic ?? item.Employee.FullName
                        : item.Employee.FullNameEnglish ?? item.Employee.FullName,
                item.Description,
                item.OldValue,
                item.NewValue,
                item.Timestamp
            })
            .ToListAsync(cancellationToken);

        var items = records.Select(item => new AuditLogItemDto(
            item.Id,
            item.UserId ?? Guid.Empty,
            ApiTextLocalizer.Localize(item.Username),
            item.Action,
            item.EntityType,
            item.EntityId.ToString(),
            item.EmployeeId,
            item.EmployeeName,
            item.Description is null ? null : ApiTextLocalizer.Localize(item.Description),
            BuildChanges(item.OldValue, item.NewValue),
            item.Timestamp)).ToArray();

        return new PagedAuditLogsDto(items, totalCount, page, pageSize, CalculatePages(totalCount, pageSize));
    }

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static IReadOnlyCollection<AuditChangeDto> BuildChanges(string? oldJson, string? newJson)
    {
        var oldValues = ReadValues(oldJson);
        var newValues = ReadValues(newJson);
        var keys = oldValues.Keys.Union(newValues.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);

        return keys
            .Select(key => new AuditChangeDto(
                key,
                ApiTextLocalizer.LocalizeValue(oldValues.GetValueOrDefault(key)),
                ApiTextLocalizer.LocalizeValue(newValues.GetValueOrDefault(key))))
            .Where(change => !string.Equals(change.OldValue, change.NewValue, StringComparison.Ordinal))
            .ToArray();
    }

    private static Dictionary<string, string?> ReadValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(json);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                Flatten(property.Value, property.Name, values);
            }
        }
        else
        {
            Flatten(document.RootElement, "Value", values);
        }

        return values;
    }

    private static void Flatten(JsonElement value, string path, IDictionary<string, string?> destination)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = value.EnumerateObject().ToArray();
            if (properties.Length == 0)
            {
                destination[path] = "{}";
                return;
            }

            foreach (var property in properties)
            {
                Flatten(property.Value, $"{path}.{property.Name}", destination);
            }
            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var items = value.EnumerateArray().ToArray();
            if (items.Length == 0)
            {
                destination[path] = "[]";
                return;
            }

            for (var index = 0; index < items.Length; index++)
            {
                Flatten(items[index], $"{path}[{index}]", destination);
            }
            return;
        }

        destination[path] = Format(value);
    }

    private static string? Format(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => ApiTextLocalizer.Localize("Yes"),
        JsonValueKind.False => ApiTextLocalizer.Localize("No"),
        _ => value.GetRawText()
    };

    private static int CalculatePages(int count, int pageSize) => count == 0 ? 0 : (int)Math.Ceiling(count / (double)pageSize);
}
