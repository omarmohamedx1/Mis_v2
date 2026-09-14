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

public sealed class SocialInsuranceTests
{
    private static SocialInsuranceRecord Record() => new(Guid.NewGuid(), "١٢٣٤", new DateOnly(2026, 1, 1), 5000, "Insured", null, null, null);
    [Fact]
    public void Ending_preserves_identity_and_values_and_prevents_reopening()
    {
        var r = Record(); var id = r.Id; var employee = r.EmployeeId;
        Assert.Equal("1234", r.SocialInsuranceNumber);
        Assert.Throws<ArgumentException>(() => r.End(new DateOnly(2025, 12, 31)));
        r.End(new DateOnly(2026, 2, 1));
        Assert.Equal(id, r.Id); Assert.Equal(employee, r.EmployeeId); Assert.Equal(5000, r.InsurableSalary);
        Assert.Equal("Ended", r.InsuranceStatus);
        Assert.Throws<InvalidOperationException>(() => r.Update("1234", new DateOnly(2026, 1, 1), 5000, "Insured", null, null, null));
        Assert.Throws<InvalidOperationException>(() => r.End(new DateOnly(2026, 3, 1)));
    }
    [Theory]
    [InlineData("Insured", 0, true)]
    [InlineData("Insured", -1, true)]
    [InlineData("Insured", 100, false)]
    [InlineData("Suspended", 100, false)]
    [InlineData("Ended", 100, true)]
    [InlineData("Invalid", 100, true)]
    public void Rejects_invalid_salary_start_and_status(string status, decimal salary, bool start)
        => Assert.Throws<ArgumentException>(() => new SocialInsuranceRecord(Guid.NewGuid(), "123", start ? new DateOnly(2026, 1, 1) : null, salary, status, null, null, null));

    [SocialInsurancePostgresFact]
    public async Task Postgres_lifecycle_filters_uniqueness_authorization_and_audit()
    {
        // Every row lives in a uniquely named disposable database, never the configured application database.
        var source = Environment.GetEnvironmentVariable("MIS_SOCIAL_TEST_CONNECTION")!;
        var name = "mis_insurance_test_" + Guid.NewGuid().ToString("N");
        var adminConnection = new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(adminConnection.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        try {
            var connection = new NpgsqlConnectionStringBuilder(source) { Database = name, Pooling = false };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            var department = new Department("Test HR", "TESTHR", DateTimeOffset.UtcNow);
            var e1 = new Employee("SI-001", "Insurance One", department.Id, true, DateTimeOffset.UtcNow);
            var e2 = new Employee("SI-002", "Insurance Two", department.Id, true, DateTimeOffset.UtcNow);
            var actor = new User("insurance.test", "insurance@test.local", "test-only", "Insurance Tester", department.Id, DateTimeOffset.UtcNow);
            db.AddRange(department, e1, e2, actor); await db.SaveChangesAsync();
            var user = new TestUser(actor.Id, ["HrManager"], []);
            var service = new SocialInsuranceService(db, user, new HrAuditService(db, user));
            var initial = await service.ListAsync(null, department.Id, null, null, 1, 20, default);
            Assert.Equal(2, initial.NotInsured); Assert.All(initial.Items, e => Assert.Null(e.Record));
            var request = new SaveSocialInsuranceRequest(e1.Id, "١٢٣٤", new DateOnly(2026, 1, 1), 5000, "Insured", "Office", "Form", "Test");
            var saved = await service.SaveAsync(null, request, default);
            Assert.Equal("1234", saved.SocialInsuranceNumber);
            Assert.Equal(1, (await service.ListAsync("1234", department.Id, "Insured", null, 1, 20, default)).TotalCount);
            Assert.Equal(1, (await service.ListAsync("SI-002", null, "NotInsured", null, 1, 20, default)).TotalCount);
            Assert.Equal(1, (await service.ListAsync("Insurance One", null, null, null, 1, 20, default)).TotalCount);
            await Assert.ThrowsAsync<HrConflictException>(() => service.SaveAsync(null, request with { SocialInsuranceNumber = "9876" }, default)); db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<HrConflictException>(() => service.SaveAsync(null, request with { EmployeeId = e2.Id, SocialInsuranceNumber = "1234" }, default)); db.ChangeTracker.Clear();
            var edited = await service.SaveAsync(saved.Id, request with { InsurableSalary = 6000, InsuranceStatus = "Suspended" }, default);
            Assert.Equal(6000, edited.InsurableSalary);
            await Assert.ThrowsAsync<HrValidationException>(() => service.EndAsync(saved.Id, new(new DateOnly(2025, 1, 1)), default));
            await service.EndAsync(saved.Id, new(new DateOnly(2026, 2, 1)), default);
            await service.SaveAsync(null, request with { InsuranceStartDate = new DateOnly(2026, 3, 1) }, default);
            var history = await service.HistoryAsync(e1.Id, default);
            Assert.Equal(2, history.Count); Assert.Single(history, r => r.InsuranceStatus == "Ended");
            Assert.Equal(2, await db.Employees.CountAsync());
            Assert.Equal(4, await db.HrAuditLogs.CountAsync(a => a.EntityType == "SocialInsuranceRecord"));
            var summary = await service.ListAsync(null, null, null, null, 1, 20, default);
            Assert.Equal(1, summary.TotalInsured); Assert.Equal(1, summary.NotInsured); Assert.Equal(1, summary.EndedRecords);
            var viewer = new TestUser(actor.Id, [], ["hr.social_insurance.view"]);
            var readOnly = new SocialInsuranceService(db, viewer, new HrAuditService(db, viewer));
            Assert.False((await readOnly.ListAsync(null, null, null, null, 1, 20, default)).CanManage);
            await Assert.ThrowsAsync<HrForbiddenException>(() => readOnly.SaveAsync(null, request, default));
            await Assert.ThrowsAsync<HrForbiddenException>(() => readOnly.EndAsync(saved.Id, new(new DateOnly(2026, 2, 1)), default));
            var denied = new TestUser(actor.Id, [], []);
            await Assert.ThrowsAsync<HrForbiddenException>(() => new SocialInsuranceService(db, denied, new HrAuditService(db, denied)).HistoryAsync(e1.Id, default));
            var importer = new SocialInsuranceImportService(db, new ImportStorage(), new HrAuditService(db, user), user, null!);
            using var csv = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Code,Insurance No,Start,Salary,Status\nSI-002,5678,2026-01-01,5000,Insured\nUNKNOWN,8888,2026-01-01,5000,Insured\nSI-001,9999,2026-01-01,5000,Insured\nSI-002,7777,2026-01-01,5000,Insured\n"));
            var uploaded = await importer.UploadAsync(new HrUploadFile("insurance.csv", "text/csv", csv.Length, csv), default);
            Assert.Equal(2, await db.SocialInsuranceRecords.CountAsync());
            await Assert.ThrowsAsync<HrValidationException>(() => importer.ConfirmAsync(uploaded.Id, Guid.NewGuid(), default));
            var mapping = new SocialInsuranceImportMapping { Columns = new() { ["EmployeeNumber"] = "Code", ["SocialInsuranceNumber"] = "Insurance No", ["InsuranceStartDate"] = "Start", ["InsurableSalary"] = "Salary", ["InsuranceStatus"] = "Status" } };
            var preview = await importer.PreviewAsync(uploaded.Id, mapping, default);
            Assert.Equal(new[] { "Ready", "Error", "Existing", "Existing" }, preview.Rows.Select(r => r.Status));
            Assert.Equal(2, await db.SocialInsuranceRecords.CountAsync());
            var imported = await importer.ConfirmAsync(uploaded.Id, preview.PreviewId, default);
            Assert.Equal(new SocialInsuranceImportResult(1, 3, 0), imported);
            Assert.Equal(imported, await importer.ConfirmAsync(uploaded.Id, preview.PreviewId, default));
            Assert.Equal(2, await db.Employees.CountAsync());
            Assert.Equal(3, await db.SocialInsuranceRecords.CountAsync());
            Assert.Equal(1, (await service.ListAsync("5678", null, "Insured", null, 1, 20, default)).TotalCount);
            Assert.Equal(imported, Assert.Single(await importer.HistoryAsync(default)).Result);
        } finally {
            // The generated identifier is the only database this test can drop.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed record TestUser(Guid UserId, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions) : ICurrentUserContext { public string Username => "insurance.test"; }
    private sealed class ImportStorage : IHrFileStorage
    {
        private readonly Dictionary<string, byte[]> files = [];
        public async Task<StoredFileDto> SaveAsync(string scope, string originalFileName, string contentType, Stream content, long maximumBytes, CancellationToken ct)
        {
            using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, ct);
            var key = Guid.NewGuid().ToString(); files[key] = buffer.ToArray();
            return new StoredFileDto(key, originalFileName, contentType, buffer.Length, "test");
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream(files[key]));
        public Task DeleteAsync(string key, CancellationToken ct) { files.Remove(key); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(files.ContainsKey(key));
    }
}

public sealed class SocialInsurancePostgresFactAttribute : FactAttribute
{
    public SocialInsurancePostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIS_SOCIAL_TEST_CONNECTION"))) Skip = "Set MIS_SOCIAL_TEST_CONNECTION to run against a disposable PostgreSQL database."; }
}
