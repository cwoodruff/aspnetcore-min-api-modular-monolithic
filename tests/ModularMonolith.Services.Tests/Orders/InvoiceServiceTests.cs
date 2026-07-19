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

public class InvoiceServiceTests
{
    private readonly IInvoiceRepository _repo = Substitute.For<IInvoiceRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly IValidator<InvoiceApiModel> _validator = Substitute.For<IValidator<InvoiceApiModel>>();
    private readonly ILogger<InvoiceService> _logger = NullLogger<InvoiceService>.Instance;
    private readonly InvoiceService _service;

    public InvoiceServiceTests()
    {
        // Default successful validation
        _validator.ValidateAsync(Arg.Any<InvoiceApiModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult()));

        _service = new InvoiceService(_repo, _cache, _keys, _validator, _logger);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(global::ModularMonolith.Services.Tests.TestCacheKeys.FromComposeCall);
    }

    [Fact]
    public async Task CreateInvoiceAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        // Arrange
        var model = new InvoiceApiModel { CustomerId = 1, Total = -1m }; // Invalid total
        var ct = CancellationToken.None;
        
        _validator.ValidateAsync(Arg.Any<InvoiceApiModel>(), ct)
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("Total", "Total must be greater than 0") 
            })));

        // Act & Assert
        await _service.Invoking(s => s.CreateInvoiceAsync(model, ct))
            .Should().ThrowAsync<ValidationException>();
        await _repo.DidNotReceive().Add(Arg.Any<Invoice>());
    }

    [Fact]
    public async Task CreateInvoiceAsync_ShouldAddAndInvalidateCache()
    {
        // Arrange
        var model = new InvoiceApiModel { CustomerId = 1, Total = 1.98m, InvoiceDate = DateTime.Now, BillingAddress = "A", BillingCity = "C", BillingCountry = "Co", BillingState = "S", BillingPostalCode = "12345" };
        var ct = CancellationToken.None;
        var created = new Invoice { Id = 10, CustomerId = 1, Total = 1.98m };
        _repo.Add(Arg.Any<Invoice>()).Returns(created);

        // Act
        var result = await _service.CreateInvoiceAsync(model, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Total.Should().Be(1.98m);
        await _repo.Received(1).Add(Arg.Is<Invoice>(i => i != null && i.Total == 1.98m));
        await _cache.Received(1).RemoveByTagAsync("orders:invoice", ct);
    }

    [Fact]
    public async Task UpdateInvoiceAsync_ShouldUpdateAndInvalidateCache()
    {
        // Arrange
        var model = new InvoiceApiModel { Id = 1, CustomerId = 1, Total = 1.98m, InvoiceDate = DateTime.Now, BillingAddress = "A", BillingCity = "C", BillingCountry = "Co", BillingState = "S", BillingPostalCode = "12345" };
        var ct = CancellationToken.None;
        _repo.Update(Arg.Any<Invoice>()).Returns(true);

        // Act
        var result = await _service.UpdateInvoiceAsync(model, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Update(Arg.Is<Invoice>(i => i != null && i.Id == 1 && i.Total == 1.98m));
        await _cache.Received(1).RemoveByTagAsync("orders:invoice", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
    }

    [Fact]
    public async Task GetInvoiceByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new InvoiceApiModel { Id = id, CustomerId = 1, Total = 1.98m };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<object?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetInvoiceByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetInvoicesByCustomerIdAsync_ShouldReturnMappedList()
    {
        // Arrange
        var customerId = 1;
        var ct = CancellationToken.None;
        var entities = new List<Invoice> { new() { Id = 1, CustomerId = customerId, Total = 1.98m } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<object>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<object>>>>(1);
                return await factory(ct);
            });

        _repo.GetByCustomerId(customerId).Returns(entities);

        // Act
        var result = await _service.GetInvoicesByCustomerIdAsync(customerId, ct);

        // Assert
        result.Should().HaveCount(1);
        var first = result.First() as InvoiceApiModel;
        first.Should().NotBeNull();
        first!.Total.Should().Be(1.98m);
    }
}
