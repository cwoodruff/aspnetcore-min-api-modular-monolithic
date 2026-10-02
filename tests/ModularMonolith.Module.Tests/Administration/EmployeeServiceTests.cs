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

public sealed class EmployeeServiceTests(AdministrationFixture database) : IClassFixture<AdministrationFixture>, IAsyncLifetime
{
    private readonly RecordingCache _cache = new();
    private AdministrationDbContext _db = null!;
    private EmployeeService _service = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAndSeedAsync();
        _db = database.CreateAdministrationContext();
        _service = new EmployeeService(_db, _cache, RecordingCache.Keys(), new EmployeeValidator(),
            NullLogger<EmployeeService>.Instance);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CreateEmployeeAsync_ShouldThrowValidationException_WhenValidationFails()
    {
        var model = new EmployeeApiModel { FirstName = null, LastName = "Doe" };

        await _service.Invoking(s => s.CreateEmployeeAsync(model, CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        await using var check = database.CreateAdministrationContext();
        (await check.Employees.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CreateEmployeeAsync_ShouldAddAndInvalidateCache()
    {
        var model = new EmployeeApiModel { FirstName = "Ann", LastName = "Lee", ReportsTo = TestData.Manager };

        var result = await _service.CreateEmployeeAsync(model, CancellationToken.None);

        result.Should().NotBeNull();
        await using var check = database.CreateAdministrationContext();
        (await check.Employees.AnyAsync(e => e.Id == result!.Id && e.FirstName == "Ann")).Should().BeTrue();
        _cache.RemovedTags.Should().Equal("administration:employee");
    }

    [Fact]
    public async Task UpdateEmployeeAsync_ShouldUpdateAndInvalidateCache()
    {
        var model = new EmployeeApiModel { Id = TestData.Rep, FirstName = "Johnny", LastName = "Doe", ReportsTo = TestData.Manager };

        var result = await _service.UpdateEmployeeAsync(model, CancellationToken.None);

        result.Should().BeTrue();
        await using var check = database.CreateAdministrationContext();
        (await check.Employees.SingleAsync(e => e.Id == TestData.Rep)).FirstName.Should().Be("Johnny");
        _cache.RemovedTags.Should().Equal("administration:employee");
        _cache.RemovedKeys.Should().ContainSingle();
    }

    [Fact]
    public async Task GetEmployeeByIdAsync_ShouldReturnFromCache()
    {
        var result = await _service.GetEmployeeByIdAsync(TestData.Rep, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be("John");
        result.ReportsTo.Should().Be(TestData.Manager);
    }

    [Fact]
    public async Task GetDirectReportsAsync_ShouldReturnMappedList()
    {
        var reports = (await _service.GetDirectReportsAsync(TestData.Manager, CancellationToken.None)).ToList();

        reports.Should().ContainSingle().Which.Id.Should().Be(TestData.Rep);
    }

    [Fact]
    public async Task GetReportsToAsync_ReturnsTheEmployeesManager()
    {
        var result = await _service.GetReportsToAsync(TestData.Rep, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(TestData.Manager);
    }

    [Theory]
    [InlineData(TestData.Manager)] // reports to no one
    [InlineData(TestData.Unknown)]
    public async Task GetReportsToAsync_ReturnsNull_WithoutAManager(int id) =>
        (await _service.GetReportsToAsync(id, CancellationToken.None)).Should().BeNull();
}
