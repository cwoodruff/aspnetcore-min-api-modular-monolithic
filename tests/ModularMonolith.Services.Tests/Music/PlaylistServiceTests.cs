using FluentAssertions;
using Music.Modules.Services;
using NSubstitute;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Music;

public class PlaylistServiceTests
{
    private readonly IPlaylistRepository _repo = Substitute.For<IPlaylistRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly PlaylistService _service;

    public PlaylistServiceTests()
    {
        _service = new PlaylistService(_repo, _cache, _keys);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task GetPlaylistByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new PlaylistApiModel { Id = id, Name = "Music" };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<PlaylistApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetPlaylistByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAllPlaylistsAsync_ShouldReturnMappedList()
    {
        // Arrange
        var ct = CancellationToken.None;
        var entities = new List<Playlist> { new() { Id = 1, Name = "Music" } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<object>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<object>>>>(1);
                return await factory(ct);
            });

        _repo.GetAll().Returns(entities);

        // Act
        var result = await _service.GetAllPlaylistsAsync(ct);

        // Assert
        result.Should().HaveCount(1);
        var first = result.First() as PlaylistApiModel;
        first.Should().NotBeNull();
        first!.Name.Should().Be("Music");
    }
}
