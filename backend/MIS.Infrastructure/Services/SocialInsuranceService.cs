using Microsoft.EntityFrameworkCore;
using Npgsql;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Constants;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Services;

public sealed class SocialInsuranceService(ApplicationDbContext db, ICurrentUserContext user, IHrAuditService audit) : ISocialInsuranceService
{
    private bool Has(string permission) =>
        user.Roles.Contains(SystemRoleNames.Admin) ||
        user.Permissions.Contains("*") ||
        user.Permissions.Contains(permission);
    private bool CanManage => Has("hr.social_insurance.manage") || user.Roles.Any(r => r is SystemRoleNames.HrManager or SystemRoleNames.HrOfficer);
    private void ReadAccess() { if (!CanManage && !Has("hr.social_insurance.view")) throw new HrForbiddenException("Social insurance access is required."); }
    private void WriteAccess() { if (!CanManage) throw new HrForbiddenException("Social insurance management permission is required."); }
    public async Task<SocialInsurancePageDto> ListAsync(string? search, Guid? departmentId, string? status, Guid? employeeId, int page, int pageSize, CancellationToken ct)
    {
        ReadAccess();
        if (page < 1 || pageSize < 1 || pageSize > 100) throw new HrValidationException("Invalid page size.");
        if (!string.IsNullOrEmpty(status) && status is not ("Insured" or "NotInsured" or "Suspended" or "Ended")) throw new HrValidationException("Invalid insurance status.");
        var employees = db.Employees.AsNoTracking().AsQueryable();
        if (employeeId.HasValue) employees = employees.Where(e => e.Id == employeeId);
        if (departmentId.HasValue) employees = employees.Where(e => e.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(search)) {
            var term = search.Trim().ToLower(); var number = SocialInsuranceRecord.NormalizeNumber(search);
            employees = employees.Where(e => e.FullName.ToLower().Contains(term) || (e.FullNameArabic != null && e.FullNameArabic.ToLower().Contains(term)) || (e.FullNameEnglish != null && e.FullNameEnglish.ToLower().Contains(term)) || e.EmployeeNumber.ToLower().Contains(term) || db.SocialInsuranceRecords.Any(r => r.EmployeeId == e.Id && r.SocialInsuranceNumber.Contains(number)));
        }
        var records = db.SocialInsuranceRecords.AsNoTracking().Where(r => employees.Any(e => e.Id == r.EmployeeId));
        var insured = await records.CountAsync(r => r.InsuranceStatus == "Insured", ct);
        var notInsured = await employees.CountAsync(e => !db.SocialInsuranceRecords.Any(r => r.EmployeeId == e.Id) || db.SocialInsuranceRecords.Any(r => r.EmployeeId == e.Id && r.InsuranceStatus == "NotInsured"), ct);
        var ended = await records.CountAsync(r => r.InsuranceStatus == "Ended", ct);
        // Show the current record, or the latest historical record, for each existing employee.
        var query = employees.Select(e => new { Employee = e, Record = db.SocialInsuranceRecords.Where(r => r.EmployeeId == e.Id).OrderBy(r => r.InsuranceStatus == "Ended").ThenByDescending(r => r.CreatedAt).FirstOrDefault() });
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => (x.Record == null ? "NotInsured" : x.Record.InsuranceStatus) == status);
        var count = await query.CountAsync(ct);
        var arabic = ApiTextLocalizer.IsArabic;
        var sensitive = Has(SystemPermissionCodes.HrSensitiveView) || user.Roles.Contains(SystemRoleNames.HrManager);
        var rows = await query.OrderBy(x => x.Employee.EmployeeNumber).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new {
            x.Employee.Id, x.Employee.EmployeeNumber,
            Name = arabic ? x.Employee.FullNameArabic ?? x.Employee.FullName : x.Employee.FullNameEnglish ?? x.Employee.FullName,
            x.Employee.DepartmentId, Department = arabic ? x.Employee.Department.NameArabic ?? x.Employee.Department.Name : x.Employee.Department.Name,
            Position = x.Employee.Position == null ? null : (arabic ? x.Employee.Position.NameArabic ?? x.Employee.Position.Name : x.Employee.Position.Name),
            NationalId = sensitive ? x.Employee.NationalId : null, x.Record
        }).ToListAsync(ct);
        return new(rows.Select(x => new SocialInsuranceEmployeeDto(x.Id, x.EmployeeNumber, x.Name, x.DepartmentId, x.Department, x.Position, x.NationalId, x.Record == null ? null : Map(x.Record))).ToArray(), count, page, pageSize, (int)Math.Ceiling(count / (double)pageSize), insured, notInsured, ended, CanManage);
    }
    public async Task<IReadOnlyCollection<SocialInsuranceDto>> HistoryAsync(Guid employeeId, CancellationToken ct)
    {
        ReadAccess();
        return (await db.SocialInsuranceRecords.AsNoTracking().Where(r => r.EmployeeId == employeeId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct)).Select(Map).ToArray();
    }
    public async Task<SocialInsuranceDto> SaveAsync(Guid? id, SaveSocialInsuranceRequest request, CancellationToken ct)
    {
        WriteAccess();
        if (!await db.Employees.AnyAsync(e => e.Id == request.EmployeeId, ct)) throw new HrNotFoundException("Employee not found.");
        var record = id.HasValue ? await db.SocialInsuranceRecords.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new HrNotFoundException("Insurance record not found.") : null;
        if (record != null && record.EmployeeId != request.EmployeeId) throw new HrValidationException("The insurance employee cannot be changed.");
        var old = record == null ? null : Map(record);
        try {
            if (record == null) { record = new(request.EmployeeId, request.SocialInsuranceNumber, request.InsuranceStartDate, request.InsurableSalary, request.InsuranceStatus, request.InsuranceOffice, request.ReferenceNumber, request.Notes); db.SocialInsuranceRecords.Add(record); }
            else record.Update(request.SocialInsuranceNumber, request.InsuranceStartDate, request.InsurableSalary, request.InsuranceStatus, request.InsuranceOffice, request.ReferenceNumber, request.Notes);
        } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { throw new HrValidationException(ex.Message); }
        await PersistAsync(record, old, id.HasValue ? "InsuranceRecordEdited" : "InsuranceRecordCreated", ct);
        return Map(record);
    }
    public async Task<SocialInsuranceDto> EndAsync(Guid id, EndSocialInsuranceRequest request, CancellationToken ct)
    {
        WriteAccess();
        var record = await db.SocialInsuranceRecords.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new HrNotFoundException("Insurance record not found.");
        var old = Map(record);
        try { record.End(request.InsuranceEndDate); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { throw new HrValidationException(ex.Message); }
        await PersistAsync(record, old, "InsuranceEnded", ct);
        return Map(record);
    }
    private async Task PersistAsync(SocialInsuranceRecord record, SocialInsuranceDto? old, string action, CancellationToken ct)
    {
        // Audit and business data commit together. Unique indexes arbitrate concurrent creates.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try {
            await audit.WriteAsync(new AuditWriteRequest(action, "SocialInsuranceRecord", record.Id.ToString(), record.EmployeeId, old, Map(record), action), ct);
            await transaction.CommitAsync(ct);
        } catch (DbUpdateConcurrencyException) { throw new HrConflictException("Insurance changed. Refresh and try again."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new HrConflictException("An active insurance record already exists for this employee or insurance number."); }
    }
    private static SocialInsuranceDto Map(SocialInsuranceRecord r) => new(r.Id, r.EmployeeId, r.SocialInsuranceNumber, r.InsuranceStartDate, r.InsuranceEndDate, r.InsurableSalary, r.InsuranceStatus, r.InsuranceOffice, r.ReferenceNumber, r.Notes);
}
