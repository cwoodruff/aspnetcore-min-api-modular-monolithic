using FluentAssertions;
using Music.Modules.Services;
using NSubstitute;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Music;

public class TrackServiceTests
{
    private readonly ITrackRepository _repo = Substitute.For<ITrackRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly TrackService _service;

    public TrackServiceTests()
    {
        _service = new TrackService(_repo, _cache, _keys);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task GetTrackByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new TrackApiModel { Id = id, Name = "For Those About To Rock (We Salute You)" };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<object?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetTrackByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetTracksByAlbumIdAsync_ShouldReturnMappedList()
    {
        // Arrange
        var albumId = 1;
        var ct = CancellationToken.None;
        var entities = new List<Track> { new() { Id = 1, Name = "Track 1", AlbumId = albumId } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<object>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<object>>>>(1);
                return await factory(ct);
            });

        _repo.GetByAlbumId(albumId).Returns(entities);

        // Act
        var result = await _service.GetTracksByAlbumIdAsync(albumId, ct);

        // Assert
        result.Should().HaveCount(1);
        var first = result.First() as TrackApiModel;
        first.Should().NotBeNull();
        first!.Name.Should().Be("Track 1");
    }
}
