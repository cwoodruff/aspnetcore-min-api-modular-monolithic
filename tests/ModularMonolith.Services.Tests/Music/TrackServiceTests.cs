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

public class TrackServiceTests
{
    private readonly ITrackRepository _repo = Substitute.For<ITrackRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly IValidator<TrackApiModel> _validator = Substitute.For<IValidator<TrackApiModel>>();
    private readonly TrackService _service;

    public TrackServiceTests()
    {
        // Default successful validation
        _validator.ValidateAsync(Arg.Any<TrackApiModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult()));

        _service = new TrackService(_repo, _cache, _keys, _validator);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task CreateTrackAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        // Arrange
        var model = new TrackApiModel { Name = "" };
        var ct = CancellationToken.None;
        
        _validator.ValidateAsync(Arg.Any<TrackApiModel>(), ct)
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("Name", "Name is required") 
            })));

        // Act & Assert
        await _service.Invoking(s => s.CreateTrackAsync(model, ct))
            .Should().ThrowAsync<ValidationException>();
        await _repo.DidNotReceive().Add(Arg.Any<Track>());
    }

    [Fact]
    public async Task CreateTrackAsync_ShouldAddAndInvalidateCache()
    {
        // Arrange
        var model = new TrackApiModel { Name = "Track 1", AlbumId = 1, GenreId = 1, MediaTypeId = 1, Composer = "C1", UnitPrice = 0.99m, Milliseconds = 100, Bytes = 100 };
        var ct = CancellationToken.None;
        var created = new Track { Id = 10, Name = "Track 1" };
        _repo.Add(Arg.Any<Track>()).Returns(created);

        // Act
        var result = await _service.CreateTrackAsync(model, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Track 1");
        await _repo.Received(1).Add(Arg.Is<Track>(t => t.Name == "Track 1"));
        await _cache.Received(1).RemoveByTagAsync("music:track", ct);
    }

    [Fact]
    public async Task UpdateTrackAsync_ShouldUpdateAndInvalidateCache()
    {
        // Arrange
        var model = new TrackApiModel { Id = 1, Name = "Track 1", AlbumId = 1, GenreId = 1, MediaTypeId = 1, Composer = "C1", UnitPrice = 0.99m, Milliseconds = 100, Bytes = 100 };
        var ct = CancellationToken.None;
        _repo.Update(Arg.Any<Track>()).Returns(true);

        // Act
        var result = await _service.UpdateTrackAsync(model, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Update(Arg.Is<Track>(t => t.Id == 1 && t.Name == "Track 1"));
        await _cache.Received(1).RemoveByTagAsync("music:track", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
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
