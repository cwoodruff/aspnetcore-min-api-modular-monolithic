using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace Admin.Modules.Data;

/// <summary>
/// Used by dotnet-ef. Reads ConnectionStrings__AppDatabase, falling back to the docker-compose database.
/// </summary>
internal sealed class AdministrationDbContextFactory : IDesignTimeDbContextFactory<AdministrationDbContext>
{
    private const string ComposeConnectionString =
        "Host=localhost;Port=5432;Database=chinook;Username=chinook;Password=chinook";

    public AdministrationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        var options = new DbContextOptionsBuilder<AdministrationDbContext>();
        ModuleDbContextOptions.Use(options,
            string.IsNullOrWhiteSpace(connectionString) ? ComposeConnectionString : connectionString,
            AdministrationDbContext.Schema);
        return new AdministrationDbContext(options.Options);
    }
}
