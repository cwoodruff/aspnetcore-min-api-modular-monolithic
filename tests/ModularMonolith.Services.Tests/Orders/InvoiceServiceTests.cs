using FluentAssertions;
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
    private readonly InvoiceService _service;

    public InvoiceServiceTests()
    {
        _service = new InvoiceService(_repo, _cache, _keys);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
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
