using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Modules.Data;
using Orders.Modules.Domain;
using Orders.Modules.Mapping;
using Orders.Modules.Models;
using SharedKernel.Caching;

namespace Orders.Modules.Services;

internal class InvoiceLineService(
    OrdersDbContext db,
    [FromKeyedServices(OrdersModule.ModuleName)] ICacheFacade cache,
    ICacheKeyComposer keys,
    IValidator<InvoiceLineApiModel> validator,
    ILogger<InvoiceLineService> logger) : IInvoiceLineService
{
    private static readonly string[] InvoiceLineTags = ["orders:invoiceline", "orders:invoiceline:by-id"];
    private readonly ILogger<InvoiceLineService> _logger = logger;
    private readonly IValidator<InvoiceLineApiModel> _validator = validator;

    public async Task<InvoiceLineApiModel?> GetInvoiceLineByIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoiceline",
            "v1",
            $"by-id:{id}");

        return await cache.GetOrAddAsync<InvoiceLineApiModel?>(key, async _ =>
            (await LoadByIdAsync(id, ct))?.ToApiModel()
        , new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceLineTags
        }, ct);
    }

    public async Task<IReadOnlyList<InvoiceLineApiModel>> GetAllInvoiceLinesAsync(CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoiceline",
            "v1",
            "all");

        return await cache.GetOrAddAsync<IReadOnlyList<InvoiceLineApiModel>>(key, async _ =>
        {
            var entities = await db.InvoiceLines.AsNoTracking().ToListAsync(ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceLineTags
        }, ct) ?? [];
    }

    public async Task<IReadOnlyList<InvoiceLineApiModel>> GetInvoiceLinesByInvoiceIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoiceline",
            "v1",
            $"by-invoice:{id}");

        return await cache.GetOrAddAsync<IReadOnlyList<InvoiceLineApiModel>>(key, async _ =>
        {
            var entities = await LoadByInvoiceIdAsync(id, ct);
            return entities.ToApiModels();
        }, new CacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20),
            Tags = InvoiceLineTags
        }, ct) ?? [];
    }

    public async Task<IReadOnlyList<InvoiceLineApiModel>> GetInvoiceLinesByTrackIdAsync(int id, CancellationToken ct)
    {
        var key = keys.Compose(
            "orders",
            "invoiceline",
            "v1",
            $"by-track:{id}");

        return await cache.GetOrAddAsync<IReadOnlyList<InvoiceLineApiModel>>(key, async _ =>
        {
            var entities = await LoadByTrackIdAsync(id, ct);
            return entities.ToApiModels();
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

        var entity = model.ToEntity();
        db.InvoiceLines.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(InvoiceLineTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateInvoiceLineAsync(InvoiceLineApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.InvoiceLines.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.InvoiceLines.Update(entity);
            await db.SaveChangesAsync(ct);

            // Invalidate cache
            await cache.RemoveByTagAsync(InvoiceLineTags[0], ct);
            var key = keys.Compose(
                "orders",
                "invoiceline",
                "v1",
                $"by-id:{model.Id}");
            await cache.RemoveAsync(key, ct);
        }

        return updated;
    }

    private async Task<List<InvoiceLine>> LoadByInvoiceIdAsync(int id, CancellationToken ct)
    {
        return await db.InvoiceLines.Where(a => a.InvoiceId == id)
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<List<InvoiceLine>> LoadByTrackIdAsync(int id, CancellationToken ct)
    {
        return await db.InvoiceLines.Where(a => a.TrackId == id)
            .AsNoTracking().ToListAsync(ct);
    }

    private async Task<InvoiceLine?> LoadByIdAsync(int id, CancellationToken ct)
    {
        return await db.InvoiceLines
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, ct);
    }
}
