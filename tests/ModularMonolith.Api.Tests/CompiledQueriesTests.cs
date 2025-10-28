using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Persistence;
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ModularMonolith.Api.Tests;

public sealed class CompiledQueriesFixture : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly DbContextOptions<AppDbContext> _options;

    public CompiledQueriesFixture()
    {
        // Shared root provider to keep a single EF Core model instance for all contexts
        _provider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("CompiledQueriesDb")
            .UseInternalServiceProvider(_provider)
            .EnableSensitiveDataLogging()
            .Options;

        using var seed = new AppDbContext(_options);
        seed.Database.EnsureDeleted();
        seed.Database.EnsureCreated();
        TestDbHelpers.SeedMinimalGraph(seed);
    }

    public AppDbContext CreateContext() => new AppDbContext(_options);

    public void Dispose()
    {
        ( _provider as IDisposable )?.Dispose();
    }
}

[Collection("SequentialTests")]
public class CompiledQueriesTests(CompiledQueriesFixture fixture) : IClassFixture<CompiledQueriesFixture>
{
    private AppDbContext NewDb() => fixture.CreateContext();
    private AppDbContext CreateDbScope() => NewDb();

    // Album
    [Fact]
    public async Task Album_GetAllAlbums_ShouldReturnAlbums()
    {
        await using var db = CreateDbScope();
        var all = await db.GetAllAlbums();
        var list = await all.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Album_GetAlbum_ById_ShouldReturnAlbumOrNull()
    {
        await using var db = CreateDbScope();
        var a1 = await db.GetAlbum(1);
        if (a1 is not null)
        {
            a1.Artist.Should().NotBeNull();
            a1.Tracks.Should().NotBeNull();
        }

        var missing = await db.GetAlbum(999999);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Album_GetByArtistId_ShouldReturnAlbums()
    {
        await using var db = CreateDbScope();
        var q = await db.GetAlbumsByArtistId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    // Artist
    [Fact]
    public async Task Artist_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = await db.GetAllArtists();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Artist_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var a = await db.GetArtist(1);
        if (a is not null)
        {
            a.Albums.Should().NotBeNull();
        }

        var missing = await db.GetArtist(999999);
        missing.Should().BeNull();
    }

    // Customer
    [Fact]
    public async Task Customer_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = await db.GetAllCustomers();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Customer_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var c = await db.GetCustomer(1);
        if (c is not null)
        {
            c.Invoices.Should().NotBeNull();
            c.SupportRep.Should().NotBeNull();
        }

        var missing = await db.GetCustomer(999999);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Customer_GetBySupportRepId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = await db.GetCustomerBySupportRepId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    // Employee
    [Fact]
    public async Task Employee_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = await db.GetAllEmployees();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Employee_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var e = await db.GetEmployee(1);
        if (e is not null)
        {
            e.Customers.Should().NotBeNull();
        }

        var missing = await db.GetEmployee(999999);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Employee_DirectReports_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = await db.GetEmployeeDirectReports(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Employee_ReportsTo_ShouldReturnManager()
    {
        await using var db = CreateDbScope();
        var mgr = await db.GetEmployeeGetReportsTo(1);
        mgr.Should().NotBeNull();
    }

    // Genre
    [Fact]
    public async Task Genre_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetAllGenres();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Genre_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var g = await db.GetGenre(1);
        if (g is not null)
        {
            g.Tracks.Should().NotBeNull();
        }

        var missing = await db.GetGenre(999999);
        missing.Should().BeNull();
    }

    // InvoiceLine
    [Fact]
    public async Task InvoiceLine_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetAllInvoiceLines();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task InvoiceLine_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var il = await db.GetInvoiceLine(1);
        if (il is not null)
        {
            il.Track.Should().NotBeNull();
        }

        var missing = await db.GetInvoiceLine(999999);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task InvoiceLine_ByInvoiceId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetInvoiceLinesByInvoiceId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task InvoiceLine_ByTrackId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetInvoiceLinesByTrackId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    // Invoice
    [Fact]
    public async Task Invoice_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetAllInvoices();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Invoice_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var inv = await db.GetInvoice(1);
        if (inv is not null)
        {
            inv.Customer.Should().NotBeNull();
            inv.InvoiceLines.Should().NotBeNull();
        }

        var missing = await db.GetInvoice(999999);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Invoice_ByCustomerId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetInvoicesByCustomerId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Invoice_ByEmployeeId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetInvoicesByEmployeeId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    // MediaType
    [Fact]
    public async Task MediaType_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetAllMediaTypes();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task MediaType_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var m = await db.GetMediaType(1);
        if (m is not null)
        {
            m.Tracks.Should().NotBeNull();
        }

        var missing = await db.GetMediaType(999999);
        missing.Should().BeNull();
    }

    // Playlist
    [Fact]
    public async Task Playlist_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetAllPlaylists();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Playlist_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var p = await db.GetPlaylist(1);
        if (p is not null)
        {
            p.Tracks.Should().NotBeNull();
        }

        var missing = await db.GetPlaylist(999999);
        missing.Should().BeNull();
    }

    // Track
    [Fact]
    public async Task Track_GetAll_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetAllTracks();
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_GetById_ShouldReturnOrNull()
    {
        await using var db = CreateDbScope();
        var t = await db.GetTrack(1);
        if (t is not null)
        {
            t.Album.Should().NotBeNull();
            t.Genre.Should().NotBeNull();
            t.MediaType.Should().NotBeNull();
        }

        var missing = await db.GetTrack(999999);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Track_ByAlbumId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetTracksByAlbumId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_ByGenreId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetTracksByGenreId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_ByMediaTypeId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetTracksByMediaTypeId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_ByArtistId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetTracksByArtistId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_ByInvoiceId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetTracksByInvoiceId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_ByPlaylistId_ShouldReturn()
    {
        await using var db = CreateDbScope();
        var q = db.GetTracksByPlaylistId(1);
        var list = await q.ToListAsync();
        list.Should().NotBeNull();
    }
}
