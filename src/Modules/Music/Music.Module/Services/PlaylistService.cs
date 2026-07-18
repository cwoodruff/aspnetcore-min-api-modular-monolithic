using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Music.Modules.Services;

internal class PlaylistService(
    IPlaylistRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<PlaylistApiModel> validator,
    ILogger<PlaylistService> logger) : IPlaylistService
{
    private static readonly string[] PlaylistTags = ["music:playlist", "music:playlist:by-id"];
    private readonly ILogger<PlaylistService> _logger = logger;
    private readonly IValidator<PlaylistApiModel> _validator = validator;

    public async Task<PlaylistApiModel?> GetPlaylistByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "playlist",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<PlaylistApiModel?>(key, async _ =>
            await repository.GetById(id)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = PlaylistTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllPlaylistsAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "music",
            "playlist",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetAll();
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = PlaylistTags
        }, ct) ?? [];
    }

    public async Task<PlaylistApiModel?> CreatePlaylistAsync(PlaylistApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repository.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(PlaylistTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdatePlaylistAsync(PlaylistApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var updated = await repository.Update(entity);

        if (updated)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(PlaylistTags[0], ct);
            var key = keys.Compose(
                "music",
                "playlist",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
