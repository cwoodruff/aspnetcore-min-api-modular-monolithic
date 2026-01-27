using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

public sealed class MediaTypeService(
    IMediaTypeRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys) : IMediaTypeService
{
    private static readonly string[] MediaTypeTags = ["administration:mediatype", "administration:mediatype:by-id"];

    public async Task<MediaTypeApiModel?> GetMediaTypeByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "mediatype",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<MediaTypeApiModel?>(key, async _ =>
        {
            try
            {
                var m = await repo.GetById(id);
                return m.Convert();
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = MediaTypeTags
        }, ct);
    }

    public async Task<IEnumerable<MediaTypeApiModel>> GetAllMediaTypesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "mediatype",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<MediaTypeApiModel>>(key, async _ =>
        {
            try
            {
                var mediaTypeEntities = await repo.GetAll();
                return mediaTypeEntities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = MediaTypeTags
        }, ct) ?? [];
    }
}
