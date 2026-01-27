using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

public sealed class CustomerService(
    ICustomerRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys) : ICustomerService
{
    private static readonly string[] CustomerTags = ["administration:customer", "administration:customer:by-id"];

    public async Task<CustomerApiModel?> GetCustomerByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "customer",
            version: "v1",
            discriminator: $"by-id:{id}");

        return await cache.GetOrAddAsync<CustomerApiModel?>(key, async _ =>
        {
            try
            {
                var c = await repo.GetById(id);
                return c;
            }
            catch
            {
                return null;
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = CustomerTags
        }, ct);
    }

    public async Task<IEnumerable<CustomerApiModel>> GetAllCustomersAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "customer",
            version: "v1",
            discriminator: "all");

        return await cache.GetOrAddAsync<IEnumerable<CustomerApiModel>>(key, async _ =>
        {
            try
            {
                var customerEntities = await repo.GetAll();
                return customerEntities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = CustomerTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<CustomerApiModel>> GetCustomersBySupportRepIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            moduleName: "administration",
            entity: "customer",
            version: "v1",
            discriminator: $"by-supportrep:{id}");

        return await cache.GetOrAddAsync<IEnumerable<CustomerApiModel>>(key, async _ =>
        {
            try
            {
                var customerEntities = await repo.GetBySupportRepId(id);
                return customerEntities.ConvertAll();
            }
            catch
            {
                return [];
            }
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = CustomerTags
        }, ct) ?? [];
    }
}
