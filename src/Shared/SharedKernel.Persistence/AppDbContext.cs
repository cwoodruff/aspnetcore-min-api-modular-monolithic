using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
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


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Album");

            entity.Property(e => e.Title).HasColumnType("nvarchar(160)");

            entity.HasOne(d => d.Artist).WithMany(p => p.Albums).HasForeignKey(d => d.ArtistId);
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artist");

            entity.Property(e => e.Name).HasColumnType("nvarchar(120)");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer");

            entity.Property(e => e.Address).HasColumnType("nvarchar(70)");
            entity.Property(e => e.City).HasColumnType("nvarchar(40)");
            entity.Property(e => e.Company).HasColumnType("nvarchar(80)");
            entity.Property(e => e.Country).HasColumnType("nvarchar(40)");
            entity.Property(e => e.Email).HasColumnType("nvarchar(60)");
            entity.Property(e => e.Fax).HasColumnType("nvarchar(24)");
            entity.Property(e => e.FirstName).HasColumnType("nvarchar(40)");
            entity.Property(e => e.LastName).HasColumnType("nvarchar(20)");
            entity.Property(e => e.Phone).HasColumnType("nvarchar(24)");
            entity.Property(e => e.PostalCode).HasColumnType("nvarchar(10)");
            entity.Property(e => e.State).HasColumnType("nvarchar(40)");

            entity.HasOne(d => d.SupportRep).WithMany(p => p.Customers).HasForeignKey(d => d.SupportRepId);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employee");

            entity.Property(e => e.Address).HasColumnType("nvarchar(70)");
            entity.Property(e => e.BirthDate).HasColumnType("datetime");
            entity.Property(e => e.City).HasColumnType("nvarchar(40)");
            entity.Property(e => e.Country).HasColumnType("nvarchar(40)");
            entity.Property(e => e.Email).HasColumnType("nvarchar(60)");
            entity.Property(e => e.Fax).HasColumnType("nvarchar(24)");
            entity.Property(e => e.FirstName).HasColumnType("nvarchar(20)");
            entity.Property(e => e.HireDate).HasColumnType("datetime");
            entity.Property(e => e.LastName).HasColumnType("nvarchar(20)");
            entity.Property(e => e.Phone).HasColumnType("nvarchar(24)");
            entity.Property(e => e.PostalCode).HasColumnType("nvarchar(10)");
            entity.Property(e => e.State).HasColumnType("nvarchar(40)");
            entity.Property(e => e.Title).HasColumnType("nvarchar(30)");

            entity.HasOne(d => d.ReportsToNavigation).WithMany(p => p.InverseReportsToNavigation)
                .HasForeignKey(d => d.ReportsTo);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("Genre");

            entity.Property(e => e.Name).HasColumnType("nvarchar(120)");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoice");

            entity.Property(e => e.BillingAddress).HasColumnType("nvarchar(70)");
            entity.Property(e => e.BillingCity).HasColumnType("nvarchar(40)");
            entity.Property(e => e.BillingCountry).HasColumnType("nvarchar(40)");
            entity.Property(e => e.BillingPostalCode).HasColumnType("nvarchar(10)");
            entity.Property(e => e.BillingState).HasColumnType("nvarchar(40)");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.Total).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Invoices).HasForeignKey(d => d.CustomerId);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("InvoiceLine");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceLines).HasForeignKey(d => d.InvoiceId);

            entity.HasOne(d => d.Track).WithMany(p => p.InvoiceLines).HasForeignKey(d => d.TrackId);
        });

        modelBuilder.Entity<MediaType>(entity =>
        {
            entity.ToTable("MediaType");

            entity.Property(e => e.Name).HasColumnType("nvarchar(120)");
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.ToTable("Playlist");

            entity.Property(e => e.Name).HasColumnType("nvarchar(120)");
        });

        modelBuilder.Entity<PlaylistTrack>(entity =>
        {
            entity.ToTable("PlaylistTrack");

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
                    j.ToTable("PlaylistTrack");
                    j.HasKey(pt => new { pt.PlaylistId, pt.TrackId });
                });


        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("Track");

            entity.Property(e => e.Composer).HasColumnType("nvarchar(220)");
            entity.Property(e => e.Name).HasColumnType("nvarchar(200)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Album).WithMany(p => p.Tracks).HasForeignKey(d => d.AlbumId);

            entity.HasOne(d => d.Genre).WithMany(p => p.Tracks).HasForeignKey(d => d.GenreId);

            entity.HasOne(d => d.MediaType).WithMany(p => p.Tracks).HasForeignKey(d => d.MediaTypeId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
