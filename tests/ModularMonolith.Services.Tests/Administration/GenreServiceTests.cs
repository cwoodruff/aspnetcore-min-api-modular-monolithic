using Admin.Modules.Services;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Administration;

public class GenreServiceTests
{
    private readonly IGenreRepository _repo = Substitute.For<IGenreRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly GenreService _service;

    public GenreServiceTests()
    {
        _service = new GenreService(_repo, _cache, _keys);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task GetGenreByIdAsync_ShouldReturnMappedEntity_WhenFound()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var entity = new Genre { Id = id, Name = "Rock" };

        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<GenreApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<GenreApiModel?>>>(1);
                return await factory(ct);
            });

        _repo.GetById(id).Returns(entity);

        // Act
        var result = await _service.GetGenreByIdAsync(id, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Name.Should().Be("Rock");
    }

    [Fact]
    public async Task CreateGenreAsync_ShouldAddAndInvalidateCache()
    {
        // Arrange
        var name = "Jazz";
        var ct = CancellationToken.None;
        var created = new Genre { Id = 10, Name = name };
        _repo.Add(Arg.Any<Genre>()).Returns(created);

        // Act
        var result = await _service.CreateGenreAsync(name, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(name);
        await _repo.Received(1).Add(Arg.Is<Genre>(g => g.Name == name));
        await _cache.Received(1).RemoveByTagAsync("administration:genre", ct);
    }

    [Fact]
    public async Task UpdateGenreAsync_ShouldUpdateAndInvalidateCache()
    {
        // Arrange
        var id = 1;
        var name = "Pop";
        var ct = CancellationToken.None;
        _repo.Update(Arg.Any<Genre>()).Returns(true);

        // Act
        var result = await _service.UpdateGenreAsync(id, name, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Update(Arg.Is<Genre>(g => g.Id == id && g.Name == name));
        await _cache.Received(1).RemoveByTagAsync("administration:genre", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
    }

    [Fact]
    public async Task DeleteGenreAsync_ShouldDeleteAndInvalidateCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        _repo.Delete(id).Returns(true);

        // Act
        var result = await _service.DeleteGenreAsync(id, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Delete(id);
        await _cache.Received(1).RemoveByTagAsync("administration:genre", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
    }
}
