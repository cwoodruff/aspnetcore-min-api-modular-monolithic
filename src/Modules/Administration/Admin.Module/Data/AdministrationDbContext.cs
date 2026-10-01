using Admin.Modules.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;

namespace Admin.Modules.Data;

/// <summary>
/// The Administration module's tables, all in the "administration" schema with their own migration history (ADR-0003).
/// No other module's entity appears in this model (ModuleBoundaryTests).
/// </summary>
internal sealed class AdministrationDbContext(DbContextOptions<AdministrationDbContext> options) : DbContext(options)
{
    public const string Schema = "administration";

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Employee> Employees { get; set; }

    public DbSet<Genre> Genres { get; set; }

    public DbSet<MediaType> MediaTypes { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModuleDbContextOptions.UseUtcDateTimes(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer");

            entity.Property(e => e.Address).HasColumnType("varchar(70)");
            entity.Property(e => e.City).HasColumnType("varchar(40)");
            entity.Property(e => e.Company).HasColumnType("varchar(80)");
            entity.Property(e => e.Country).HasColumnType("varchar(40)");
            entity.Property(e => e.Email).HasColumnType("varchar(60)");
            entity.Property(e => e.Fax).HasColumnType("varchar(24)");
            entity.Property(e => e.FirstName).HasColumnType("varchar(40)");
            entity.Property(e => e.LastName).HasColumnType("varchar(20)");
            entity.Property(e => e.Phone).HasColumnType("varchar(24)");
            entity.Property(e => e.PostalCode).HasColumnType("varchar(10)");
            entity.Property(e => e.State).HasColumnType("varchar(40)");

            entity.HasOne(d => d.SupportRep).WithMany(p => p.Customers).HasForeignKey(d => d.SupportRepId);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employee");

            entity.Property(e => e.Address).HasColumnType("varchar(70)");
            entity.Property(e => e.BirthDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.City).HasColumnType("varchar(40)");
            entity.Property(e => e.Country).HasColumnType("varchar(40)");
            entity.Property(e => e.Email).HasColumnType("varchar(60)");
            entity.Property(e => e.Fax).HasColumnType("varchar(24)");
            entity.Property(e => e.FirstName).HasColumnType("varchar(20)");
            entity.Property(e => e.HireDate).HasColumnType("timestamp with time zone");
            entity.Property(e => e.LastName).HasColumnType("varchar(20)");
            entity.Property(e => e.Phone).HasColumnType("varchar(24)");
            entity.Property(e => e.PostalCode).HasColumnType("varchar(10)");
            entity.Property(e => e.State).HasColumnType("varchar(40)");
            entity.Property(e => e.Title).HasColumnType("varchar(30)");

            entity.HasOne(d => d.ReportsToNavigation).WithMany(p => p.InverseReportsToNavigation)
                .HasForeignKey(d => d.ReportsTo);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("Genre");

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
        });

        modelBuilder.Entity<MediaType>(entity =>
        {
            entity.ToTable("MediaType");

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
        });
    }
}
