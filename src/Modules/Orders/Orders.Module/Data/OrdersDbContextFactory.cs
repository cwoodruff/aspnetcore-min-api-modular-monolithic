using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace Orders.Modules.Data;

/// <summary>
/// Used by dotnet-ef. Reads ConnectionStrings__AppDatabase, falling back to the docker-compose database.
/// </summary>
internal sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    private const string ComposeConnectionString =
        "Host=localhost;Port=5432;Database=chinook;Username=chinook;Password=chinook";

    public OrdersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AppDatabase");
        var options = new DbContextOptionsBuilder<OrdersDbContext>();
        ModuleDbContextOptions.Use(options,
            string.IsNullOrWhiteSpace(connectionString) ? ComposeConnectionString : connectionString,
            OrdersDbContext.Schema);
        return new OrdersDbContext(options.Options);
    }
}
