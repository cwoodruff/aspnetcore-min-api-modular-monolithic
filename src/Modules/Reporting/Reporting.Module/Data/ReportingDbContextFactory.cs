using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace Reporting.Modules.Data;

/// <summary>
/// Used by dotnet-ef. Migrations run as the schema owner (ConnectionStrings__AppDatabase, falling back to
/// the docker-compose database), never as the read-only reporting role.
/// </summary>
internal sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    private const string ComposeConnectionString =
        "Host=localhost;Port=5432;Database=chinook;Username=chinook;Password=chinook";

    public ReportingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        var options = new DbContextOptionsBuilder<ReportingDbContext>();
        ModuleDbContextOptions.Use(options,
            string.IsNullOrWhiteSpace(connectionString) ? ComposeConnectionString : connectionString,
            ReportingDbContext.Schema);
        return new ReportingDbContext(options.Options);
    }
}
