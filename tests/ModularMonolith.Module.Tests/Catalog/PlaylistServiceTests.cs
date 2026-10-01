using Catalog.Modules.Data;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Catalog.Modules.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Catalog;

public sealed class PlaylistServiceTests(CatalogFixture database) : IClassFixture<CatalogFixture>, IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private CatalogDbContext _db = null!;
    private PlaylistService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateCatalogContext();
        _service = new PlaylistService(_db, _cache, RecordingCache.Keys(), new PlaylistValidator(),
            NullLogger<PlaylistService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetPlaylistByIdAsync_ShouldReturnFromCache()
    {
        var result = await _service.GetPlaylistByIdAsync(TestData.Playlist, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("My Playlist");
        result.Tracks.Should().ContainSingle().Which.Id.Should().Be(TestData.Track1);
    }

    [Fact]
    public async Task GetAllPlaylistsAsync_ShouldReturnMappedList()
    {
        var all = (await _service.GetAllPlaylistsAsync(CancellationToken.None)).Cast<PlaylistApiModel>().ToList();

        all.Should().ContainSingle().Which.Name.Should().Be("My Playlist");
    }
}
