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

    public DbSet<Track> Tracks { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Album");

            entity.Property(e => e.Title).HasColumnType("nvarchar(160)");

            entity.HasOne(d => d.Artist).WithMany(p => p.Albums).HasForeignKey(d => d.ArtistId);

            entity.HasMany(e => e.Tracks).WithOne(p => p.Album).HasForeignKey(p => p.AlbumId);
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

            entity.HasOne(d => d.SupportRep).WithMany(p => p.Customers)
                .HasForeignKey(d => d.SupportRepId);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employee");

            entity.Property(e => e.Address).HasColumnType("nvarchar(70)");
            entity.Property(e => e.BirthDate).HasConversion<DateTime>();
            entity.Property(e => e.City).HasColumnType("nvarchar(40)");
            entity.Property(e => e.Country).HasColumnType("nvarchar(40)");
            entity.Property(e => e.Email).HasColumnType("nvarchar(60)");
            entity.Property(e => e.Fax).HasColumnType("nvarchar(24)");
            entity.Property(e => e.FirstName).HasColumnType("nvarchar(20)");
            entity.Property(e => e.HireDate).HasConversion<DateTime>();
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
            entity.Property(e => e.InvoiceDate).HasConversion<DateTime>();
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

            entity.HasMany(d => d.Tracks).WithMany(p => p.Playlists)
                .UsingEntity<Dictionary<string, object>>(
                    "PlaylistTrack",
                    r => r.HasOne<Track>().WithMany()
                        .HasForeignKey("TrackId")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    l => l.HasOne<Playlist>().WithMany()
                        .HasForeignKey("PlaylistId")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    j => { j.ToTable("PlaylistTrack"); });
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

    // Compiled Queries

    // Album Methods
    public Task<IAsyncEnumerable<Album>> GetAllAlbums() => Task.FromResult(_queryGetAllAlbums(this));

    public Task<Album?> GetAlbum(int id) => _queryGetAlbum(this, id);

    public Task<IAsyncEnumerable<Album>> GetAlbumsByArtistId(int id) => Task.FromResult(_queryGetAlbumsByArtistId(this, id));

    // Artist Methods
    public Task<IAsyncEnumerable<Artist>> GetAllArtists() => Task.FromResult(_queryGetAllArtists(this));

    public Task<Artist?> GetArtist(int id) => _queryGetArtist(this, id);

    // Customer Methods
    public Task<IAsyncEnumerable<Customer>> GetAllCustomers() => Task.FromResult(_queryGetAllCustomers(this));

    public Task<Customer?> GetCustomer(int id) => _queryGetCustomer(this, id);

    public Task<IAsyncEnumerable<Customer>> GetCustomerBySupportRepId(int id) => Task.FromResult(_queryGetCustomerBySupportRepId(this, id));

    // Employee Methods
    public Task<IAsyncEnumerable<Employee>> GetAllEmployees() => Task.FromResult(_queryGetAllEmployees(this));

    public Task<Employee?> GetEmployee(int id) => _queryGetEmployee(this, id);

    public Task<IAsyncEnumerable<Employee>> GetEmployeeDirectReports(int id) => Task.FromResult(_queryGetDirectReports(this, id));

    public Task<Employee> GetEmployeeGetReportsTo(int id) => _queryGetReportsTo(this, id);

    // Genre Methods
    public IAsyncEnumerable<Genre> GetAllGenres() => _queryGetAllGenres(this);

    public Task<Genre?> GetGenre(int id) => _queryGetGenre(this, id);

    // InvoiceLine Methods
    public IAsyncEnumerable<InvoiceLine> GetAllInvoiceLines() => _queryGetAllInvoiceLines(this);

    public Task<InvoiceLine?> GetInvoiceLine(int id) => _queryGetInvoiceLine(this, id);

    public IAsyncEnumerable<InvoiceLine> GetInvoiceLinesByInvoiceId(int id) =>
        _queryGetInvoiceLinesByInvoiceId(this, id);

    public IAsyncEnumerable<InvoiceLine> GetInvoiceLinesByTrackId(int id) => _queryGetInvoiceLinesByTrackId(this, id);

    // Invoice Methods
    public IAsyncEnumerable<Invoice> GetAllInvoices() => _queryGetAllInvoices(this);

    public Task<Invoice?> GetInvoice(int id) => _queryGetInvoice(this, id);

    public IAsyncEnumerable<Invoice> GetInvoicesByEmployeeId(int id) => _queryGetInvoicesByEmployeeId(this, id);

    public IAsyncEnumerable<Invoice> GetInvoicesByCustomerId(int id) => _queryGetInvoicesByCustomerId(this, id);

    // MediaType Methods
    public IAsyncEnumerable<MediaType> GetAllMediaTypes() => _queryGetAllMediaTypes(this);

    public Task<MediaType?> GetMediaType(int id) => _queryGetMediaType(this, id);

    // Playlist Methods
    public IAsyncEnumerable<Playlist> GetAllPlaylists() => _queryGetAllPlaylists(this);

    public Task<Playlist?> GetPlaylist(int id) => _queryGetPlaylist(this, id);


    public IAsyncEnumerable<Track> GetAllTracks() => _queryGetAllTracks(this);

    public Task<Track?> GetTrack(int id) => _queryGetTrack(this, id);

    public IAsyncEnumerable<Track> GetTracksByAlbumId(int id) => _queryGetTracksByAlbumId(this, id);

    public IAsyncEnumerable<Track> GetTracksByGenreId(int id) => _queryGetTracksByGenreId(this, id);

    public IAsyncEnumerable<Track> GetTracksByMediaTypeId(int id) => _queryGetTracksByMediaTypeId(this, id);

    public IAsyncEnumerable<Track> GetTracksByArtistId(int id) => _queryGetTracksByArtistId(this, id);

    public IAsyncEnumerable<Track> GetTracksByInvoiceId(int id) => _queryGetTracksByInvoiceId(this, id);

    public IAsyncEnumerable<Track> GetTracksByPlaylistId(int id) => _queryGetTracksByPlaylistId(this, id);

    // Delegates

    // Album Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Album>> _queryGetAllAlbums =
        EF.CompileAsyncQuery((AppDbContext db) => db.Albums
            .AsNoTracking()
            .Include(a => a.Artist));

    private static readonly Func<AppDbContext, int, Task<Album?>> _queryGetAlbum =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Albums
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking()
            .FirstOrDefault(a => a.Id == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Album>> _queryGetAlbumsByArtistId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Albums.Where(a => a.ArtistId == id)
            .Include(a => a.Artist)
            .Include(a => a.Tracks)
            .AsNoTracking());

    // Artist Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Artist>> _queryGetAllArtists =
        EF.CompileAsyncQuery((AppDbContext db) => db.Artists.AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<Artist?>> _queryGetArtist =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Artists
            .Include(a => a.Albums)
            .ThenInclude(a => a.Tracks)
            .AsNoTracking()
            .FirstOrDefault(a => a.Id == id));

    // Customer Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Customer>> _queryGetAllCustomers =
        EF.CompileAsyncQuery((AppDbContext db) => db.Customers.AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<Customer?>> _queryGetCustomer =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Customers
            .Include(c => c.InverseSupportRep)
            .Include(c => c.Invoices)
            .ThenInclude(i => i.InvoiceLines)
            .Include(c => c.SupportRep)
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Customer>> _queryGetCustomerBySupportRepId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Customers
            .Include(c => c.InverseSupportRep)
            .Include(c => c.Invoices)
            .ThenInclude(i => i.InvoiceLines)
            .Include(c => c.SupportRep)
            .AsNoTracking()
            .Where(a => a.SupportRepId == id));

    // Employee Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Employee>> _queryGetAllEmployees =
        EF.CompileAsyncQuery((AppDbContext db) => db.Employees.AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<Employee?>> _queryGetEmployee =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Employees
            .Include(e => e.Customers)
            .Include(e => e.InverseReportsToNavigation)
            .Include(e => e.ReportsToNavigation)
            .AsNoTracking()
            .FirstOrDefault(e => e.Id == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Employee>> _queryGetDirectReports =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Employees
            .Include(e => e.Customers)
            .Include(e => e.InverseReportsToNavigation)
            .Include(e => e.ReportsToNavigation)
            .AsNoTracking()
            .Where(e => e.ReportsTo == id));

    private static readonly Func<AppDbContext, int, Task<Employee>> _queryGetReportsTo =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Employees
            .Include(e => e.Customers)
            .AsNoTracking()
            .First(e => e.ReportsTo == id));

    // Genre Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Genre>> _queryGetAllGenres =
        EF.CompileAsyncQuery((AppDbContext db) => db.Genres
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<Genre?>> _queryGetGenre =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Genres
            .Include(g => g.Tracks)
            .AsNoTracking()
            .FirstOrDefault(g => g.Id == id));

    // InvoiceLine Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<InvoiceLine>> _queryGetAllInvoiceLines =
        EF.CompileAsyncQuery((AppDbContext db) => db.InvoiceLines
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<InvoiceLine?>> _queryGetInvoiceLine =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.InvoiceLines
            .Include(i => i.Track)
            .AsNoTracking()
            .FirstOrDefault(i => i.Id == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<InvoiceLine>> _queryGetInvoiceLinesByInvoiceId
        = EF.CompileAsyncQuery((AppDbContext db, int id) => db.InvoiceLines
            .Include(i => i.Track)
            .AsNoTracking()
            .Where(a => a.InvoiceId == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<InvoiceLine>> _queryGetInvoiceLinesByTrackId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.InvoiceLines
            .AsNoTracking()
            .Where(a => a.TrackId == id));

    // Invoice Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Invoice>> _queryGetAllInvoices =
        EF.CompileAsyncQuery((AppDbContext db) => db.Invoices
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<Invoice?>> _queryGetInvoice =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Invoices
            .Include(i => i.Customer)
            .ThenInclude(i => i!.SupportRep)
            .Include(i => i.InvoiceLines)
            .ThenInclude(il => il.Track)
            .AsNoTracking()
            .FirstOrDefault(i => i.Id == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Invoice>> _queryGetInvoicesByCustomerId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.InvoiceLines)
            .ThenInclude(il => il.Track)
            .AsNoTracking()
            .Where(a => a.CustomerId == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Invoice>> _queryGetInvoicesByEmployeeId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Customers
            .Where(a => a.SupportRepId == id)
            .SelectMany(t => t.Invoices)
            .Include(i => i.InvoiceLines)
            .ThenInclude(il => il.Track)
            .AsNoTracking());

    // MediaType Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<MediaType>> _queryGetAllMediaTypes =
        EF.CompileAsyncQuery((AppDbContext db) => db.MediaTypes
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<MediaType?>> _queryGetMediaType =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.MediaTypes
            .Include(m => m.Tracks)
            .AsNoTracking()
            .FirstOrDefault(m => m.Id == id));

    // Playlist Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Playlist>> _queryGetAllPlaylists =
        EF.CompileAsyncQuery((AppDbContext db) => db.Playlists
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<Playlist?>> _queryGetPlaylist =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Playlists
            .Include(p => p.Tracks)
            .AsNoTracking()
            .FirstOrDefault(p => p.Id == id));

    // Track Queries
    private static readonly Func<AppDbContext, IAsyncEnumerable<Track>> _queryGetAllTracks =
        EF.CompileAsyncQuery((AppDbContext db) => db.Tracks
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, Task<Track?>> _queryGetTrack =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Tracks
            .Include(t => t.Album)
            .Include(t => t.Genre)
            .Include(t => t.MediaType)
            .AsNoTracking()
            .FirstOrDefault(t => t.Id == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Track>> _queryGetTracksByAlbumId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Tracks
            .Include(t => t.Genre)
            .Include(t => t.MediaType)
            .AsNoTracking()
            .Where(a => a.AlbumId == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Track>> _queryGetTracksByGenreId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Tracks
            .Include(t => t.Album)
            .Include(t => t.MediaType)
            .AsNoTracking()
            .Where(a => a.GenreId == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Track>> _queryGetTracksByMediaTypeId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Tracks
            .Include(t => t.Album)
            .Include(t => t.Genre)
            .AsNoTracking()
            .Where(a => a.MediaTypeId == id));

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Track>> _queryGetTracksByArtistId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Albums
            .Where(a => a.ArtistId == id)
            .SelectMany(t => t.Tracks)
            .Include(t => t.Album)
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Track>> _queryGetTracksByInvoiceId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Tracks
            .Where(c => c.InvoiceLines.Any(o => o.InvoiceId == id))
            .Include(t => t.Album)
            .AsNoTracking());

    private static readonly Func<AppDbContext, int, IAsyncEnumerable<Track>> _queryGetTracksByPlaylistId =
        EF.CompileAsyncQuery((AppDbContext db, int id) => db.Playlists
            .Where(a => a.Id == id)
            .SelectMany(t => t.Tracks)
            .Include(t => t.Album)
            .AsNoTracking());
}
