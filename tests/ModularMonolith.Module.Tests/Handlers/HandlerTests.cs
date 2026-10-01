using Admin.Modules.Endpoints;
using Admin.Modules.Services;
using Catalog.Modules.Endpoints;
using Catalog.Modules.Models;
using Catalog.Modules.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Orders.Modules.Endpoints;
using Orders.Modules.Services;
using Reporting.Modules.Data;
using Reporting.Modules.Endpoints;
using SharedKernel.Persistence;
using GenreApiModel = Admin.Modules.Models.GenreApiModel;
using InvoiceApiModel = global::Orders.Modules.Models.InvoiceApiModel;

namespace ModularMonolith.Module.Tests.Handlers;

/// <summary>
///     Endpoint handlers are plain static methods, so they can be called directly: no host, no HTTP, a
///     substituted service. Two per module (Identity's are in Api.Tests, which has its InternalsVisibleTo).
/// </summary>
public sealed class HandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // Catalog

    [Fact]
    public async Task Catalog_GetAlbumById_ReturnsOk_WhenTheServiceFindsIt()
    {
        var service = new FakeAlbumService { Album = new AlbumApiModel { Id = 1, Title = "Album A" } };

        var result = await AlbumHandlers.GetAlbumById(1, service, Ct);

        result.Result.Should().BeOfType<Ok<AlbumApiModel>>().Which.Value!.Title.Should().Be("Album A");
    }

    [Fact]
    public async Task Catalog_GetAlbumById_ReturnsNotFound_WhenTheServiceDoesNot()
    {
        var result = await AlbumHandlers.GetAlbumById(9, new FakeAlbumService(), Ct);

        result.Result.Should().BeOfType<NotFound>();
    }

    // Orders

    [Theory]
    [InlineData((int)FinalizeInvoiceResult.Finalized, typeof(Accepted<InvoiceHandlers.InvoiceFinalizeAccepted>))]
    [InlineData((int)FinalizeInvoiceResult.AlreadyFinalized, typeof(Conflict))]
    [InlineData((int)FinalizeInvoiceResult.NotFound, typeof(NotFound))]
    public async Task Orders_FinalizeInvoice_MapsEachOutcomeToItsStatus(int outcome, Type expected)
    {
        // The enum is internal, so the theory takes its value.
        var result = await InvoiceHandlers.FinalizeInvoice(5, new FakeInvoiceService { Finalize = (FinalizeInvoiceResult)outcome }, Ct);

        result.Result.Should().BeOfType(expected);
        if (result.Result is Accepted<InvoiceHandlers.InvoiceFinalizeAccepted> accepted)
        {
            accepted.Location.Should().Be("/api/orders/invoices/5");
            accepted.Value!.SalesCountersUpdate.Should().Be("eventual");
        }
    }

    [Fact]
    public async Task Orders_GetInvoicesByCustomerId_ReturnsTheServicesList()
    {
        var service = new FakeInvoiceService { ByCustomer = [new InvoiceApiModel { Id = 7 }] };

        var result = await InvoiceHandlers.GetInvoicesByCustomerId(3, service, Ct);

        result.Value.Should().ContainSingle().Which.Id.Should().Be(7);
    }

    // Administration

    [Fact]
    public async Task Administration_DeleteGenre_ReturnsNoContentOrNotFound()
    {
        var service = new FakeGenreService { Existing = 1 };

        (await GenreHandlers.DeleteGenre(1, service, Ct)).Result.Should().BeOfType<NoContent>();
        (await GenreHandlers.DeleteGenre(2, service, Ct)).Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Administration_UpdateGenre_EchoesTheIdAndName()
    {
        var service = new FakeGenreService { Existing = 4 };

        var result = await GenreHandlers.UpdateGenre(4, new GenreEndpoints.UpdateGenreRequest("Jazz"), service, Ct);

        result.Result.Should().BeOfType<Ok<GenreHandlers.GenreUpdated>>()
            .Which.Value.Should().Be(new GenreHandlers.GenreUpdated(4, "Jazz"));
    }

    // Reporting

    [Fact]
    public async Task Reporting_Health_ReturnsTheMinimalBody_OutsideDevelopment()
    {
        var result = ReportingHealthHandlers.Health(Environment("Production"), new ConfigurationBuilder().Build());

        var body = await ExecuteAsync(result);
        body.Should().Contain("\"module\":\"Reporting\"").And.Contain("\"status\":\"Healthy\"").And.NotContain("environment");
    }

    [Fact]
    public async Task Reporting_DataHealth_ReportsDegraded_WhenTheDatabaseIsUnreachable()
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>();
        ModuleDbContextOptions.Use(options, "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1",
            ReportingDbContext.Schema);
        await using var db = new ReportingDbContext(options.Options);

        var result = await ReportingDataHealthHandlers.DataHealth(db, Environment("Production"), new ConfigurationBuilder().Build(), Ct);

        (await ExecuteAsync(result)).Should().Contain("\"status\":\"Degraded\"");
    }

    private static IHostEnvironment Environment(string name)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        env.ContentRootFileProvider.Returns(new NullFileProvider());
        return env;
    }

    // Writes an IResult the way the host would, without a host.
    private static async Task<string> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    // Hand-written fakes: the service interfaces are internal, and a proxying mock library would need the
    // modules to grant it InternalsVisibleTo, which the guardrails allow for one test project only.
    private sealed class FakeAlbumService : IAlbumService
    {
        public AlbumApiModel? Album { get; init; }

        public Task<AlbumApiModel?> GetAlbumByIdAsync(int id, CancellationToken ct) => Task.FromResult(Album?.Id == id ? Album : null);
        public Task<IReadOnlyList<AlbumApiModel>> GetAllAlbumsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AlbumApiModel>> GetAlbumsByArtistIdAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<AlbumApiModel?> CreateAlbumAsync(AlbumApiModel model, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> UpdateAlbumAsync(AlbumApiModel model, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeInvoiceService : IInvoiceService
    {
        public FinalizeInvoiceResult Finalize { get; init; }
        public IReadOnlyList<InvoiceApiModel> ByCustomer { get; init; } = [];

        public Task<FinalizeInvoiceResult> FinalizeInvoiceAsync(int id, CancellationToken ct) => Task.FromResult(Finalize);
        public Task<IReadOnlyList<InvoiceApiModel>> GetInvoicesByCustomerIdAsync(int id, CancellationToken ct) => Task.FromResult(ByCustomer);
        public Task<InvoiceApiModel?> GetInvoiceByIdAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<InvoiceApiModel>> GetAllInvoicesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<InvoiceApiModel?> CreateInvoiceAsync(InvoiceApiModel model, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> UpdateInvoiceAsync(InvoiceApiModel model, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeGenreService : IGenreService
    {
        public int Existing { get; init; }

        public Task<bool> UpdateGenreAsync(int id, string name, CancellationToken ct) => Task.FromResult(id == Existing);
        public Task<bool> DeleteGenreAsync(int id, CancellationToken ct) => Task.FromResult(id == Existing);
        public Task<GenreApiModel?> GetGenreByIdAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<GenreApiModel>> GetAllGenresAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<GenreApiModel?> CreateGenreAsync(string name, CancellationToken ct) => throw new NotSupportedException();
    }
}
