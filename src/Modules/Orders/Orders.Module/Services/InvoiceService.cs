using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Modules.Data;
using Orders.Modules.Domain;
using Orders.Modules.Mapping;
using Orders.Modules.Models;
using SharedKernel.Caching;

namespace Orders.Modules.Services;

internal class InvoiceService(
    OrdersDbContext db,
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
            await LoadByIdAsync(id, ct)
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
            var entities = await db.Invoices.AsNoTracking().ToListAsync(ct);
            return entities.ToApiModels();
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
            var entities = await LoadByCustomerIdAsync(id, ct);
            return entities.ToApiModels();
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

        var entity = model.ToEntity();
        db.Invoices.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(InvoiceTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateInvoiceAsync(InvoiceApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.Invoices.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.Invoices.Update(entity);
            await db.SaveChangesAsync(ct);

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

    private async Task<List<Invoice>> LoadByCustomerIdAsync(int id, CancellationToken ct)
    {
        return await db.Invoices.Where(a => a.CustomerId == id)
            .AsNoTracking().ToListAsync(ct);
        }

    private async Task<InvoiceApiModel?> LoadByIdAsync(int id, CancellationToken ct)
    {
        return await db.Invoices
            .Where(i => i.Id == id)
            .Select(i => new InvoiceApiModel
            {
                Id = i.Id,
                CustomerId = i.CustomerId,
                InvoiceDate = i.InvoiceDate,
                BillingAddress = i.BillingAddress,
                BillingCity = i.BillingCity,
                BillingState = i.BillingState,
                BillingCountry = i.BillingCountry,
                BillingPostalCode = i.BillingPostalCode,
                Total = i.Total,
                InvoiceLines = i.InvoiceLines.Select(il => new InvoiceLineApiModel
                {
                    Id = il.Id,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity,
                    Invoice = null
                }).ToList()
            })
            .AsNoTracking()
            .SingleOrDefaultAsync(ct);
        }
}
