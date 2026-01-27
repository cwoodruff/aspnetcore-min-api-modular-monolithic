using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Orders.Modules.Services;

public class InvoiceLineService(
    IInvoiceLineRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys) : IInvoiceLineService
{
    private static readonly string[] InvoiceLineTags = ["orders:invoiceline", "orders:invoiceline:by-id"];

    public async Task<object?> GetInvoiceLineByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "orders",
            entity: "invoiceline",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<object?>(key, async _ =>
        {
            try
            {
                return await repository.GetById(id);
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceLineTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllInvoiceLinesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "orders",
            entity: "invoiceline",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetAll();
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceLineTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetInvoiceLinesByInvoiceIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "orders",
            entity: "invoiceline",
            version: "v1",
            discriminator: $"by-invoice:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByInvoiceId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceLineTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetInvoiceLinesByTrackIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "orders",
            entity: "invoiceline",
            version: "v1",
            discriminator: $"by-track:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByTrackId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceLineTags
        }, ct) ?? [];
    }
}
