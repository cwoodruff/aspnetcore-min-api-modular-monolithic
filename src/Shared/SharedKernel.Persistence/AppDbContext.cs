using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Example of simple conventions (can be expanded later)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Ensure all DateTime properties are treated as UTC in your application logic
            // (SQLite provider stores as TEXT/INTEGER; enforcement is at app logic level.)
            // Add common conventions here if needed.
        }
    }
}
