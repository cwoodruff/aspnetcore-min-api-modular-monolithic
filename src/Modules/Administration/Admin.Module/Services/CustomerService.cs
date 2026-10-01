using Admin.Modules.Data;
using Admin.Modules.Domain;
using Admin.Modules.Mapping;
using Admin.Modules.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Caching;

namespace Admin.Modules.Services;

internal sealed class CustomerService(
    AdministrationDbContext db,
    [FromKeyedServices(AdministrationModule.ModuleName)] ICacheFacade cache,
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
            await LoadByIdAsync(id, ct)
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
            var customerEntities = await db.Customers.AsNoTracking().ToListAsync(ct);
            return customerEntities.ToApiModels();
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
            var customerEntities = await LoadBySupportRepIdAsync(id, ct);
            return customerEntities.ToApiModels();
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

        var entity = model.ToEntity();
        db.Customers.Add(entity);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync(CustomerTags[0], ct);

        return entity.ToApiModel();
    }

    public async Task<bool> UpdateCustomerAsync(CustomerApiModel model, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(model, ct);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        var entity = model.ToEntity();
        var updated = await db.Customers.AnyAsync(e => e.Id == entity.Id, ct);
        if (updated)
        {
            db.Customers.Update(entity);
            await db.SaveChangesAsync(ct);

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

    /// <returns>Null if the customer does not exist; zeros if no purchase has been counted yet.</returns>
    /// <remarks>Not cached: the summary changes whenever the orders outbox delivers.</remarks>
    public async Task<CustomerPurchasesApiModel?> GetCustomerPurchasesAsync(int id, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == id, ct))
        {
            return null;
        }

        var summary = await db.CustomerPurchaseSummaries.AsNoTracking().SingleOrDefaultAsync(s => s.CustomerId == id, ct);
        return new CustomerPurchasesApiModel(id, summary?.TotalSpent ?? 0m, summary?.InvoiceCount ?? 0, summary?.LastPurchaseAt);
    }

    private async Task<List<Customer>> LoadBySupportRepIdAsync(int id, CancellationToken ct)
    {
        return await db.Customers
            .Where(a => a.SupportRepId == id)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private async Task<CustomerApiModel?> LoadByIdAsync(int id, CancellationToken ct)
    {
        return await db.Customers
            .Where(c => c.Id == id)
            .Select(c => new CustomerApiModel
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                // ... other fields ...
                SupportRepId = c.SupportRepId,
                SupportRepName = c.SupportRep != null ? c.SupportRep.FirstName + " " + c.SupportRep.LastName : null,
                SupportRep = c.SupportRep == null
                    ? null
                    : new EmployeeApiModel
                    {
                        Id = c.SupportRep.Id,
                        FirstName = c.SupportRep.FirstName,
                        LastName = c.SupportRep.LastName,
                        Title = c.SupportRep.Title
                        // No Customers collection here
                    }
            })
            .AsNoTracking()
            .SingleOrDefaultAsync(ct);
    }
}
