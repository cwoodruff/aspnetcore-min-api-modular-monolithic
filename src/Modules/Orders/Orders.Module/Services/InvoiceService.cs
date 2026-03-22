using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Extensions;
using SharedKernel.Persistence.Repositories;

namespace Orders.Modules.Services;

public class InvoiceService(
    IInvoiceRepository repository,
    ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<InvoiceApiModel> validator,
    ILogger<InvoiceService> logger) : IInvoiceService
{
    private static readonly string[] InvoiceTags = ["orders:invoice", "orders:invoice:by-id"];
    private readonly ILogger<InvoiceService> _logger = logger;
    private readonly IValidator<InvoiceApiModel> _validator = validator;

    public async Task<object?> GetInvoiceByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoice",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<object?>(key, async _ =>
            await repository.GetById(id)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceTags
        }, ct);
    }

    public async Task<IEnumerable<object>> GetAllInvoicesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoice",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetAll();
            return entities.ConvertAll();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceTags
        }, ct) ?? [];
    }

    public async Task<IEnumerable<object>> GetInvoicesByCustomerIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoice",
            "v1",
            $"by-customer:{id}");

        return await cache.GetOrAddAsync<IEnumerable<object>>(key, async _ =>
        {
            var entities = await repository.GetByCustomerId(id);
            return entities.ConvertAll();
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
                "orders",
                "invoice",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }
}
