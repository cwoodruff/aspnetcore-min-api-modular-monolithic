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

public class EmployeeServiceTests
{
    private readonly IEmployeeRepository _repo = Substitute.For<IEmployeeRepository>();
    private readonly ICacheFacade _cache = Substitute.For<ICacheFacade>();
    private readonly ICacheKeyComposer _keys = Substitute.For<ICacheKeyComposer>();
    private readonly IValidator<EmployeeApiModel> _validator = Substitute.For<IValidator<EmployeeApiModel>>();
    private readonly ILogger<EmployeeService> _logger = NullLogger<EmployeeService>.Instance;
    private readonly EmployeeService _service;

    public EmployeeServiceTests()
    {
        // Default successful validation
        _validator.ValidateAsync(Arg.Any<EmployeeApiModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult()));

        _service = new EmployeeService(_repo, _cache, _keys, _validator, _logger);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(global::ModularMonolith.Services.Tests.TestCacheKeys.FromComposeCall);
    }

    [Fact]
    public async Task CreateEmployeeAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        // Arrange
        var model = new EmployeeApiModel { FirstName = "" };
        var ct = CancellationToken.None;
        
        _validator.ValidateAsync(Arg.Any<EmployeeApiModel>(), ct)
            .Returns(Task.FromResult(new FluentValidation.Results.ValidationResult(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("FirstName", "FirstName is required") 
            })));

        // Act & Assert
        await _service.Invoking(s => s.CreateEmployeeAsync(model, ct))
            .Should().ThrowAsync<ValidationException>();
        await _repo.DidNotReceive().Add(Arg.Any<Employee>());
    }

    [Fact]
    public async Task CreateEmployeeAsync_ShouldAddAndInvalidateCache()
    {
        // Arrange
        var model = new EmployeeApiModel { FirstName = "Andrew", LastName = "Adams" };
        var ct = CancellationToken.None;
        var created = new Employee { Id = 10, FirstName = "Andrew", LastName = "Adams" };
        _repo.Add(Arg.Any<Employee>()).Returns(created);

        // Act
        var result = await _service.CreateEmployeeAsync(model, ct);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Andrew");
        await _repo.Received(1).Add(Arg.Is<Employee>(e => e != null && e.FirstName == "Andrew"));
        await _cache.Received(1).RemoveByTagAsync("administration:employee", ct);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_ShouldUpdateAndInvalidateCache()
    {
        // Arrange
        var model = new EmployeeApiModel { Id = 1, FirstName = "Andrew", LastName = "Adams" };
        var ct = CancellationToken.None;
        _repo.Update(Arg.Any<Employee>()).Returns(true);

        // Act
        var result = await _service.UpdateEmployeeAsync(model, ct);

        // Assert
        result.Should().BeTrue();
        await _repo.Received(1).Update(Arg.Is<Employee>(e => e != null && e.Id == 1 && e.FirstName == "Andrew"));
        await _cache.Received(1).RemoveByTagAsync("administration:employee", ct);
        await _cache.Received(1).RemoveAsync(Arg.Any<CacheKey>(), ct);
    }

    [Fact]
    public async Task GetEmployeeByIdAsync_ShouldReturnFromCache()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var expected = new EmployeeApiModel { Id = id, FirstName = "Andrew" };
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<EmployeeApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(expected);

        // Act
        var result = await _service.GetEmployeeByIdAsync(id, ct);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetDirectReportsAsync_ShouldReturnMappedList()
    {
        // Arrange
        var id = 1;
        var ct = CancellationToken.None;
        var entities = new List<Employee> { new() { Id = 2, FirstName = "Nancy" } };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<IEnumerable<EmployeeApiModel>>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<IEnumerable<EmployeeApiModel>>>>(1);
                return await factory(ct);
            });

        _repo.GetDirectReports(id).Returns(entities);

        // Act
        var result = await _service.GetDirectReportsAsync(id, ct);

        // Assert
        result.Should().HaveCount(1);
        result.First().FirstName.Should().Be("Nancy");
    }

    [Fact]
    public async Task GetReportsToAsync_ShouldReturnManager()
    {
        // Arrange
        var id = 2;
        var ct = CancellationToken.None;
        var manager = new Employee { Id = 1, FirstName = "Andrew" };
        
        _cache.GetOrAddAsync(Arg.Any<CacheKey>(), Arg.Any<Func<CancellationToken, Task<EmployeeApiModel?>>>(), Arg.Any<CacheEntryOptions>(), ct)
            .Returns(async callInfo => 
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<EmployeeApiModel?>>>(1);
                return await factory(ct);
            });

        _repo.GetReportsTo(id).Returns(manager);

        // Act
        var result = await _service.GetReportsToAsync(id, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.FirstName.Should().Be("Andrew");
    }
}
