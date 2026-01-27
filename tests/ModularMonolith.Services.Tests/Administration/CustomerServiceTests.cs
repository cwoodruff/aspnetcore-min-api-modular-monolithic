using Admin.Modules.Services;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Caching;
using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Entities;
using SharedKernel.Persistence.Repositories;
using Xunit;

namespace ModularMonolith.Services.Tests.Administration;

public class CustomerServiceTests
{
    private readonly ICustomerRepository _repo = Substitute.For<ICustomerRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _service = new CustomerService(_repo, _cache, _keys);
        
        // Setup default key composition
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
    }

    [Fact]
    public async Task GetCustomerByIdAsync_ShouldReturnFromCache_WhenExists()
    {
        // Arrange
        var customerId = 1;
        var expectedCustomer = new CustomerApiModel { Id = customerId, FirstName = "John", LastName = "Doe" };
        var ct = CancellationToken.None;

        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<CustomerApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expectedCustomer);

        // Act
        var result = await _service.GetCustomerByIdAsync(customerId, ct);

        // Assert
        result.Should().BeEquivalentTo(expectedCustomer);
        await _cache.Received(1).GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<CustomerApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct);
    }

    [Fact]
    public async Task GetAllCustomersAsync_ShouldReturnMappedList()
    {
        // Arrange
        var ct = CancellationToken.None;
        var entities = new List<Customer> 
        { 
            new() { Id = 1, FirstName = "John", LastName = "Doe" },
            new() { Id = 2, FirstName = "Jane", LastName = "Smith" }
        };
        
        // Mock cache to execute the factory
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<CustomerApiModel>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<CustomerApiModel>>>>(1);
                return await factory(ct);
            });

        _repo.GetAll().Returns(entities);

        // Act
        var result = await _service.GetAllCustomersAsync(ct);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.FirstName == "John");
        result.Should().Contain(c => c.FirstName == "Jane");
        await _repo.Received(1).GetAll();
    }

    [Fact]
    public async Task GetCustomersBySupportRepIdAsync_ShouldReturnFilteredList()
    {
        // Arrange
        var repId = 5;
        var ct = CancellationToken.None;
        var entities = new List<Customer> 
        { 
            new() { Id = 1, FirstName = "John", SupportRepId = repId }
        };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<CustomerApiModel>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<CustomerApiModel>>>>(1);
                return await factory(ct);
            });

        _repo.GetBySupportRepId(repId).Returns(entities);

        // Act
        var result = await _service.GetCustomersBySupportRepIdAsync(repId, ct);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(1);
        await _repo.Received(1).GetBySupportRepId(repId);
    }
}
