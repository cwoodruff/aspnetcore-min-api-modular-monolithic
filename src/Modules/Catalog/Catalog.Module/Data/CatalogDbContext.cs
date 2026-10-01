using Catalog.Modules.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;

namespace Catalog.Modules.Data;

/// <summary>
/// The Catalog module's tables, all in the "catalog" schema with their own migration history (ADR-0003).
/// No other module's entity appears in this model (ModuleBoundaryTests).
/// </summary>
internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Album> Albums { get; set; }

    public DbSet<Artist> Artists { get; set; }

    public DbSet<Playlist> Playlists { get; set; }

    public DbSet<PlaylistTrack> PlaylistTracks { get; set; }

    public DbSet<Track> Tracks { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModuleDbContextOptions.UseUtcDateTimes(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Album");

            entity.Property(e => e.Title).HasColumnType("varchar(160)");

            entity.HasOne(d => d.Artist).WithMany(p => p.Albums).HasForeignKey(d => d.ArtistId);
        });

        modelBuilder.Entity<Artist>(entity =>
        {
            entity.ToTable("Artist");

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
        });

        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.ToTable("Playlist");

            entity.Property(e => e.Name).HasColumnType("varchar(120)");
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

            entity.Property(e => e.Composer).HasColumnType("varchar(220)");
            entity.Property(e => e.Name).HasColumnType("varchar(200)");
            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10,2)");

            entity.HasOne(d => d.Album).WithMany(p => p.Tracks).HasForeignKey(d => d.AlbumId);

            // Cross-module references (Administration): ids only, indexed for lookups, no FKs.
            entity.HasIndex(e => e.GenreId);
            entity.HasIndex(e => e.MediaTypeId);
        });
    }
}
