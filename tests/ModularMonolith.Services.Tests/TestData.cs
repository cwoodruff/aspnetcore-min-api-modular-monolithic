using Admin.Modules.Domain;
using Catalog.Modules.Domain;
using Microsoft.EntityFrameworkCore;
using Orders.Modules.Domain;

namespace ModularMonolith.Services.Tests;

/// <summary>
///     A small graph written through each module's own context. Cross-module references are plain
///     ids, as in production: Track 1 uses Genre 1 and MediaType 1, Invoice 1 belongs to Customer 1,
///     and its lines point at Tracks 1 and 2.
/// </summary>
internal static class TestData
{
    public const int Manager = 1;
    public const int Rep = 2;
    public const int Customer = 1;
    public const int Genre = 1;
    public const int MediaType = 1;
    public const int Artist = 1;
    public const int Album = 1;
    public const int Track1 = 1;
    public const int Track2 = 2;
    public const int Playlist = 1;
    public const int Invoice = 1;
    public const int Unknown = 9999;

    public static async Task SeedAsync(ModuleDatabaseFixture database)
    {
        await using (var administration = database.CreateAdministrationContext())
        {
            administration.Employees.AddRange(
                new Employee { Id = Manager, FirstName = "Jane", LastName = "Manager", Title = "Mgr" },
                new Employee { Id = Rep, FirstName = "John", LastName = "Doe", Title = "Rep", ReportsTo = Manager });
            administration.Customers.Add(new Customer
            {
                Id = Customer, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com", SupportRepId = Rep
            });
            administration.Genres.Add(new Genre { Id = Genre, Name = "Rock" });
            administration.MediaTypes.Add(new MediaType { Id = MediaType, Name = "MP3" });
            await administration.SaveChangesAsync();
            await ResetSequencesAsync(administration, "Employee", "Customer", "Genre", "MediaType");
        }

        await using (var catalog = database.CreateCatalogContext())
        {
            var track1 = new Track
            {
                Id = Track1, Name = "Song 1", AlbumId = Album, GenreId = Genre, MediaTypeId = MediaType,
                Composer = "C", Milliseconds = 1000, Bytes = 1000, UnitPrice = 0.99m
            };
            var track2 = new Track
            {
                Id = Track2, Name = "Song 2", AlbumId = Album, GenreId = Genre, MediaTypeId = MediaType,
                Composer = "C", Milliseconds = 1000, Bytes = 1000, UnitPrice = 1.29m
            };
            catalog.Artists.Add(new Artist { Id = Artist, Name = "Artist A" });
            catalog.Albums.Add(new Album { Id = Album, Title = "Album A", ArtistId = Artist });
            catalog.Tracks.AddRange(track1, track2);
            var playlist = new Playlist { Id = Playlist, Name = "My Playlist" };
            playlist.Tracks.Add(track1);
            catalog.Playlists.Add(playlist);
            await catalog.SaveChangesAsync();
            await ResetSequencesAsync(catalog, "Artist", "Album", "Track", "Playlist");
        }

        await using (var orders = database.CreateOrdersContext())
        {
            orders.Invoices.Add(new Invoice
            {
                Id = Invoice, CustomerId = Customer, InvoiceDate = DateTime.UtcNow, Total = 2.28m,
                InvoiceLines =
                {
                    new InvoiceLine { Id = 1, TrackId = Track1, UnitPrice = 0.99m, Quantity = 1 },
                    new InvoiceLine { Id = 2, TrackId = Track2, UnitPrice = 1.29m, Quantity = 1 }
                }
            });
            await orders.SaveChangesAsync();
            await ResetSequencesAsync(orders, "Invoice", "InvoiceLine");
        }
    }

    // The graph uses explicit ids; move each identity sequence past them so later inserts do not collide.
    private static async Task ResetSequencesAsync(DbContext context, params string[] tables)
    {
        var schema = context.Model.GetDefaultSchema();
        foreach (var table in tables)
        {
            var qualified = $"{schema}.\"{table}\"";
#pragma warning disable EF1002 // Table names come from the constants above, not from input.
            await context.Database.ExecuteSqlRawAsync(
                $"SELECT setval(pg_get_serial_sequence('{qualified}', 'Id'), (SELECT max(\"Id\") FROM {qualified}))");
#pragma warning restore EF1002
        }
    }
}
