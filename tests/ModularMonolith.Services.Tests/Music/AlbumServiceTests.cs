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

public class AlbumServiceTests
{
    private readonly IAlbumRepository _repo = Substitute.For<IAlbumRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly IValidator<AlbumApiModel> _validator = Substitute.For<IValidator<AlbumApiModel>>();
    private readonly AlbumService _service;

    public AlbumServiceTests()
    {
        // Default successful validation
        _validator.ValidateAsync(Arg.Any<AlbumApiModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult()));

        _service = new AlbumService(_repo, _cache, _keys, _validator);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task CreateAlbumAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        // Arrange
        var model = new AlbumApiModel { Title = "" };
        var ct = CancellationToken.None;
        
        _validator.ValidateAsync(Arg.Any<AlbumApiModel>(), ct)
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("Title", "Title is required") 
            })));

        // Act & Assert
        await _service.Invoking(s => s.CreateAlbumAsync(model, ct))
            .Should().ThrowAsync<ValidationException>();
        await _repo.DidNotReceive().Add(Arg.Any<Album>());
    }

    [Fact]
    public async Task CreateAlbumAsync_ShouldAddAndInvalidateCache()
    {
        // Arrange
        var model = new AlbumApiModel { Title = "Big Ones", ArtistId = 1 };
        var ct = CancellationToken.None;
        var created = new Album { Id = 10, Title = "Big Ones", ArtistId = 1 };
        _repo.Add(Arg.Any<Album>()).Returns(created);

        // Act
        var result = await _service.CreateAlbumAsync(model, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Big Ones");
        await _repo.Received(1).Add(Arg.Is<Album>(a => a.Title == "Big Ones"));
        await _cache.Received(1).RemoveByTagAsync("music:album", ct);
    }

    [Fact]
    public async Task UpdateAlbumAsync_ShouldUpdateAndInvalidateCache()
    {
        // Arrange
        var model = new AlbumApiModel { Id = 1, Title = "Big Ones", ArtistId = 1 };
        var ct = CancellationToken.None;
        _repo.Update(Arg.Any<Album>()).Returns(true);

        // Act
        var result = await _service.UpdateAlbumAsync(model, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Update(Arg.Is<Album>(a => a.Id == 1 && a.Title == "Big Ones"));
        await _cache.Received(1).RemoveByTagAsync("music:album", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
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
