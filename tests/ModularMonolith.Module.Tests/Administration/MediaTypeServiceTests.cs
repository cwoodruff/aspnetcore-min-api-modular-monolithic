using Admin.Modules.Data;
using Admin.Modules.Models;
using Admin.Modules.Services;
using Admin.Modules.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Administration;

public sealed class MediaTypeServiceTests(AdministrationFixture database) : IClassFixture<AdministrationFixture>, IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private AdministrationDbContext _db = null!;
    private MediaTypeService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateAdministrationContext();
        _service = new MediaTypeService(_db, _cache, RecordingCache.Keys(), new MediaTypeValidator(),
            NullLogger<MediaTypeService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetMediaTypeByIdAsync_ShouldReturnFromCache()
    {
        var result = await _service.GetMediaTypeByIdAsync(TestData.MediaType, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("MP3");
    }

    [Fact]
    public async Task GetAllMediaTypesAsync_ShouldReturnMappedList()
    {
        var all = (await _service.GetAllMediaTypesAsync(CancellationToken.None)).ToList();

        all.Should().ContainSingle().Which.Should().BeEquivalentTo(new MediaTypeApiModel { Id = TestData.MediaType, Name = "MP3" });
    }
}
