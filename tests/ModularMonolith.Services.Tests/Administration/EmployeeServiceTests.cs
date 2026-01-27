using Admin.Modules.Services;
using FluentAssertions;
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
    private readonly EmployeeService _service;

    public EmployeeServiceTests()
    {
        _service = new EmployeeService(_repo, _cache, _keys);
        
        _keys.Compose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => new CacheKey("test", "app", (string)callInfo[0], (string)callInfo[1], (string)callInfo[2], null, null, null, (string)callInfo[3]));
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
