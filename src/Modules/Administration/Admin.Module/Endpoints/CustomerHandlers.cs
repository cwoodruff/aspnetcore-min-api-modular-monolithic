using Admin.Modules.Domain;
using Admin.Modules.Models;
using Admin.Modules.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Admin.Modules.Endpoints;

/// <summary>The customer endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class CustomerHandlers
{
    [Authorize]
    public static async Task<Results<Ok<CustomerApiModel>, NotFound>> GetCustomerById(int id, ICustomerService service, CancellationToken ct)
    {
        var customer = await service.GetCustomerByIdAsync(id, ct);
        return customer is not null ? TypedResults.Ok(customer) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Results<Ok<CustomerPurchasesApiModel>, NotFound>> GetCustomerPurchases(int id, ICustomerService service, CancellationToken ct)
    {
        var purchases = await service.GetCustomerPurchasesAsync(id, ct);
        return purchases is not null ? TypedResults.Ok(purchases) : TypedResults.NotFound();
    }

    [Authorize]
    public static async Task<Ok<IEnumerable<CustomerApiModel>>> GetAllCustomers(ICustomerService service, CancellationToken ct)
    {
        var customers = await service.GetAllCustomersAsync(ct);
        return TypedResults.Ok(customers);
    }

    [Authorize]
    public static async Task<Ok<IEnumerable<CustomerApiModel>>> GetCustomersBySupportRepId(int id, ICustomerService service, CancellationToken ct)
    {
        var customers = await service.GetCustomersBySupportRepIdAsync(id, ct);
        return TypedResults.Ok(customers);
    }
}
