using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;
using MIS.Infrastructure.Files;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Services;
using Npgsql;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class HrExcuseTests
{
    [Fact]
    public void Approval_is_explicit_and_rejection_is_final_until_source_review()
    {
        var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var m = new HrExcuseMission(Guid.NewGuid(), "MedicalExcuse", new(2026, 9, 12), null, null, "Medical appointment", null, actor, now);
        Assert.Equal("PendingApproval", m.Status);
        Assert.Throws<ArgumentException>(() => m.Reject(actor, " ", now));
        m.Reject(actor, "Missing evidence", now);
        Assert.Equal("Rejected", m.Status); Assert.Null(m.ApprovedAt);
        Assert.Throws<InvalidOperationException>(() => m.Approve(actor, now));
    }

    [Fact]
    public void Manual_full_day_hides_times_and_partial_requires_ordered_range()
    {
        var actor = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var m = new HrExcuseMission(Guid.NewGuid(), "LateArrivalExcuse", new(2026, 9, 12), new(9, 0), new(10, 0), "Traffic", null, actor, now);
        Assert.Equal("Manual", m.SourceType);
        m.SetManualPeriod(true, new(9, 0), new(10, 0), now);
        Assert.True(m.FullDay); Assert.Null(m.FromTime); Assert.Null(m.ToTime);
        Assert.Throws<ArgumentException>(() => m.SetManualPeriod(false, new(10, 0), new(9, 0), now));
        m.SetManualPeriod(false, new(9, 0), new(10, 30), now);
        Assert.False(m.FullDay); Assert.Equal(new TimeOnly(9, 0), m.FromTime); Assert.Equal(new TimeOnly(10, 30), m.ToTime);
    }

    [Fact]
    public void Unlinked_collectors_cannot_be_approved_and_unknown_end_is_not_fabricated()
    {
        var m = new HrExcuseMission(null, "FieldVisitMission", new(2026, 9, 12), new(11, 0), null, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid());
        Assert.Null(m.ToTime);
        Assert.Throws<ArgumentException>(() => m.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => m.SetValues(m.Type, m.Date, new(11, 0), new(10, 0), null, null, DateTimeOffset.UtcNow));
    }

    [ExcusePostgresFact]
    public async Task PostgreSQL_workflow_files_notifications_concurrency_attendance_and_authorization()
    {
        var source = Environment.GetEnvironmentVariable("MIS_EXCUSE_TEST_CONNECTION")!;
        var name = "mis_excuse_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(source) { Database = name, Pooling = false };
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false }.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), name));
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options); await db.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow; var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            var scheduled = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(11, 0)), zone));
            var department = new Department("Excuse Test", "EXCUSETEST", now);
            var employee = new Employee("EX-001", "Ahmed Test", department.Id, true, now);
            var actor = new User("excuse.test", "excuse@test.local", "test-only", "HR Tester", department.Id, now);
            var collector = new User("collector.test", "collector@test.local", "test-only", "Ahmed Collector", department.Id, now);
            collector.LinkEmployee(employee.Id, now);
            var org = new ClientOrganization("EX", "بنك الاختبار", "Test Bank", "BANK", now);
            var portfolio = new CollectionPortfolio(org.Id, "EX", "محفظة", "Portfolio", "EGP", now);
            var customer = new CollectionCustomer(org.Id, "C1", "عميل الاختبار", "Test Customer", now);
            var bucket = new DelinquencyBucketDefinition(org.Id, portfolio.Id, "B1", "فئة", "Bucket", 0, 30, 1, now);
            var c = new CollectionCase(portfolio.Id, customer.Id, "EX-CASE", "EX-ACCOUNT", 100, 100, 10, 1, bucket.Id, now);
            var visit = new FieldVisit(c.Id, collector.Id, scheduled, "Cairo test address", null, null, actor.Id, now, "Collection", "Visit notes");
            var attendance = new AttendanceRecord(employee.Id, date, null, null, 0, 0, 0, 0, "Absent", "DeviceIntegration", "Raw attendance retained", null, false, actor.Id, now);
            var punch = new AttendancePunch(attendance.Id, scheduled.AddHours(-2), "Unknown", "DeviceIntegration", 1, "original fingerprint", "{}", now);
            db.AddRange(department, employee, actor, collector, org, portfolio, customer, bucket, c, visit, attendance, punch); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var user = new TestUser(actor.Id, ["HrManager"], []);
            var storage = new LocalHrFileStorage(Options.Create(new HrFileStorageOptions { RootPath = root }));
            HrExcuseMissionService Service(ApplicationDbContext context, ICurrentUserContext? u = null) => new(context, u ?? user, new(context), storage);
            var service = Service(db);
            var first = Assert.Single((await service.ListAsync(new(), default)).Items);
            Assert.Equal("PendingApproval", first.Status); Assert.Equal(visit.Id, first.VisitId); Assert.Equal(employee.Id, first.EmployeeId);
            Assert.Equal("EX-001", first.EmployeeNumber); Assert.Equal("Test Customer", first.Customer); Assert.Equal("Test Bank", first.Organization); Assert.Null(first.ToTime);
            Assert.Equal(first.Id, Assert.Single(await service.NotificationsAsync(default)).Id);
            Assert.Equal(first.Id, Assert.Single(await service.NotificationsAsync(default)).Id);
            Assert.Equal(1, await db.HrExcuseMissions.CountAsync());
            Assert.Equal(1, await db.HrAuditLogs.CountAsync(a => a.Action == "VisitRequestSubmitted"));
            Assert.Equal(1, (await service.ListAsync(new() { Search = "Ahmed", Source = "FieldVisit", DepartmentId = department.Id, EmployeeId = employee.Id, Date = date }, default)).TotalCount);
            Assert.Equal(0, (await service.ListAsync(new() { Search = "99999999999" }, default)).TotalCount);
            var viewer = Service(db, new TestUser(actor.Id, [], ["hr.excuses.view"]));
            Assert.Single((await viewer.ListAsync(new(), default)).Items);
            await Assert.ThrowsAsync<HrForbiddenException>(() => viewer.NotificationsAsync(default));
            await Assert.ThrowsAsync<HrForbiddenException>(() => viewer.DecideAsync(first.Id, new(true, null, first.UpdatedAt), default));
            await Assert.ThrowsAsync<HrForbiddenException>(() => Service(db, new TestUser(actor.Id, [], [])).ListAsync(new(), default));
            var officer = Service(db, new TestUser(actor.Id, ["HrOfficer"], []));
            await Assert.ThrowsAsync<HrForbiddenException>(() => officer.NotificationsAsync(default));
            await Assert.ThrowsAsync<HrValidationException>(() => service.DecideAsync(first.Id, new(false, "", first.UpdatedAt), default));
            db.ChangeTracker.Clear();
            await service.DecideAsync(first.Id, new(true, null, first.UpdatedAt, new(13, 0), "Approved field duty"), default);
            Assert.Empty(await service.NotificationsAsync(default));
            var approved = await service.DetailsAsync(first.Id, default); Assert.Equal("Approved", approved.Status);
            var attService = new HrAttendanceService(db, null!, user, new HrAuditService(db, user));
            var att = await attService.GetDetailsAsync(attendance.Id, default);
            Assert.Equal("Absent", att.Status); Assert.Equal(first.Id, Assert.Single(att.ApprovedExcuses!).Id);
            Assert.Single(Assert.Single((await attService.GetPagedAsync(new(), default)).Items).ApprovedExcuses!);
            Assert.Equal("original fingerprint", (await db.AttendancePunches.SingleAsync()).RawValue);
            Assert.Equal(1, await db.CollectionFieldVisits.CountAsync()); Assert.Equal(1, await db.Employees.CountAsync());
            Assert.Equal(scheduled, (await db.CollectionFieldVisits.AsNoTracking().SingleAsync()).ScheduledAt);
            Assert.Equal(visit.Id, (await service.OriginalVisitAsync(visit.Id, default)).VisitId);

            var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\nTest supporting file\n%%EOF");
            using (var stream = new MemoryStream(bytes)) await service.UploadAsync(first.Id, null, new("evidence.pdf", "application/pdf", bytes.Length, stream), default);
            var file = Assert.Single((await service.DetailsAsync(first.Id, default)).Attachments);
            Assert.Equal(actor.Id, file.UploadedByUserId); Assert.Equal(first.Id, file.ExcuseId);
            var content = await service.DownloadAsync(first.Id, file.Id, default);
            await using (content.Content) { using var copy = new MemoryStream(); await content.Content.CopyToAsync(copy); Assert.Equal(bytes, copy.ToArray()); }
            await Assert.ThrowsAsync<HrForbiddenException>(() => viewer.DeleteAttachmentAsync(first.Id, file.Id, default));
            await Assert.ThrowsAsync<HrNotFoundException>(() => service.DownloadAsync(Guid.NewGuid(), file.Id, default));
            using (var wrong = new MemoryStream(bytes)) await Assert.ThrowsAsync<HrValidationException>(() => service.UploadAsync(first.Id, null, new("evidence.png", "image/png", bytes.Length, wrong), default));
            using (var replacement = new MemoryStream(bytes)) await service.UploadAsync(first.Id, file.Id, new("replacement.pdf", "application/pdf", bytes.Length, replacement), default);
            Assert.Equal("replacement.pdf", Assert.Single((await service.DetailsAsync(first.Id, default)).Attachments).FileName);
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            await service.DeleteAttachmentAsync(first.Id, file.Id, default); Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));

            db.ChangeTracker.Clear(); var changedVisit = await db.CollectionFieldVisits.SingleAsync(); changedVisit.Reschedule(scheduled.AddHours(1), now.AddSeconds(5)); await db.SaveChangesAsync();
            // Before the synchronizer runs, attendance already excludes the stale approval.
            Assert.Empty((await attService.GetDetailsAsync(attendance.Id, default)).ApprovedExcuses!);
            var renewed = await service.DetailsAsync(first.Id, default); Assert.Equal("PendingApproval", renewed.Status); Assert.Null(renewed.ToTime); Assert.True(renewed.SourceChanged);
            Assert.Contains(await db.HrAuditLogs.Where(a => a.Action == "VisitExcuseUpdated").ToListAsync(), a => a.OldValue!.Contains("Approved"));
            await Assert.ThrowsAsync<HrConflictException>(() => service.DecideAsync(first.Id, new(true, null, approved.UpdatedAt), default));
            await service.DecideAsync(first.Id, new(false, "Not authorized", renewed.UpdatedAt), default);
            Assert.Empty(await service.NotificationsAsync(default)); Assert.Empty((await attService.GetDetailsAsync(attendance.Id, default)).ApprovedExcuses!);

            db.ChangeTracker.Clear(); changedVisit = await db.CollectionFieldVisits.SingleAsync(); changedVisit.Reschedule(scheduled.AddDays(1), now.AddSeconds(10)); await db.SaveChangesAsync();
            var tomorrow = await service.DetailsAsync(first.Id, default); Assert.Equal(date.AddDays(1), tomorrow.Date); Assert.Equal("PendingApproval", tomorrow.Status); Assert.Empty(await service.NotificationsAsync(default));
            db.ChangeTracker.Clear(); changedVisit = await db.CollectionFieldVisits.SingleAsync(); changedVisit.Reschedule(scheduled, now.AddSeconds(10.5)); await db.SaveChangesAsync();
            Assert.Equal(first.Id, Assert.Single(await service.NotificationsAsync(default)).Id);
            Assert.Equal(date, (await service.DetailsAsync(first.Id, default)).Date);
            db.ChangeTracker.Clear(); changedVisit = await db.CollectionFieldVisits.SingleAsync(); changedVisit.Cancel("Cancelled", now.AddSeconds(11)); await db.SaveChangesAsync();
            Assert.Equal("Cancelled", (await service.DetailsAsync(first.Id, default)).Status);
            Assert.Equal(1, await db.HrExcuseMissions.CountAsync());

            var manualId = await officer.SaveManualAsync(null, new() { EmployeeId = employee.Id, Type = "MedicalExcuse", Date = date, FullDay = true, Reason = "Medical appointment" }, default);
            var manual = await service.DetailsAsync(manualId, default); Assert.Equal("Manual", manual.Source); Assert.Equal("PendingApproval", manual.Status);
            // Two independent reviewers cannot both decide the same revision.
            async Task<bool> DecideConcurrently()
            {
                await using var other = new ApplicationDbContext(options);
                try { await Service(other).DecideAsync(manualId, new(true, null, manual.UpdatedAt), default); return true; }
                catch (HrConflictException) { return false; }
            }
            var decisions = await Task.WhenAll(DecideConcurrently(), DecideConcurrently()); Assert.Single(decisions, x => x);
            Assert.Equal(1, await db.HrAuditLogs.CountAsync(a => a.EntityId == manualId && a.Action == "ExcuseApproved"));

            // A collector is linked only to an existing employee, never a copied employee.
            db.ChangeTracker.Clear();
            var secondEmployee = new Employee("EX-002", "Existing Second Employee", department.Id, true, now);
            var unlinked = new User("unlinked.test", "unlinked@test.local", "test-only", "Unlinked Collector", department.Id, now);
            var secondVisit = new FieldVisit(c.Id, unlinked.Id, scheduled, "Existing address", null, null, actor.Id, now);
            db.AddRange(secondEmployee, unlinked, secondVisit); await db.SaveChangesAsync();
            var unlinkedRequest = Assert.Single(await service.NotificationsAsync(default)); Assert.Null(unlinkedRequest.EmployeeId);
            await Assert.ThrowsAsync<HrValidationException>(() => service.DecideAsync(unlinkedRequest.Id, new(true, null, unlinkedRequest.UpdatedAt), default));
            db.ChangeTracker.Clear(); await service.LinkEmployeeAsync(unlinked.Id, secondEmployee.Id, default);
            var linked = await service.DetailsAsync(unlinkedRequest.Id, default); Assert.Equal(secondEmployee.Id, linked.EmployeeId);
            await service.DecideAsync(linked.Id, new(true, null, linked.UpdatedAt), default);
            db.ChangeTracker.Clear(); var cancelledApprovedVisit = await db.CollectionFieldVisits.SingleAsync(v => v.Id == secondVisit.Id);
            cancelledApprovedVisit.Cancel("Cancelled after approval", now.AddSeconds(12)); await db.SaveChangesAsync();
            var cancelledApproval = await service.DetailsAsync(linked.Id, default);
            Assert.Equal("Cancelled", cancelledApproval.Status); Assert.NotNull(cancelledApproval.DecisionAt);
            Assert.Equal(2, await db.Employees.CountAsync()); Assert.Equal(2, await db.CollectionFieldVisits.CountAsync());
            Assert.Equal(1, await db.AttendancePunches.CountAsync());

            // The database enforces visit uniqueness even if synchronization is bypassed.
            await using var duplicateDb = new ApplicationDbContext(options);
            duplicateDb.Add(new HrExcuseMission(employee.Id, "FieldVisitMission", date, new(11, 0), null, null, null, actor.Id, now, visit.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
            // Delete only this test's generated directory under the resolved temporary root.
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root) == name && Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    private sealed record TestUser(Guid UserId, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions) : ICurrentUserContext { public string Username => "excuse.test"; }
}
public sealed class ExcusePostgresFactAttribute : FactAttribute
{
    public ExcusePostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIS_EXCUSE_TEST_CONNECTION"))) Skip = "Set MIS_EXCUSE_TEST_CONNECTION for disposable PostgreSQL integration tests."; }
}
