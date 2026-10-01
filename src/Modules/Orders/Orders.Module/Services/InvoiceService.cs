using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Contracts.Events;
using Orders.Modules.Data;
using Orders.Modules.Domain;
using Orders.Modules.Mapping;
using Orders.Modules.Models;
using SharedKernel.Caching;
using SharedKernel.Events;

namespace Orders.Modules.Services;

internal class InvoiceService(
    OrdersDbContext db,
    [FromKeyedServices(OrdersModule.ModuleName)] ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<InvoiceApiModel> validator,
    [FromKeyedServices(OrdersModule.ModuleName)] IEventPublisher events,
    TimeProvider time,
    ILogger<InvoiceService> logger) : IInvoiceService
{
    private static readonly string[] InvoiceTags = ["orders:invoice", "orders:invoice:by-id"];
    private readonly ILogger<InvoiceService> _logger = logger;
    private readonly IValidator<InvoiceApiModel> _validator = validator;

    public async Task<InvoiceApiModel?> GetInvoiceByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoice",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<InvoiceApiModel?>(key, async _ =>
            await LoadByIdAsync(id, ct)
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceTags
        }, ct);
    }

    public async Task<IReadOnlyList<InvoiceApiModel>> GetAllInvoicesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoice",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IReadOnlyList<InvoiceApiModel>>(key, async _ =>
        {
            var entities = await db.Invoices.AsNoTracking().ToListAsync(ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceTags
        }, ct) ?? [];
    }

    public async Task<IReadOnlyList<InvoiceApiModel>> GetInvoicesByCustomerIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoice",
            "v1",
            $"by-customer:{id}");

        return await cache.GetOrAddAsync<IReadOnlyList<InvoiceApiModel>>(key, async _ =>
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
            // Status changes only through FinalizeInvoiceAsync.
            db.Entry(entity).Property(e => e.Status).IsModified = false;
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

    public async Task<FinalizeInvoiceResult> FinalizeInvoiceAsync(int id, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.InvoiceLines).SingleOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null)
        {
            return FinalizeInvoiceResult.NotFound;
        }

        if (invoice.Status == InvoiceStatus.Finalized)
        {
            return FinalizeInvoiceResult.AlreadyFinalized;
        }

        invoice.Status = InvoiceStatus.Finalized;

        // The outbox row commits in the same SaveChanges as the status change, or neither does.
        await events.PublishAsync(new InvoiceFinalized(
            Guid.NewGuid(),
            time.GetUtcNow(),
            invoice.Id,
            invoice.CustomerId,
            invoice.InvoiceDate,
            invoice.Total,
            invoice.InvoiceLines
                .Where(line => line.TrackId is not null)
                .Select(line => new InvoiceFinalizedLine(line.TrackId!.Value, line.Quantity ?? 0, line.UnitPrice ?? 0m))
                .ToList()), db, ct);
        await db.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync(InvoiceTags[0], ct);
        await cache.RemoveAsync(keys.Compose("orders", "invoice", "v1", $"by-id:{id}"), ct);
        return FinalizeInvoiceResult.Finalized;
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
                Status = i.Status.ToString(),
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
