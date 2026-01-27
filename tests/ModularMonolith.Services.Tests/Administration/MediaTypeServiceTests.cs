using Admin.Modules.Services;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Administration;

public class MediaTypeServiceTests
{
    private readonly IMediaTypeRepository _repo = Substitute.For<IMediaTypeRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly IValidator<MediaTypeApiModel> _validator = Substitute.For<IValidator<MediaTypeApiModel>>();
    private readonly MediaTypeService _service;

    public MediaTypeServiceTests()
    {
        _service = new MediaTypeService(_repo, _cache, _keys, _validator);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task GetMediaTypeByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new MediaTypeApiModel { Id = id, Name = "MPEG audio file" };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<MediaTypeApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetMediaTypeByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetAllMediaTypesAsync_ShouldReturnMappedList()
    {
        // Arrange
        var ct = CancellationToken.None;
        var entities = new List<MediaType> { new() { Id = 1, Name = "MPEG audio file" } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<MediaTypeApiModel>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<MediaTypeApiModel>>>>(1);
                return await factory(ct);
            });

        _repo.GetAll().Returns(entities);

        // Act
        var result = await _service.GetAllMediaTypesAsync(ct);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("MPEG audio file");
    }
}
