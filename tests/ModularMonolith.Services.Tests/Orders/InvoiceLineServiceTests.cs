using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Modules.Data;
using Orders.Modules.Domain;
using Orders.Modules.Models;
using Orders.Modules.Services;
using Orders.Modules.Validation;

namespace ModularMonolith.Services.Tests.Orders;

[Collection(ModuleDatabaseDefinition.Name)]
public sealed class InvoiceLineServiceTests(ModuleDatabaseFixture database) : IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private OrdersDbContext _db = null!;
    private InvoiceLineService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateOrdersContext();
        _service = new InvoiceLineService(_db, _cache, RecordingCache.Keys(), new InvoiceLineValidator(),
            NullLogger<InvoiceLineService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static InvoiceLineApiModel ValidLine(int id = 0) => new()
    {
        Id = id, InvoiceId = TestData.Invoice, TrackId = TestData.Track1, UnitPrice = 0.99m, Quantity = 1
    };

    [Fact]
    public async Task CreateInvoiceLineAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        var model = ValidLine();
        model.Quantity = 0;

        await _service.Invoking(s => s.CreateInvoiceLineAsync(model, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        await using var check = database.CreateOrdersContext();
        (await check.InvoiceLines.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CreateInvoiceLineAsync_ShouldAddAndInvalidateCache()
    {
        var result = await _service.CreateInvoiceLineAsync(ValidLine(), CancellationToken.None);

        result.Should().NotBeNull();
        await using var check = database.CreateOrdersContext();
        (await check.InvoiceLines.CountAsync(l => l.InvoiceId == TestData.Invoice)).Should().Be(3);
        _cache.RemovedTags.Should().Equal("orders:invoiceline");
    }

    [Fact]
    public async Task UpdateInvoiceLineAsync_ShouldUpdateAndInvalidateCache()
    {
        var model = ValidLine(1);
        model.Quantity = 5;

        var result = await _service.UpdateInvoiceLineAsync(model, CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateOrdersContext();
        (await check.InvoiceLines.SingleAsync(l => l.Id == 1)).Quantity.Should().Be(5);
        _cache.RemovedTags.Should().Equal("orders:invoiceline");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task GetInvoiceLineByIdAsync_ShouldReturnFromCache()
    {
        // The service returns the entity for this lookup, as the repository it replaced did.
        var result = (InvoiceLine?)await _service.GetInvoiceLineByIdAsync(1, CancellationToken.None);

        result.Should().NotBeNull();
        result!.TrackId.Should().Be(TestData.Track1);
    }

    [Fact]
    public async Task GetInvoiceLinesByInvoiceIdAsync_ShouldReturnMappedList()
    {
        (await _service.GetInvoiceLinesByInvoiceIdAsync(TestData.Invoice, CancellationToken.None)).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetInvoiceLinesByTrackIdAsync_ShouldReturnLines()
    {
        (await _service.GetInvoiceLinesByTrackIdAsync(TestData.Track2, CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task UnknownIds_ShouldReturnEmpty()
    {
        (await _service.GetInvoiceLinesByInvoiceIdAsync(TestData.Unknown, CancellationToken.None)).Should().BeEmpty();
        (await _service.GetInvoiceLinesByTrackIdAsync(TestData.Unknown, CancellationToken.None)).Should().BeEmpty();
    }
}
