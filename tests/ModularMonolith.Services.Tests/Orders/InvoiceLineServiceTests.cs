using FluentAssertions;
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
    private readonly InvoiceLineService _service;

    public InvoiceLineServiceTests()
    {
        _service = new InvoiceLineService(_repo, _cache, _keys);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
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
