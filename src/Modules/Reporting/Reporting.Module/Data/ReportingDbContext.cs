using Microsoft.EntityFrameworkCore;
using Reporting.Modules.Domain;
using SharedKernel.Persistence;

namespace Reporting.Modules.Data;

/// <summary>
/// Reporting's read model (ADR-0014): two keyless views over the other modules' schemas, and the one table
/// Reporting writes, its integrity findings (ADR-0015). At runtime it connects as the read-only
/// reporting_reader role.
/// </summary>
internal sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public const string Schema = "reporting";

    public DbSet<SalesByGenreRow> SalesByGenre { get; set; }

    public DbSet<InvoiceLineWithNamesRow> InvoiceLinesWithNames { get; set; }

    public DbSet<IntegrityFinding> IntegrityFindings { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModuleDbContextOptions.UseUtcDateTimes(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Views are created by raw SQL in the migration; EF maps them read-only and leaves them out of
        // migrations.
        modelBuilder.Entity<SalesByGenreRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(ReportingSql.SalesByGenreView);
        });

        modelBuilder.Entity<InvoiceLineWithNamesRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(ReportingSql.InvoiceLinesWithNamesView);
        });

        modelBuilder.Entity<IntegrityFinding>(entity =>
        {
            entity.ToTable("IntegrityFinding");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CheckName).HasColumnType("varchar(100)");
            entity.Property(e => e.SourceSchema).HasColumnType("varchar(63)");
            entity.Property(e => e.SourceTable).HasColumnType("varchar(63)");
            entity.Property(e => e.MissingReference).HasColumnType("varchar(200)");

            // At most one open finding per (check, row); a re-run adds no duplicate.
            entity.HasIndex(e => new { e.CheckName, e.SourceId })
                .IsUnique()
                .HasFilter("\"ResolvedAt\" IS NULL")
                .HasDatabaseName("IX_IntegrityFinding_Open");
        });
    }
}
