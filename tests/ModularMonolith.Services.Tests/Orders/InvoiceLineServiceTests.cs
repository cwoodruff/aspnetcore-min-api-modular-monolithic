using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orders.Modules.Services;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Orders;

public class InvoiceLineServiceTests
{
    private readonly IInvoiceLineRepository _repo = Substitute.For<IInvoiceLineRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly IValidator<InvoiceLineApiModel> _validator = Substitute.For<IValidator<InvoiceLineApiModel>>();
    private readonly ILogger<InvoiceLineService> _logger = NullLogger<InvoiceLineService>.Instance;
    private readonly InvoiceLineService _service;

    public InvoiceLineServiceTests()
    {
        // Default successful validation
        _validator.ValidateAsync(Arg.Any<InvoiceLineApiModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult()));

        _service = new InvoiceLineService(_repo, _cache, _keys, _validator, _logger);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(global::ModularMonolith.Services.Tests.TestCacheKeys.FromComposeCall);
    }

    [Fact]
    public async Task CreateInvoiceLineAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        // Arrange
        var model = new InvoiceLineApiModel { InvoiceId = 1, TrackId = 1, UnitPrice = -1m }; // Invalid unit price
        var ct = CancellationToken.None;
        
        _validator.ValidateAsync(Arg.Any<InvoiceLineApiModel>(), ct)
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("UnitPrice", "UnitPrice must be greater than 0") 
            })));

        // Act & Assert
        await _service.Invoking(s => s.CreateInvoiceLineAsync(model, ct))
            .Should().ThrowAsync<ValidationException>();
        await _repo.DidNotReceive().Add(Arg.Any<InvoiceLine>());
    }

    [Fact]
    public async Task CreateInvoiceLineAsync_ShouldAddAndInvalidateCache()
    {
        // Arrange
        var model = new InvoiceLineApiModel { InvoiceId = 1, TrackId = 1, UnitPrice = 0.99m, Quantity = 1 };
        var ct = CancellationToken.None;
        var created = new InvoiceLine { Id = 10, InvoiceId = 1, TrackId = 1, UnitPrice = 0.99m };
        _repo.Add(Arg.Any<InvoiceLine>()).Returns(created);

        // Act
        var result = await _service.CreateInvoiceLineAsync(model, ct);

        // Assert
        result.Should().NotBeNull();
        result!.UnitPrice.Should().Be(0.99m);
        await _repo.Received(1).Add(Arg.Is<InvoiceLine>(il => il != null && il.UnitPrice == 0.99m));
        await _cache.Received(1).RemoveByTagAsync("orders:invoiceline", ct);
    }

    [Fact]
    public async Task UpdateInvoiceLineAsync_ShouldUpdateAndInvalidateCache()
    {
        // Arrange
        var model = new InvoiceLineApiModel { Id = 1, InvoiceId = 1, TrackId = 1, UnitPrice = 0.99m, Quantity = 1 };
        var ct = CancellationToken.None;
        _repo.Update(Arg.Any<InvoiceLine>()).Returns(true);

        // Act
        var result = await _service.UpdateInvoiceLineAsync(model, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Update(Arg.Is<InvoiceLine>(il => il != null && il.Id == 1 && il.UnitPrice == 0.99m));
        await _cache.Received(1).RemoveByTagAsync("orders:invoiceline", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
    }

    [Fact]
    public async Task GetInvoiceLineByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new InvoiceLineApiModel { Id = id, InvoiceId = 1, TrackId = 1, UnitPrice = 0.99m };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<object?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetInvoiceLineByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetInvoiceLinesByInvoiceIdAsync_ShouldReturnMappedList()
    {
        // Arrange
        var invoiceId = 1;
        var ct = CancellationToken.None;
        var entities = new List<InvoiceLine> { new() { Id = 1, InvoiceId = invoiceId, TrackId = 1, UnitPrice = 0.99m } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<object>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<object>>>>(1);
                return await factory(ct);
            });

        _repo.GetByInvoiceId(invoiceId).Returns(entities);

        // Act
        var result = await _service.GetInvoiceLinesByInvoiceIdAsync(invoiceId, ct);

        // Assert
        result.Should().HaveCount(1);
        var first = result.First() as InvoiceLineApiModel;
        first.Should().NotBeNull();
        first!.UnitPrice.Should().Be(0.99m);
    }
}
