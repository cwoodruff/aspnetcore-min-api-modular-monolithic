using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

[Collection(RepositoryDatabaseDefinition.Name)]
public class AlbumRepositoryTests(RepositoryDatabaseFixture database)
{
    [Fact]
    public async Task GetByArtistId_ShouldReturnAlbumsForArtist()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new AlbumRepository(ctx);

        var albums = await repo.GetByArtistId(1);
        albums.Should().HaveCount(1);
        albums[0].ArtistId.Should().Be(1);
    }

    [Fact]
    public async Task GetByArtistId_ShouldReturnEmptyWhenNoAlbums()
    {
        await using var ctx = await database.CreateCleanContextAsync();
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new AlbumRepository(ctx);

        var albums = await repo.GetByArtistId(999);
        albums.Should().BeEmpty();
    }
}
