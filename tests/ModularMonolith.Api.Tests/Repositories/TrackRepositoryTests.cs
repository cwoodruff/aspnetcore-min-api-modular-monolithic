using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

[Collection(RepositoryDatabaseDefinition.Name)]
public class TrackRepositoryTests(RepositoryDatabaseFixture database)
{
    [Fact]
    public async Task GetByAlbumId_ShouldReturnTracksInAlbum()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new TrackRepository(ctx);

        var tracks = await repo.GetByAlbumId(1);
        tracks.Should().HaveCount(2);
        tracks.Should().OnlyContain(t => t.AlbumId == 1);
    }

    [Fact]
    public async Task GetByGenreId_ShouldReturnTracksInGenre()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new TrackRepository(ctx);

        var tracks = await repo.GetByGenreId(1);
        tracks.Should().HaveCount(2);
        tracks.Should().OnlyContain(t => t.GenreId == 1);
    }

    [Fact]
    public async Task GetByMediaTypeId_ShouldReturnTracksWithMediaType()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new TrackRepository(ctx);

        var tracks = await repo.GetByMediaTypeId(1);
        tracks.Should().HaveCount(2);
        tracks.Should().OnlyContain(t => t.MediaTypeId == 1);
    }

    [Fact]
    public async Task GetByPlaylistId_ShouldReturnTracksInPlaylist()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new TrackRepository(ctx);

        var tracks = await repo.GetByPlaylistId(1);
        tracks.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByArtistId_ShouldReturnTracksByArtist()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new TrackRepository(ctx);

        var tracks = await repo.GetByArtistId(1);
        tracks.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByInvoiceId_ShouldReturnTracksOnInvoice()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new TrackRepository(ctx);

        var tracks = await repo.GetByInvoiceId(1);
        tracks.Should().HaveCount(2);
    }

    [Fact]
    public async Task UnknownIds_ShouldReturnEmpty()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new TrackRepository(ctx);

        (await repo.GetByAlbumId(999)).Should().BeEmpty();
        (await repo.GetByGenreId(999)).Should().BeEmpty();
        (await repo.GetByMediaTypeId(999)).Should().BeEmpty();
        (await repo.GetByPlaylistId(999)).Should().BeEmpty();
        (await repo.GetByArtistId(999)).Should().BeEmpty();
        (await repo.GetByInvoiceId(999)).Should().BeEmpty();
    }
}
