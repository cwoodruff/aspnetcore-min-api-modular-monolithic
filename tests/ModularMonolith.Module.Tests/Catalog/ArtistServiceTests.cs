using Catalog.Modules.Data;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Catalog.Modules.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Catalog;

public sealed class ArtistServiceTests(CatalogFixture database) : IClassFixture<CatalogFixture>, IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private CatalogDbContext _db = null!;
    private ArtistService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateCatalogContext();
        _service = new ArtistService(_db, _cache, RecordingCache.Keys(), new ArtistValidator(),
            NullLogger<ArtistService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetArtistByIdAsync_ShouldReturnFromCache()
    {
        var result = await _service.GetArtistByIdAsync(TestData.Artist, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Artist A");
        result.Albums.Should().ContainSingle().Which.Tracks.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllArtistsAsync_ShouldReturnMappedList()
    {
        var all = (await _service.GetAllArtistsAsync(CancellationToken.None)).Cast<ArtistApiModel>().ToList();

        all.Should().ContainSingle().Which.Name.Should().Be("Artist A");
    }
}
