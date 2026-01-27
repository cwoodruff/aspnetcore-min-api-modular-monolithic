using FluentValidation;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Orders.Modules.Services;

public class InvoiceService(
    IInvoiceRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<InvoiceApiModel> validator) : IInvoiceService
{
    private readonly IValidator<InvoiceApiModel> _validator = validator;
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

    public async Task<InvoiceApiModel?> CreateInvoiceAsync(InvoiceApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repository.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(InvoiceTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateInvoiceAsync(InvoiceApiModel model, CancellationToken ct)
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
            await cache.RemoveByTagAsync(InvoiceTags[0], ct);
            var key = keys.Compose(
                moduleName: "orders",
                entity: "invoice",
                version: "v1",
                discriminator: $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
