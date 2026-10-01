using Identity.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

internal static class OutboxEndpoints
{
    public static void MapOutboxEndpoints(this IEndpointRouteBuilder group)
    {
        // GET /api/orders/outbox/dead-letters
        group.MapGet("/outbox/dead-letters", OutboxHandlers.ListDeadLetters)
            .RequireAuthorization(Policies.Admin)
            .WithName("OrdersListDeadLetters")
            .WithDescription("Outbox messages whose delivery failed six times. Each stays here until retried.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy

        // POST /api/orders/outbox/dead-letters/{id}/retry
        group.MapPost("/outbox/dead-letters/{id:guid}/retry", OutboxHandlers.RetryDeadLetter)
            .RequireAuthorization(Policies.Admin)
            .WithName("OrdersRetryDeadLetter")
            .WithDescription("Resets the attempt count so the dispatcher delivers the message again on its next poll.")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Orders")
            .Produces(429); // Rate limited by the module group's policy
    }
}
