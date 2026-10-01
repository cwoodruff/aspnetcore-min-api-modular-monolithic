using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace Catalog.Modules.Data;

/// <summary>
/// Used by dotnet-ef. Reads ConnectionStrings__AppDatabase, falling back to the docker-compose database.
/// </summary>
internal sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    private const string ComposeConnectionString =
        "Host=localhost;Port=5432;Database=chinook;Username=chinook;Password=chinook";

    public CatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        var options = new DbContextOptionsBuilder<CatalogDbContext>();
        ModuleDbContextOptions.Use(options,
            string.IsNullOrWhiteSpace(connectionString) ? ComposeConnectionString : connectionString,
            CatalogDbContext.Schema);
        return new CatalogDbContext(options.Options);
    }
}
