using FluentAssertions;
using Music.Modules.Services;
using NSubstitute;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Music;

public class AlbumServiceTests
{
    private readonly IAlbumRepository _repo = Substitute.For<IAlbumRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly AlbumService _service;

    public AlbumServiceTests()
    {
        _service = new AlbumService(_repo, _cache, _keys);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task GetAlbumByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new AlbumApiModel { Id = id, Title = "Big Ones" };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<AlbumApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetAlbumByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAlbumsByArtistIdAsync_ShouldReturnMappedList()
    {
        // Arrange
        var artistId = 1;
        var ct = CancellationToken.None;
        var entities = new List<Album> { new() { Id = 1, Title = "Big Ones", ArtistId = artistId } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<object>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<object>>>>(1);
                return await factory(ct);
            });

        _repo.GetByArtistId(artistId).Returns(entities);

        // Act
        var result = await _service.GetAlbumsByArtistIdAsync(artistId, ct);

        // Assert
        result.Should().HaveCount(1);
        var first = result.First() as AlbumApiModel;
        first.Should().NotBeNull();
        first!.Title.Should().Be("Big Ones");
    }
}
