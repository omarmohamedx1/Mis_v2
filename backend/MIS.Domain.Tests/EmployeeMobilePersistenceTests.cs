using Microsoft.EntityFrameworkCore;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Persistence.Repositories;
using Npgsql;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class EmployeeMobilePersistenceTests
{
    [EmployeeMobilePostgresFact]
    public async Task Mobile_roundtrips_in_details_and_is_searchable_without_changing_other_contacts()
    {
        var source = Environment.GetEnvironmentVariable("MIS_MOBILE_TEST_CONNECTION")!;
        var name = "mis_mobile_test_" + Guid.NewGuid().ToString("N");
        var settings = new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(settings.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            settings.Database = name;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(settings.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var department = new Department("Mobile Test", "MOBILE", DateTimeOffset.UtcNow);
            var employee = new Employee("M-001", "Mobile Test Employee", department.Id, true, DateTimeOffset.UtcNow);
            employee.SetNationalId("12345678901234", DateTimeOffset.UtcNow);
            employee.UpdateContactInformation("01012345678", "01123456789", "test@example.com", "Cairo", "Cairo", DateTimeOffset.UtcNow);
            db.AddRange(department, employee); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var repository = new HrEmployeeRepository(db);
            Assert.Equal("01012345678", (await repository.GetDetailsByIdAsync(employee.Id, default))!.MobileNumber);
            foreach (var search in new[] { "01012345678", "M-001", "Mobile Test Employee", "12345678901234" })
                Assert.Equal(1, (await repository.GetPagedAsync(1, 20, search, null, null, default)).TotalCount);
            var tracked = (await repository.GetTrackedByIdAsync(employee.Id, default))!;
            tracked.UpdateContactInformation("+201012345678", tracked.AlternativeMobileNumber, tracked.Email, tracked.Address, tracked.City, DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync(default); db.ChangeTracker.Clear();
            Assert.Equal("+201012345678", (await repository.GetDetailsByIdAsync(employee.Id, default))!.MobileNumber);
            Assert.Equal(1, (await repository.GetPagedAsync(1, 20, "+201012345678", null, null, default)).TotalCount);
            Assert.Equal("test@example.com", (await repository.GetTrackedByIdAsync(employee.Id, default))!.Email);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}

public sealed class EmployeeMobilePostgresFactAttribute : FactAttribute
{
    public EmployeeMobilePostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIS_MOBILE_TEST_CONNECTION")))
            Skip = "Set MIS_MOBILE_TEST_CONNECTION to run against a disposable PostgreSQL database.";
    }
}
