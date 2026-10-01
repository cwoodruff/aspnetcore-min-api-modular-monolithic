using Orders.Modules.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;

namespace Orders.Modules.Data;

/// <summary>
/// The Orders module's tables, all in the "orders" schema with their own migration history (ADR-0003).
/// No other module's entity appears in this model (ModuleBoundaryTests).
/// </summary>
internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string Schema = "orders";

    public DbSet<Invoice> Invoices { get; set; }

    public DbSet<InvoiceLine> InvoiceLines { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModuleDbContextOptions.UseUtcDateTimes(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoice");

            entity.Property(e => e.BillingAddress).HasColumnType("varchar(70)");
            entity.Property(e => e.BillingCity).HasColumnType("varchar(40)");
            entity.Property(e => e.BillingCountry).HasColumnType("varchar(40)");
            entity.Property(e => e.BillingPostalCode).HasColumnType("varchar(10)");
            entity.Property(e => e.BillingState).HasColumnType("varchar(40)");
            entity.Property(e => e.InvoiceDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.Total).HasColumnType("numeric(10,2)");

            // Cross-module reference (Administration): an id only, indexed for lookups, no FK.
            entity.HasIndex(e => e.CustomerId);

        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("InvoiceLine");

            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10,2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceLines).HasForeignKey(d => d.InvoiceId);

            // Cross-module reference (Catalog): an id only, indexed for lookups, no FK.
            entity.HasIndex(e => e.TrackId);
        });
    }
}
