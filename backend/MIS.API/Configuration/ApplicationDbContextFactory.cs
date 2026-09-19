using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MIS.Infrastructure.Persistence;

namespace MIS.API.Configuration;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Development;
        var apiDirectory = ResolveApiDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["MIS_DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connectionString) || !connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DefaultConnection with a password using MIS.API user secrets, " +
                "ConnectionStrings__DefaultConnection, or MIS_DB_CONNECTION before running EF Core commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDirectory, "appsettings.json")) &&
            string.Equals(Path.GetFileName(currentDirectory), "MIS.API", StringComparison.OrdinalIgnoreCase))
            return currentDirectory;

        var nestedApiDirectory = Path.Combine(currentDirectory, "MIS.API");
        if (File.Exists(Path.Combine(nestedApiDirectory, "appsettings.json")))
            return nestedApiDirectory;

        throw new InvalidOperationException(
            "Could not locate the MIS.API configuration directory. Run EF commands from backend or backend/MIS.API.");
    }
}
