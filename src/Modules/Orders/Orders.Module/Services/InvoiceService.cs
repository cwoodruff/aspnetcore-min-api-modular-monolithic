using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Orders.Modules.Services;

public class InvoiceService(
    IInvoiceRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys) : IInvoiceService
{
    private static readonly string[] InvoiceTags = ["orders:invoice", "orders:invoice:by-id"];

    public async Task<object?> GetInvoiceByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "orders",
            entity: "invoice",
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
            Tags = InvoiceTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllInvoicesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "orders",
            entity: "invoice",
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
            Tags = InvoiceTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetInvoicesByCustomerIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "orders",
            entity: "invoice",
            version: "v1",
            discriminator: $"by-customer:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            try
            {
                await Task.Yield();
                var entities = await repository.GetByCustomerId(id);
                return entities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceTags
        }, ct) ?? [];
    }
}
