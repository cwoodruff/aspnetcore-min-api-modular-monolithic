using Catalog.Modules.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reporting.Modules.Data;
using Reporting.Modules.Integrity;

using ModularMonolith.Module.Tests.Hosting;

namespace ModularMonolith.Module.Tests.Reporting;

/// <summary>
///     Reporting's read model and integrity check (ADR-0014, ADR-0015), against PostgreSQL, as the
///     read-only reporting login.
/// </summary>
public sealed class ReportingTests(ReportingFixture database) : IClassFixture<ReportingFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SalesByGenre_SumsTrackSalesPerGenre()
    {
        await using (var catalog = database.CreateCatalogContext())
        {
            catalog.TrackSales.AddRange(
                new TrackSales { TrackId = TestData.Track1, TimesSold = 3 },
                new TrackSales { TrackId = TestData.Track2, TimesSold = 2 });
            await catalog.SaveChangesAsync();
        }

        await using var reporting = database.CreateReportingContext();
        var rows = await reporting.SalesByGenre.ToListAsync();

        var rock = rows.Should().ContainSingle().Subject;
        rock.GenreId.Should().Be(TestData.Genre);
        rock.GenreName.Should().Be("Rock");
        rock.TrackCount.Should().Be(2);
        rock.UnitsSold.Should().Be(5);
    }

    [Fact]
    public async Task InvoiceLinesWithNames_JoinsTrackAndCustomerNames()
    {
        await using var reporting = database.CreateReportingContext();
        var lines = await reporting.InvoiceLinesWithNames
            .Where(line => line.InvoiceId == TestData.Invoice)
            .OrderBy(line => line.InvoiceLineId)
            .ToListAsync();

        lines.Select(line => (line.TrackId, line.TrackName)).Should().Equal(
            (TestData.Track1, "Song 1"), (TestData.Track2, "Song 2"));
        lines.Should().AllSatisfy(line => line.CustomerName.Should().Be("Alice Smith"));
    }

    [Fact]
    public async Task DeletingATrack_ProducesOneFinding_AndARerunAddsNoDuplicate()
    {
        await using (var catalog = database.CreateCatalogContext())
        {
            // Track 2 is on invoice line 2 only: Orders keeps the id, nothing stops the delete (ADR-0005).
            catalog.Tracks.Remove(await catalog.Tracks.SingleAsync(t => t.Id == TestData.Track2));
            await catalog.SaveChangesAsync();
        }

        var first = await Job.RunChecksAsync(CancellationToken.None);
        var second = await Job.RunChecksAsync(CancellationToken.None);

        first.Detected.Should().Be(1);
        second.Detected.Should().Be(0);
        second.Open.Should().Be(1);
        await using var reporting = database.CreateReportingContext();
        var finding = await reporting.IntegrityFindings.SingleAsync();
        finding.CheckName.Should().Be("invoice-line-track");
        finding.SourceSchema.Should().Be("orders");
        finding.SourceTable.Should().Be("InvoiceLine");
        finding.SourceId.Should().Be(2);
        finding.MissingReference.Should().Be("catalog.Track 2");
        finding.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task AFinding_IsResolved_WhenTheReferenceExistsAgain()
    {
        await using (var catalog = database.CreateCatalogContext())
        {
            catalog.Tracks.Remove(await catalog.Tracks.SingleAsync(t => t.Id == TestData.Track2));
            await catalog.SaveChangesAsync();
        }

        await Job.RunChecksAsync(CancellationToken.None);

        await using (var catalog = database.CreateCatalogContext())
        {
            catalog.Tracks.Add(new Track { Id = TestData.Track2, Name = "Song 2", AlbumId = TestData.Album, GenreId = TestData.Genre, MediaTypeId = TestData.MediaType });
            await catalog.SaveChangesAsync();
        }

        var result = await Job.RunChecksAsync(CancellationToken.None);

        result.Resolved.Should().Be(1);
        result.Open.Should().Be(0);
    }

    [Fact]
    public async Task TheReportingLogin_CannotWriteToAnotherModulesTables()
    {
        await using var connection = new NpgsqlConnection(database.ReaderConnectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand("""INSERT INTO catalog."Track" ("Name") VALUES ('should fail')""", connection);

        var failure = await FluentActions.Invoking(() => insert.ExecuteNonQueryAsync()).Should().ThrowAsync<PostgresException>();
        failure.Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public void EveryColumnReportingReads_StillExistsInItsOwnersModel()
    {
        // Reporting's views and checks name other modules' tables and columns in SQL. If a module renames
        // one, this fails here, in Reporting's tests, instead of at the next deploy (ADR-0014).
        using var catalog = database.CreateCatalogContext();
        using var orders = database.CreateOrdersContext();
        using var administration = database.CreateAdministrationContext();

        var missing = MissingColumns([catalog.Model, orders.Model, administration.Model], ReportingSql.References);

        missing.Should().BeEmpty();
        MissingColumns([catalog.Model], [new ColumnReference("catalog", "Track", "Title")])
            .Should().ContainSingle("the check itself must notice a column that does not exist");
    }

    private IntegrityCheckJob Job => database.Host.Services.GetRequiredService<IntegrityCheckJob>();

    private static List<string> MissingColumns(IReadOnlyList<IModel> models, IEnumerable<ColumnReference> references)
    {
        var columns = models
            .SelectMany(model => model.GetEntityTypes())
            .Where(entity => entity.GetTableName() is not null)
            .SelectMany(entity =>
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                return entity.GetProperties().Select(property =>
                    (table.Schema, table.Name, Column: property.GetColumnName(table)));
            })
            .ToHashSet();

        return references
            .Where(reference => !columns.Contains((reference.Schema, reference.Table, reference.Column)))
            .Select(reference => $"{reference.Schema}.{reference.Table}.{reference.Column}")
            .Distinct()
            .ToList();
    }
}
