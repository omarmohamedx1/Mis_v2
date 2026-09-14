using Microsoft.EntityFrameworkCore;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Npgsql;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class ManualExcuseServiceTests
{
    [ExcusePostgresFact]
    public async Task Manual_validation_permissions_edit_cancel_duplicate_and_attendance()
    {
        var source = Environment.GetEnvironmentVariable("MIS_EXCUSE_TEST_CONNECTION")!;
        var name = "mis_manual_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false }.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(source) { Database = name, Pooling = false }.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options); await db.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow; var date = new DateOnly(2026, 9, 12);
            var department = new Department("Manual Test", "MANUALTEST", now);
            var employee = new Employee("M001", "Manual Test Employee", department.Id, true, now);
            var inactive = new Employee("M002", "Inactive Employee", department.Id, false, now);
            var actor = new User("manual.test", "manual@test.local", "test-only", "HR Test", department.Id, now);
            var record = new AttendanceRecord(employee.Id, date, null, null, 0, 60, 0, 0, "Absent", "DeviceIntegration", null, null, false, actor.Id, now);
            var punch = new AttendancePunch(record.Id, now, "Unknown", "DeviceIntegration", 1, "raw fingerprint", "{}", now);
            db.AddRange(department, employee, inactive, actor, record, punch); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var manager = new TestUser(actor.Id, ["HrManager"], []);
            var officer = new TestUser(actor.Id, ["HrOfficer"], []);
            HrExcuseMissionService Service(ApplicationDbContext context, ICurrentUserContext user) => new(context, user, new(context), null!);
            var service = Service(db, manager); var restricted = Service(db, officer);
            ManualMissionRequest Request(bool fullDay = false, bool approve = false, Guid? emp = null, TimeOnly? from = null, TimeOnly? to = null, DateTimeOffset? revision = null) => new()
            { EmployeeId = emp ?? employee.Id, Type = "LateArrivalExcuse", Date = date, FullDay = fullDay, ApproveImmediately = approve, FromTime = from, ToTime = to, Reason = "Travel delay", ExpectedUpdatedAt = revision };
            Assert.Contains(service.Types(), t => t.Code == "EarlyLeaveExcuse");
            await Assert.ThrowsAsync<HrValidationException>(() => service.SaveManualAsync(null, Request(), default));
            await Assert.ThrowsAsync<HrValidationException>(() => service.SaveManualAsync(null, Request(from: new(10, 0), to: new(9, 0)), default));
            await Assert.ThrowsAsync<HrValidationException>(() => service.SaveManualAsync(null, Request(true, emp: inactive.Id), default));
            await Assert.ThrowsAsync<HrForbiddenException>(() => restricted.SaveManualAsync(null, Request(true, true), default));
            var id = await restricted.SaveManualAsync(null, Request(from: new(9, 0), to: new(10, 0)), default);
            var pending = await service.DetailsAsync(id, default); Assert.Equal("Manual", pending.Source); Assert.Null(pending.VisitId); Assert.Equal("PendingApproval", pending.Status);
            await Assert.ThrowsAsync<HrConflictException>(() => service.SaveManualAsync(null, Request(from: new(9, 0), to: new(10, 0)), default));
            await Assert.ThrowsAsync<HrConflictException>(() => service.SaveManualAsync(id, Request(true), default));
            await restricted.SaveManualAsync(id, Request(true, revision: pending.UpdatedAt), default);
            var fullDay = await service.DetailsAsync(id, default); Assert.True(fullDay.FullDay); Assert.Null(fullDay.FromTime); Assert.Null(fullDay.ToTime);
            await service.DecideAsync(id, new(true, null, fullDay.UpdatedAt), default);
            var approved = await service.DetailsAsync(id, default);
            await Assert.ThrowsAsync<HrValidationException>(() => service.SaveManualAsync(id, Request(true, revision: approved.UpdatedAt), default));
            await Assert.ThrowsAsync<HrForbiddenException>(() => restricted.CancelAsync(id, new("Cancel", approved.UpdatedAt), default));
            var attendance = new HrAttendanceService(db, null!, manager, new HrAuditService(db, manager));
            var details = await attendance.GetDetailsAsync(record.Id, default);
            Assert.True(Assert.Single(details.ApprovedExcuses!).FullDay); Assert.Equal("Absent", details.Status);
            Assert.Equal("raw fingerprint", (await db.AttendancePunches.SingleAsync()).RawValue);
            await service.CancelAsync(id, new("Cancelled with audit", approved.UpdatedAt), default);
            Assert.Empty((await attendance.GetDetailsAsync(record.Id, default)).ApprovedExcuses!);
            var immediateId = await service.SaveManualAsync(null, Request(true, true), default);
            Assert.Equal("Approved", (await service.DetailsAsync(immediateId, default)).Status);
            Assert.True(await db.HrAuditLogs.AnyAsync(a => a.EntityId == id && a.Action == "ExcuseEdited"));
            Assert.True(await db.HrAuditLogs.AnyAsync(a => a.EntityId == id && a.Action == "ExcuseCancelled"));
            Assert.True(await db.HrAuditLogs.AnyAsync(a => a.EntityId == immediateId && a.Action == "ExcuseApproved"));
            async Task<bool> ConcurrentCreate()
            {
                await using var other = new ApplicationDbContext(options);
                try { await Service(other, officer).SaveManualAsync(null, Request(from: new(14, 0), to: new(15, 0)), default); return true; }
                catch (HrConflictException) { return false; }
            }
            Assert.Single(await Task.WhenAll(ConcurrentCreate(), ConcurrentCreate()), x => x);
            Assert.Equal(2, await db.Employees.CountAsync()); Assert.Empty(await db.CollectionFieldVisits.ToListAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed record TestUser(Guid UserId, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions) : ICurrentUserContext { public string Username => "manual.test"; }
}
