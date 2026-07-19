using Admin.Modules.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly IValidator<CustomerApiModel> _validator = Substitute.For<IValidator<CustomerApiModel>>();
    private readonly ILogger<CustomerService> _logger = NullLogger<CustomerService>.Instance;
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        // Default successful validation
        _validator.ValidateAsync(Arg.Any<CustomerApiModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult()));

        _service = new CustomerService(_repo, _cache, _keys, _validator, _logger);
        
        // Setup default key composition
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(global::ModularMonolith.Services.Tests.TestCacheKeys.FromComposeCall);
    }

    [Fact]
    public async Task CreateCustomerAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        // Arrange
        var model = new CustomerApiModel { FirstName = "" };
        var ct = CancellationToken.None;
        
        _validator.ValidateAsync(Arg.Any<CustomerApiModel>(), ct)
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("FirstName", "FirstName is required") 
            })));

        // Act & Assert
        await _service.Invoking(s => s.CreateCustomerAsync(model, ct))
            .Should().ThrowAsync<ValidationException>();
        await _repo.DidNotReceive().Add(Arg.Any<Customer>());
    }

    [Fact]
    public async Task CreateCustomerAsync_ShouldAddAndInvalidateCache()
    {
        // Arrange
        var model = new CustomerApiModel { FirstName = "John", LastName = "Doe" };
        var ct = CancellationToken.None;
        var created = new Customer { Id = 10, FirstName = "John", LastName = "Doe" };
        _repo.Add(Arg.Any<Customer>()).Returns(created);

        // Act
        var result = await _service.CreateCustomerAsync(model, ct);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("John");
        await _repo.Received(1).Add(Arg.Is<Customer>(c => c != null && c.FirstName == "John"));
        await _cache.Received(1).RemoveByTagAsync("administration:customer", ct);
    }

    [Fact]
    public async Task UpdateCustomerAsync_ShouldUpdateAndInvalidateCache()
    {
        // Arrange
        var model = new CustomerApiModel { Id = 1, FirstName = "John", LastName = "Doe" };
        var ct = CancellationToken.None;
        _repo.Update(Arg.Any<Customer>()).Returns(true);

        // Act
        var result = await _service.UpdateCustomerAsync(model, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Update(Arg.Is<Customer>(c => c != null && c.Id == 1 && c.FirstName == "John"));
        await _cache.Received(1).RemoveByTagAsync("administration:customer", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
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
