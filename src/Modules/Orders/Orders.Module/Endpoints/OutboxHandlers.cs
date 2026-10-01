using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Orders.Modules.Domain;
using Orders.Modules.Models;
using Orders.Modules.Services;

namespace Orders.Modules.Endpoints;

/// <summary>The outbox endpoints' handlers: static, typed results, testable without a host.</summary>
internal static class OutboxHandlers
{
    [Authorize]
    public static async Task<Ok<IReadOnlyList<DeadLetter>>> ListDeadLetters(DeadLetterService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.ListAsync(ct));
    }

    [Authorize]
    public static async Task<Results<NoContent, NotFound>> RetryDeadLetter(Guid id, DeadLetterService service, CancellationToken ct)
    {
        return await service.RetryAsync(id, ct) ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
