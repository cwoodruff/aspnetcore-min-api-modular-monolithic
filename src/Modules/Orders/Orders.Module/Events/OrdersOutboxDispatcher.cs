using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Contracts.Events;
using Orders.Modules.Data;
using SharedKernel.Concurrency;
using SharedKernel.Diagnostics;
using SharedKernel.Events;

namespace Orders.Modules.Events;

/// <summary>Delivers orders.OutboxMessage to the handlers other modules register (ADR-0008).</summary>
internal sealed class OrdersOutboxDispatcher(
    IServiceScopeFactory scopes,
    IEnumerable<IntegrationEventSubscription> subscriptions,
    IConfiguration configuration,
    TimeProvider time,
    [FromKeyedServices(OrdersModule.ModuleName)] ModuleWorkQueue workQueue,
    [FromKeyedServices(OrdersModule.ModuleName)] ModuleMeter meter,
    ILogger<OrdersOutboxDispatcher> logger)
    : OutboxDispatcher<OrdersDbContext>(scopes, subscriptions, configuration, time, workQueue, meter, logger)
{
    protected override IReadOnlyCollection<Type> EventTypes { get; } = [typeof(InvoiceFinalized)];
}
