using Microsoft.EntityFrameworkCore;
using MIS.Infrastructure.Persistence;
using MIS.Infrastructure.Persistence.Repositories;
using System.Globalization;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class HrEmployeeListQueryTests
{
    [LocalEmployeeDatabaseFact]
    public async Task Employee_directory_query_executes_with_enterprise_list_projection()
    {
        var connection = Environment.GetEnvironmentVariable("MIS_EMPLOYEE_LIST_TEST_CONNECTION")!;
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var repository = new HrEmployeeRepository(db);
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-EG");
            var result = await repository.GetPagedByStatusAsync(
                1, 100, null, "identity", null, null, null, false, null, null, null, default);
            Assert.True(result.TotalCount >= result.Items.Count);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}

public sealed class LocalEmployeeDatabaseFactAttribute : FactAttribute
{
    public LocalEmployeeDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIS_EMPLOYEE_LIST_TEST_CONNECTION")))
            Skip = "Set MIS_EMPLOYEE_LIST_TEST_CONNECTION to run the employee list query against PostgreSQL.";
    }
}
