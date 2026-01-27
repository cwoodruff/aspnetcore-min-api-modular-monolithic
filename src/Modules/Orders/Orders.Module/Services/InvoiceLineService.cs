using FluentValidation;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Orders.Modules.Services;

public class InvoiceLineService(
    IInvoiceLineRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<InvoiceLineApiModel> validator) : IInvoiceLineService
{
    private readonly IValidator<InvoiceLineApiModel> _validator = validator;
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

    public async Task<InvoiceLineApiModel?> CreateInvoiceLineAsync(InvoiceLineApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.Convert();
        var created = await repository.Add(entity);

        // Invalidate cache
        await cache.RemoveByTagAsync(InvoiceLineTags[0], ct);

        return created?.Convert();
    }

    public async Task<bool> UpdateInvoiceLineAsync(InvoiceLineApiModel model, CancellationToken ct)
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
            await cache.RemoveByTagAsync(InvoiceLineTags[0], ct);
            var key = keys.Compose(
                moduleName: "orders",
                entity: "invoiceline",
                version: "v1",
                discriminator: $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
