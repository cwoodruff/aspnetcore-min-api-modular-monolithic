using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    /// <summary>
    /// Each table already lives in the schema of the module that will own it (ADR-0001), so splitting
    /// the context per module in phase 2 moves code, not data.
    /// </summary>
    public static class Schemas
    {
        public const string Catalog = "catalog";
        public const string Orders = "orders";
        public const string Administration = "administration";

        public static readonly string[] All = [Catalog, Orders, Administration];
    }

    public DbSet<Album> Albums { get; set; }

    public DbSet<Artist> Artists { get; set; }

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Employee> Employees { get; set; }

    public DbSet<Genre> Genres { get; set; }

    public DbSet<Invoice> Invoices { get; set; }

    public DbSet<InvoiceLine> InvoiceLines { get; set; }

    public DbSet<MediaType> MediaTypes { get; set; }

    public DbSet<Playlist> Playlists { get; set; }

    public DbSet<PlaylistTrack> PlaylistTracks { get; set; }

    public DbSet<Track> Tracks { get; set; }


    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Npgsql only writes UTC values to timestamp with time zone. API payloads such as
        // "InvoiceDate": "2024-01-01" bind as DateTimeKind.Unspecified; treat those as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Album", Schemas.Catalog);

            entity.Property(e => e.Title).HasColumnType("varchar(160)");

            entity.HasOne(d => d.Artist).WithMany(p => p.Albums).HasForeignKey(d => d.ArtistId);
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artist", Schemas.Catalog);

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer", Schemas.Administration);

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
            entity.ToTable("Employee", Schemas.Administration);

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
            entity.ToTable("Genre", Schemas.Administration);

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoice", Schemas.Orders);

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
            entity.ToTable("InvoiceLine", Schemas.Orders);

            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10,2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceLines).HasForeignKey(d => d.InvoiceId);

            // Cross-module reference (Catalog): an id only, indexed for lookups, no FK.
            entity.HasIndex(e => e.TrackId);
        });

        modelBuilder.Entity<MediaType>(entity =>
        {
            entity.ToTable("MediaType", Schemas.Administration);

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.ToTable("Playlist", Schemas.Catalog);

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
        });

        modelBuilder.Entity<PlaylistTrack>(entity =>
        {
            entity.ToTable("PlaylistTrack", Schemas.Catalog);

            entity.HasKey(e => new { e.PlaylistId, e.TrackId });

            entity.HasOne(d => d.Playlist)
                .WithMany(p => p.PlaylistTracks)
                .HasForeignKey(d => d.PlaylistId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Track)
                .WithMany(t => t.PlaylistTracks)
                .HasForeignKey(d => d.TrackId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        // Configure many-to-many skip navigations between Playlist and Track using PlaylistTrack as join
        modelBuilder.Entity<Playlist>()
            .HasMany(p => p.Tracks)
            .WithMany(t => t.Playlists)
            .UsingEntity<PlaylistTrack>(
                j => j
                    .HasOne(pt => pt.Track)
                    .WithMany(t => t.PlaylistTracks)
                    .HasForeignKey(pt => pt.TrackId),
                j => j
                    .HasOne(pt => pt.Playlist)
                    .WithMany(p => p.PlaylistTracks)
                    .HasForeignKey(pt => pt.PlaylistId),
                j =>
                {
                    j.ToTable("PlaylistTrack", Schemas.Catalog);
                    j.HasKey(pt => new { pt.PlaylistId, pt.TrackId });
                });


        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("Track", Schemas.Catalog);

            entity.Property(e => e.Composer).HasColumnType("varchar(220)");
            entity.Property(e => e.Name).HasColumnType("varchar(200)");
            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10,2)");

            entity.HasOne(d => d.Album).WithMany(p => p.Tracks).HasForeignKey(d => d.AlbumId);

            // Cross-module references (Administration): ids only, indexed for lookups, no FKs.
            entity.HasIndex(e => e.GenreId);
            entity.HasIndex(e => e.MediaTypeId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Utc ? value
        : value.Kind == DateTimeKind.Local ? value.ToUniversalTime()
        : DateTime.SpecifyKind(value, DateTimeKind.Utc),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
