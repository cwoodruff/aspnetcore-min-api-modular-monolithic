using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SharedKernel.Persistence;

/// <summary>
/// Used by dotnet-ef. Reads ConnectionStrings__AppDatabase, falling back to the docker-compose database.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ComposeConnectionString =
        "Host=localhost;Port=5432;Database=chinook;Username=chinook;Password=chinook";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceRegistration.UseAppDatabase(optionsBuilder,
            string.IsNullOrWhiteSpace(connectionString) ? ComposeConnectionString : connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
