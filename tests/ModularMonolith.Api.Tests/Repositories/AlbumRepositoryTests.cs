using FluentAssertions;
using SharedKernel.DataSQLite.Repositories;

namespace ModularMonolith.Api.Tests.Repositories;

public class AlbumRepositoryTests
{
    [Fact]
    public async Task GetByArtistId_ShouldReturnAlbumsForArtist()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new AlbumRepository(ctx);

        var albums = await repo.GetByArtistId(1);
        albums.Should().HaveCount(1);
        albums[0].ArtistId.Should().Be(1);
    }

    [Fact]
    public async Task GetByArtistId_ShouldReturnEmptyWhenNoAlbums()
    {
        using var ctx = TestDbHelpers.CreateInMemoryContext(Guid.NewGuid().ToString());
        TestDbHelpers.SeedMinimalGraph(ctx);
        var repo = new AlbumRepository(ctx);

        var albums = await repo.GetByArtistId(999);
        albums.Should().BeEmpty();
    }
}