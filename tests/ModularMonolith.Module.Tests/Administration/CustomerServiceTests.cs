using Admin.Modules.Data;
using Admin.Modules.Models;
using Admin.Modules.Services;
using Admin.Modules.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Administration;

public sealed class CustomerServiceTests(AdministrationFixture database) : IClassFixture<AdministrationFixture>, IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private AdministrationDbContext _db = null!;
    private CustomerService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateAdministrationContext();
        _service = new CustomerService(_db, _cache, RecordingCache.Keys(), new CustomerValidator(),
            NullLogger<CustomerService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CreateCustomerAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        var model = new CustomerApiModel { FirstName = null, LastName = "Doe" };

        await _service.Invoking(s => s.CreateCustomerAsync(model, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        await using var check = database.CreateAdministrationContext();
        (await check.Customers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateCustomerAsync_ShouldAddAndInvalidateCache()
    {
        var model = new CustomerApiModel { FirstName = "John", LastName = "Doe", Email = "john@example.com" };

        var result = await _service.CreateCustomerAsync(model, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be("John");
        await using var check = database.CreateAdministrationContext();
        (await check.Customers.AnyAsync(c => c.Id == result.Id && c.Email == "john@example.com")).Should().BeTrue();
        _cache.RemovedTags.Should().Equal("administration:customer");
    }

    [Fact]
    public async Task UpdateCustomerAsync_ShouldUpdateAndInvalidateCache()
    {
        var model = new CustomerApiModel { Id = TestData.Customer, FirstName = "Alicia", LastName = "Smith" };

        var result = await _service.UpdateCustomerAsync(model, CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateAdministrationContext();
        (await check.Customers.SingleAsync(c => c.Id == TestData.Customer)).FirstName.Should().Be("Alicia");
        _cache.RemovedTags.Should().Equal("administration:customer");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task GetCustomerByIdAsync_ShouldReturnFromCache_WhenExists()
    {
        var result = await _service.GetCustomerByIdAsync(TestData.Customer, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Alice");
        result.SupportRepId.Should().Be(TestData.Rep);
        result.SupportRepName.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetAllCustomersAsync_ShouldReturnMappedList()
    {
        var all = (await _service.GetAllCustomersAsync(CancellationToken.None)).ToList();

        all.Should().ContainSingle().Which.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task GetCustomersBySupportRepIdAsync_ShouldReturnFilteredList()
    {
        var customers = (await _service.GetCustomersBySupportRepIdAsync(TestData.Rep, CancellationToken.None)).ToList();

        customers.Should().ContainSingle().Which.Id.Should().Be(TestData.Customer);
    }

    [Fact]
    public async Task GetCustomersBySupportRepIdAsync_ShouldReturnEmptyForUnknownRep()
    {
        (await _service.GetCustomersBySupportRepIdAsync(TestData.Unknown, CancellationToken.None)).Should().BeEmpty();
    }
}
