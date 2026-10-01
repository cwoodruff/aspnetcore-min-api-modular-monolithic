using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Contracts.Events;
using Orders.Modules.Data;
using SharedKernel;
using SharedKernel.Concurrency;
using SharedKernel.Diagnostics;
using SharedKernel.Events;

namespace Orders.Modules.Events;

/// <summary>Delivers orders.OutboxMessage to the handlers other modules register (ADR-0008).</summary>
internal sealed class OrdersOutboxDispatcher(
    IServiceScopeFactory scopes,
    IEnumerable<IntegrationEventSubscription> subscriptions,
    [FromKeyedServices(ModuleJson.OptionsKey)] JsonSerializerOptions json,
    IConfiguration configuration,
    TimeProvider time,
    [FromKeyedServices(OrdersModule.ModuleName)] ModuleWorkQueue workQueue,
    [FromKeyedServices(OrdersModule.ModuleName)] ModuleMeter meter,
    ILogger<OrdersOutboxDispatcher> logger)
    : OutboxDispatcher<OrdersDbContext>(scopes, subscriptions, json, configuration, time, workQueue, meter, logger)
{
    protected override IReadOnlyCollection<Type> EventTypes { get; } = [typeof(InvoiceFinalized)];
}
