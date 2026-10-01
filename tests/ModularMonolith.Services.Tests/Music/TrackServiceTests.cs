using Catalog.Modules.Data;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using Catalog.Modules.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ModularMonolith.Services.Tests.Catalog;

[Collection(ModuleDatabaseDefinition.Name)]
public sealed class TrackServiceTests(ModuleDatabaseFixture database) : IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private CatalogDbContext _db = null!;
    private TrackService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateCatalogContext();
        _service = new TrackService(_db, _cache, RecordingCache.Keys(), new TrackValidator(),
            NullLogger<TrackService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static TrackApiModel ValidTrack(int id = 0) => new()
    {
        Id = id, Name = "Track 1", AlbumId = TestData.Album, GenreId = TestData.Genre,
        MediaTypeId = TestData.MediaType, Composer = "C1", UnitPrice = 0.99m, Milliseconds = 100, Bytes = 100
    };

    [Fact]
    public async Task CreateTrackAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        var model = ValidTrack();
        model.Name = null;

        await _service.Invoking(s => s.CreateTrackAsync(model, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        await using var check = database.CreateCatalogContext();
        (await check.Tracks.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CreateTrackAsync_ShouldAddAndInvalidateCache()
    {
        var result = await _service.CreateTrackAsync(ValidTrack(), CancellationToken.None);

        result.Should().NotBeNull();
        await using var check = database.CreateCatalogContext();
        (await check.Tracks.AnyAsync(t => t.Id == result!.Id && t.GenreId == TestData.Genre)).Should().BeTrue();
        _cache.RemovedTags.Should().Equal("catalog:track");
    }

    [Fact]
    public async Task UpdateTrackAsync_ShouldUpdateAndInvalidateCache()
    {
        var model = ValidTrack(TestData.Track1);
        model.Name = "Renamed";

        var result = await _service.UpdateTrackAsync(model, CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateCatalogContext();
        (await check.Tracks.SingleAsync(t => t.Id == TestData.Track1)).Name.Should().Be("Renamed");
        _cache.RemovedTags.Should().Equal("catalog:track");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task GetTrackByIdAsync_ShouldReturnFromCache()
    {
        var result = (TrackApiModel?)await _service.GetTrackByIdAsync(TestData.Track1, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Song 1");
        result.AlbumName.Should().Be("Album A");
        result.GenreId.Should().Be(TestData.Genre, "the genre is referenced by id only");
    }

    [Fact]
    public async Task GetTracksByAlbumIdAsync_ShouldReturnMappedList()
    {
        (await _service.GetTracksByAlbumIdAsync(TestData.Album, CancellationToken.None)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTracksByGenreIdAsync_ShouldReturnTracksInGenre()
    {
        (await _service.GetTracksByGenreIdAsync(TestData.Genre, CancellationToken.None)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTracksByMediaTypeIdAsync_ShouldReturnTracksWithMediaType()
    {
        (await _service.GetTracksByMediaTypeIdAsync(TestData.MediaType, CancellationToken.None)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTracksByPlaylistIdAsync_ShouldReturnTracksInPlaylist()
    {
        var tracks = (await _service.GetTracksByPlaylistIdAsync(TestData.Playlist, CancellationToken.None))
            .Cast<TrackApiModel>().ToList();

        tracks.Should().ContainSingle().Which.Id.Should().Be(TestData.Track1);
    }

    [Fact]
    public async Task GetTracksByArtistIdAsync_ShouldReturnTracksByArtist()
    {
        (await _service.GetTracksByArtistIdAsync(TestData.Artist, CancellationToken.None)).Should().HaveCount(2);
    }

    [Fact]
    public async Task UnknownIds_ShouldReturnEmpty()
    {
        var ct = CancellationToken.None;
        (await _service.GetTracksByAlbumIdAsync(TestData.Unknown, ct)).Should().BeEmpty();
        (await _service.GetTracksByGenreIdAsync(TestData.Unknown, ct)).Should().BeEmpty();
        (await _service.GetTracksByMediaTypeIdAsync(TestData.Unknown, ct)).Should().BeEmpty();
        (await _service.GetTracksByPlaylistIdAsync(TestData.Unknown, ct)).Should().BeEmpty();
        (await _service.GetTracksByArtistIdAsync(TestData.Unknown, ct)).Should().BeEmpty();
    }
}
