using Microsoft.EntityFrameworkCore;
using MIS.Infrastructure.Persistence;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public void Database_model_has_a_matching_migration_snapshot()
    {
        // Model comparison only: no connection is opened and no database is changed.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=ModelCheck;Username=modelcheck;Password=unused")
            .Options;
        using var db = new ApplicationDbContext(options);

        Assert.False(db.Database.HasPendingModelChanges(),
            "Add a migration before publishing: pending model changes prevent startup migrations.");
    }
}
