using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence;
using SharedKernel.Persistence.Entities;

namespace ModularMonolith.Api.Tests;

public static class TestDbHelpers
{
    private static readonly IServiceProvider SharedProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    public static AppDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .UseInternalServiceProvider(SharedProvider)
            .EnableSensitiveDataLogging()
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public static void SeedMinimalGraph(AppDbContext db)
    {
        // Employees
        var mgr = new Employee { Id = 1, FirstName = "Jane", LastName = "Manager", Title = "Mgr" };
        var emp = new Employee
            { Id = 2, FirstName = "John", LastName = "Doe", Title = "Rep", ReportsTo = 1, ReportsToNavigation = mgr };
        mgr.InverseReportsToNavigation.Add(emp);
        db.Employees.AddRange(mgr, emp);

        // Customers
        var cust = new Customer
        {
            Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com", SupportRepId = 2,
            SupportRep = emp
        };
        db.Customers.Add(cust);

        // Artists/Albums/Tracks
        var artist = new Artist { Id = 1, Name = "Artist A" };
        var album = new Album { Id = 1, Title = "Album A", ArtistId = 1, Artist = artist };
        var genre = new Genre { Id = 1, Name = "Rock" };
        var media = new MediaType { Id = 1, Name = "MP3" };
        var track1 = new Track
        {
            Id = 1, Name = "Song 1", AlbumId = 1, Album = album, GenreId = 1, Genre = genre, MediaTypeId = 1,
            MediaType = media, UnitPrice = 0.99m
        };
        var track2 = new Track
        {
            Id = 2, Name = "Song 2", AlbumId = 1, Album = album, GenreId = 1, Genre = genre, MediaTypeId = 1,
            MediaType = media, UnitPrice = 1.29m
        };
        album.Tracks.Add(track1);
        album.Tracks.Add(track2);
        artist.Albums.Add(album);
        db.Artists.Add(artist);
        db.Albums.Add(album);
        db.Genres.Add(genre);
        db.MediaTypes.Add(media);
        db.Tracks.AddRange(track1, track2);

        // Playlist
        var playlist = new Playlist { Id = 1, Name = "My Playlist" };
        playlist.Tracks.Add(track1);
        track1.Playlists.Add(playlist);
        db.Playlists.Add(playlist);

        // Invoice and lines
        var invoice = new Invoice
            { Id = 1, CustomerId = 1, Customer = cust, InvoiceDate = DateTime.UtcNow, Total = 2.28m };
        var il1 = new InvoiceLine
            { Id = 1, InvoiceId = 1, Invoice = invoice, TrackId = 1, Track = track1, UnitPrice = 0.99m, Quantity = 1 };
        var il2 = new InvoiceLine
            { Id = 2, InvoiceId = 1, Invoice = invoice, TrackId = 2, Track = track2, UnitPrice = 1.29m, Quantity = 1 };
        invoice.InvoiceLines.Add(il1);
        invoice.InvoiceLines.Add(il2);
        db.Invoices.Add(invoice);
        db.InvoiceLines.AddRange(il1, il2);

        db.SaveChanges();
    }
}
