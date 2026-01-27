using FluentAssertions;
using FluentValidation;
using Music.Modules.Services;
using NSubstitute;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Music;

public class ArtistServiceTests
{
    private readonly IArtistRepository _repo = Substitute.For<IArtistRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly IValidator<ArtistApiModel> _validator = Substitute.For<IValidator<ArtistApiModel>>();
    private readonly ArtistService _service;

    public ArtistServiceTests()
    {
        _service = new ArtistService(_repo, _cache, _keys, _validator);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task GetArtistByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new ArtistApiModel { Id = id, Name = "AC/DC" };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<ArtistApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetArtistByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAllArtistsAsync_ShouldReturnMappedList()
    {
        // Arrange
        var ct = CancellationToken.None;
        var entities = new List<Artist> { new() { Id = 1, Name = "AC/DC" } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<object>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<object>>>>(1);
                return await factory(ct);
            });

        _repo.GetAll().Returns(entities);

        // Act
        var result = await _service.GetAllArtistsAsync(ct);

        // Assert
        result.Should().HaveCount(1);
        var first = result.First() as ArtistApiModel;
        first.Should().NotBeNull();
        first!.Name.Should().Be("AC/DC");
    }
}
