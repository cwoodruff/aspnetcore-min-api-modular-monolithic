using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

internal static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/orders/invoices/{id}
        group.MapGet("/invoices/{id:int}", InvoiceHandlers.GetInvoiceById)
            .RequireAuthorization(Permissions.OrdersRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("OrdersGetInvoiceById")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/orders/invoices
        group.MapGet("invoices/", InvoiceHandlers.GetAllInvoices)
            .RequireAuthorization(Permissions.OrdersRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetAllInvoices")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy

        // GET /api/orders/invoices/customer/{id}
        group.MapGet("invoices/customer/{id:int}", InvoiceHandlers.GetInvoicesByCustomerId)
            .RequireAuthorization(Permissions.OrdersRead).RequireAuthorization(Policies.TenantScoped)
            .WithName("GetInvoicesByCustomerId")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy

        // POST /api/orders/invoices/{id}/finalize
        group.MapPost("/invoices/{id:int}/finalize", InvoiceHandlers.FinalizeInvoice)
            .RequireAuthorization(Permissions.OrdersWrite).RequireAuthorization(Policies.TenantScoped)
            .WithName("OrdersFinalizeInvoice")
            .WithDescription(
                "Finalizes a draft invoice and publishes InvoiceFinalized through the orders outbox in the same " +
                "transaction. Returns 202: the invoice is final now, but track sales and the customer's purchase " +
                "summary are updated eventually, after the outbox is dispatched (salesCountersUpdate: \"eventual\"). " +
                "Do not read them back expecting this invoice to be counted yet.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy
    }
}
