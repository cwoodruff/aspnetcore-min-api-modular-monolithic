using Catalog.Modules.Data;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Catalog.Modules.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Catalog;

public sealed class AlbumServiceTests(CatalogFixture database) : IClassFixture<CatalogFixture>, IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private CatalogDbContext _db = null!;
    private AlbumService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateCatalogContext();
        _service = new AlbumService(_db, _cache, RecordingCache.Keys(), new AlbumValidator(),
            NullLogger<AlbumService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CreateAlbumAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        var model = new AlbumApiModel { Title = "ab", ArtistId = TestData.Artist };

        await _service.Invoking(s => s.CreateAlbumAsync(model, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        await using var check = database.CreateCatalogContext();
        (await check.Albums.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAlbumAsync_ShouldAddAndInvalidateCache()
    {
        var model = new AlbumApiModel { Title = "Second Album", ArtistId = TestData.Artist };

        var result = await _service.CreateAlbumAsync(model, CancellationToken.None);

        result.Should().NotBeNull();
        await using var check = database.CreateCatalogContext();
        (await check.Albums.AnyAsync(a => a.Id == result!.Id && a.Title == "Second Album")).Should().BeTrue();
        _cache.RemovedTags.Should().Equal("catalog:album");
    }

    [Fact]
    public async Task UpdateAlbumAsync_ShouldUpdateAndInvalidateCache()
    {
        var model = new AlbumApiModel { Id = TestData.Album, Title = "Renamed", ArtistId = TestData.Artist };

        var result = await _service.UpdateAlbumAsync(model, CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateCatalogContext();
        (await check.Albums.SingleAsync(a => a.Id == TestData.Album)).Title.Should().Be("Renamed");
        _cache.RemovedTags.Should().Equal("catalog:album");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAlbumByIdAsync_ShouldReturnFromCache()
    {
        var result = await _service.GetAlbumByIdAsync(TestData.Album, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ArtistName.Should().Be("Artist A");
        result.Tracks.Select(t => t.Id).Should().BeEquivalentTo([TestData.Track1, TestData.Track2]);
    }

    [Fact]
    public async Task GetAlbumsByArtistIdAsync_ShouldReturnMappedList()
    {
        var albums = (await _service.GetAlbumsByArtistIdAsync(TestData.Artist, CancellationToken.None))
            .Cast<AlbumApiModel>().ToList();

        albums.Should().ContainSingle().Which.ArtistName.Should().Be("Artist A");
    }

    [Fact]
    public async Task GetAlbumsByArtistIdAsync_ShouldReturnEmptyWhenNoAlbums()
    {
        (await _service.GetAlbumsByArtistIdAsync(TestData.Unknown, CancellationToken.None)).Should().BeEmpty();
    }
}
