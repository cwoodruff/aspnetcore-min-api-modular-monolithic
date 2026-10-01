using Admin.Modules.Data;
using Admin.Modules.Services;
using Admin.Modules.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Administration;

public sealed class GenreServiceTests(AdministrationFixture database) : IClassFixture<AdministrationFixture>, IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private AdministrationDbContext _db = null!;
    private GenreService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateAdministrationContext();
        _service = new GenreService(_db, _cache, RecordingCache.Keys(), new GenreValidator(),
            NullLogger<GenreService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetGenreByIdAsync_ShouldReturnMappedEntity_WhenFound()
    {
        var result = await _service.GetGenreByIdAsync(TestData.Genre, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(TestData.Genre);
        result.Name.Should().Be("Rock");
    }

    [Fact]
    public async Task GetGenreByIdAsync_ShouldReturnNull_WhenMissing()
    {
        (await _service.GetGenreByIdAsync(TestData.Unknown, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetAllGenresAsync_ShouldReturnSeededEntities()
    {
        var all = (await _service.GetAllGenresAsync(CancellationToken.None)).ToList();

        all.Should().ContainSingle().Which.Name.Should().Be("Rock");
    }

    [Fact]
    public async Task CreateGenreAsync_ShouldAddAndInvalidateCache()
    {
        var result = await _service.CreateGenreAsync("Jazz", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(TestData.Genre, "the identity sequence starts after the seeded ids");
        result.Name.Should().Be("Jazz");
        await using var check = database.CreateAdministrationContext();
        (await check.Genres.SingleAsync(g => g.Id == result.Id)).Name.Should().Be("Jazz");
        _cache.RemovedTags.Should().Equal("administration:genre");
    }

    [Fact]
    public async Task CreateGenreAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        var tooLong = new string('x', 121);

        await _service.Invoking(s => s.CreateGenreAsync(tooLong, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        await using var check = database.CreateAdministrationContext();
        (await check.Genres.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateGenreAsync_ShouldUpdateAndInvalidateCache()
    {
        var result = await _service.UpdateGenreAsync(TestData.Genre, "Pop", CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateAdministrationContext();
        (await check.Genres.SingleAsync(g => g.Id == TestData.Genre)).Name.Should().Be("Pop");
        _cache.RemovedTags.Should().Equal("administration:genre");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateGenreAsync_ShouldReturnFalse_WhenMissing()
    {
        (await _service.UpdateGenreAsync(TestData.Unknown, "Pop", CancellationToken.None)).Should().BeFalse();
        _cache.RemovedTags.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteGenreAsync_ShouldDeleteAndInvalidateCache()
    {
        var result = await _service.DeleteGenreAsync(TestData.Genre, CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateAdministrationContext();
        (await check.Genres.AnyAsync(g => g.Id == TestData.Genre)).Should().BeFalse();
        _cache.RemovedTags.Should().Equal("administration:genre");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteGenreAsync_ShouldReturnFalse_WhenMissing()
    {
        (await _service.DeleteGenreAsync(TestData.Unknown, CancellationToken.None)).Should().BeFalse();
    }
}
