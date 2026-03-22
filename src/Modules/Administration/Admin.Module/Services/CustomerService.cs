using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Admin.Modules.Services;

public sealed class CustomerService(
    ICustomerRepository repo,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<CustomerApiModel> validator,
    ILogger<CustomerService> logger) : ICustomerService
{
    private static readonly string[] CustomerTags = ["administration:customer", "administration:customer:by-id"];
    private readonly ILogger<CustomerService> _logger = logger;
    private readonly IValidator<CustomerApiModel> _validator = validator;

    public async Task<CustomerApiModel?> GetCustomerByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "customer",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<CustomerApiModel?>(key, async _ =>
            await repo.GetById(id)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = CustomerTags
        }, ct);
    }

    public async Task<IEnumerable<CustomerApiModel>> GetAllCustomersAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "customer",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<CustomerApiModel>>(key, async _ =>
        {
            var customerEntities = await repo.GetAll();
            return customerEntities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = CustomerTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<CustomerApiModel>> GetCustomersBySupportRepIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "administration",
            "customer",
            "v1",
            $"by-supportrep:{id}");

        return await cache.GetOrAddAsync<IEnumerable<CustomerApiModel>>(key, async _ =>
        {
            var customerEntities = await repo.GetBySupportRepId(id);
            return customerEntities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = CustomerTags
        }, ct) ?? [];
    }

    public async Task<CustomerApiModel?> CreateCustomerAsync(CustomerApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repo.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(CustomerTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateCustomerAsync(CustomerApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var updated = await repo.Update(entity);

        if (updated)
        {
            // Invalidate cache
            await cache.RemoveByTagAsync(CustomerTags[0], ct);
            var key = keys.Compose(
                "administration",
                "customer",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
