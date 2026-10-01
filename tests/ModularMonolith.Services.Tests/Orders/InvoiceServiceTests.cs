using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Modules.Data;
using Orders.Modules.Models;
using Orders.Modules.Services;
using Orders.Modules.Validation;

namespace ModularMonolith.Services.Tests.Orders;

[Collection(ModuleDatabaseDefinition.Name)]
public sealed class InvoiceServiceTests(ModuleDatabaseFixture database) : IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private OrdersDbContext _db = null!;
    private InvoiceService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateOrdersContext();
        _service = new InvoiceService(_db, _cache, RecordingCache.Keys(), new InvoiceValidator(),
            NullLogger<InvoiceService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static InvoiceApiModel ValidInvoice(int id = 0) => new()
    {
        Id = id, CustomerId = TestData.Customer, Total = 1.98m, InvoiceDate = new DateTime(2024, 1, 1),
        BillingAddress = "A", BillingCity = "C", BillingCountry = "Co", BillingState = "S", BillingPostalCode = "12345"
    };

    [Fact]
    public async Task CreateInvoiceAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        var model = ValidInvoice();
        model.Total = -1m;

        await _service.Invoking(s => s.CreateInvoiceAsync(model, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        await using var check = database.CreateOrdersContext();
        (await check.Invoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateInvoiceAsync_ShouldAddAndInvalidateCache()
    {
        // InvoiceDate binds as DateTimeKind.Unspecified, as it does from JSON; it is stored as UTC.
        var result = await _service.CreateInvoiceAsync(ValidInvoice(), CancellationToken.None);

        result.Should().NotBeNull();
        await using var check = database.CreateOrdersContext();
        var stored = await check.Invoices.SingleAsync(i => i.Id == result!.Id);
        stored.InvoiceDate.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _cache.RemovedTags.Should().Equal("orders:invoice");
    }

    [Fact]
    public async Task UpdateInvoiceAsync_ShouldUpdateAndInvalidateCache()
    {
        var model = ValidInvoice(TestData.Invoice);
        model.Total = 3.50m;

        var result = await _service.UpdateInvoiceAsync(model, CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateOrdersContext();
        (await check.Invoices.SingleAsync(i => i.Id == TestData.Invoice)).Total.Should().Be(3.50m);
        _cache.RemovedTags.Should().Equal("orders:invoice");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task GetInvoiceByIdAsync_ShouldReturnFromCache()
    {
        var result = (InvoiceApiModel?)await _service.GetInvoiceByIdAsync(TestData.Invoice, CancellationToken.None);

        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(TestData.Customer, "the customer is referenced by id only");
        result.InvoiceLines.Select(l => l.TrackId).Should().BeEquivalentTo(new int?[] { TestData.Track1, TestData.Track2 });
    }

    [Fact]
    public async Task GetInvoicesByCustomerIdAsync_ShouldReturnMappedList()
    {
        (await _service.GetInvoicesByCustomerIdAsync(TestData.Customer, CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task GetInvoicesByCustomerIdAsync_ShouldReturnEmptyForUnknownCustomer()
    {
        (await _service.GetInvoicesByCustomerIdAsync(TestData.Unknown, CancellationToken.None)).Should().BeEmpty();
    }
}
