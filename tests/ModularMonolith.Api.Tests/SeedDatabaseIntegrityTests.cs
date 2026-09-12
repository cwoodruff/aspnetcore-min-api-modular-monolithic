using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Asserts that the seed database the host opens is still stock Chinook.
/// </summary>
/// <remarks>
///     <para>
///         <c>src/ModularMonolith.Api/data/chinook.db</c> is committed, and the application can write to
///         it — the Genre endpoints are a live write surface. Nothing currently asserts what the file
///         should contain, so a test or a manual probe that writes to it can be committed unnoticed.
///     </para>
///     <para>
///         That is not hypothetical. The repository used to track a second copy at its root which had
///         exactly that happen — eight leftover rows, including <c>Genre</c> 1 renamed from
///         <c>Rock</c> to <c>Updated_&lt;guid&gt;</c>. It has since been removed and the leak that
///         fed it closed; see the note on <see cref="TheRepositoryRootCarriesNoSecondCopy" />.
///     </para>
///     <para>
///         A failure here means the committed database has been modified. Restore it rather than
///         updating the expectations, unless the seed genuinely changed on purpose.
///     </para>
/// </remarks>
public class SeedDatabaseIntegrityTests
{
    /// <summary>Every table, with the row count stock Chinook has.</summary>
    public static readonly TheoryData<string, long> Census = new()
    {
        { "Album", 347 },
        { "Artist", 275 },
        { "Customer", 59 },
        { "Employee", 8 },
        { "Genre", 25 },
        { "Invoice", 458 },
        { "InvoiceLine", 2662 },
        { "MediaType", 5 },
        { "Playlist", 18 },
        { "PlaylistTrack", 8715 },
        { "Track", 3503 }
    };

    /// <summary>
    ///     Name columns a write endpoint could plausibly reach, paired with the shapes a leftover test
    ///     row tends to take.
    /// </summary>
    /// <remarks>
    ///     The eight rows the removed root copy had collected were named <c>Updated_</c>,
    ///     <c>CacheTest_</c>, <c>TestGenre_</c>, <c>Concurrent_</c> and <c>CharsetTest_</c>, each with
    ///     a GUID suffix.
    /// </remarks>
    public static readonly TheoryData<string, string> ArtifactPatterns = new()
    {
        { "Genre", "Name" },
        { "MediaType", "Name" },
        { "Playlist", "Name" },
        { "Artist", "Name" },
        { "Album", "Title" },
        { "Track", "Name" }
    };

    /// <summary>The checksum of the stock database, as committed.</summary>
    /// <remarks>
    ///     Update this <b>only</b> when the seed data is meant to change, and say so in the commit
    ///     message. A surprise failure here means something wrote to the committed file.
    /// </remarks>
    private const string PristineSha256 =
        "bc1185936f4d905a1b435059f0acdf642e70cc1317ecb4c25d5d1ae47142eebb";

    private static readonly string[] LeftoverNamePatterns =
    [
        "Updated\\_%",
        "%Test\\_%",
        "Concurrent\\_%",
        "%Probe%"
    ];

    [Theory]
    [MemberData(nameof(Census))]
    public void EveryTableHoldsExactlyTheStockRows(string table, long expected)
    {
        // Covers every table rather than only Genre, so pollution of any of them is caught if another
        // entity gains a write endpoint later.
        using var connection = OpenSeedDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM \"{table}\"";

        var count = (long)(command.ExecuteScalar() ?? 0L);

        count.Should().Be(
            expected,
            "{0} should hold the stock Chinook row count; a different number means the committed " +
            "database has been written to",
            table);
    }

    [Fact]
    public void TheRowsWritesCanReachStillSayWhatTheyShould()
    {
        // Genre is the only entity with write endpoints, so it is the one that has actually been
        // corrupted in practice.
        using var connection = OpenSeedDatabase();

        ScalarText(connection, "SELECT \"Name\" FROM \"Genre\" WHERE \"Id\" = 1")
            .Should().Be("Rock", "the polluted copy has Genre 1 renamed to Updated_<guid>");

        // This catches rows that were inserted and kept, not ones inserted and deleted again:
        // Genre.Id is a plain integer primary key rather than AUTOINCREMENT, so SQLite hands the
        // freed key straight back. TheCommittedFileIsByteForByteTheStockDatabase covers that case.
        ScalarLong(connection, "SELECT max(\"Id\") FROM \"Genre\"")
            .Should().Be(25, "a higher key means rows were inserted into the committed database");
    }

    [Theory]
    [MemberData(nameof(ArtifactPatterns))]
    public void NoTableCarriesARowThatLooksLikeATestArtifact(string table, string column)
    {
        using var connection = OpenSeedDatabase();

        foreach (var pattern in LeftoverNamePatterns)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT \"{column}\" FROM \"{table}\" WHERE \"{column}\" LIKE $pattern ESCAPE '\\'";
            command.Parameters.AddWithValue("$pattern", pattern);

            var matches = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                matches.Add(reader.GetString(0));
            }

            matches.Should().BeEmpty(
                "{0}.{1} matching {2} means a test or a manual probe wrote to the committed database",
                table,
                column,
                pattern);
        }
    }

    [Fact]
    public void TheCommittedFileIsByteForByteTheStockDatabase()
    {
        // The content checks above miss one case: a row inserted and then deleted again restores
        // every count, and SQLite reuses the freed key, so nothing logical differs. The file bytes
        // still change, and a write that leaves no logical trace is still a write to a committed
        // file — which is the habit worth catching.
        using var stream = File.OpenRead(SeedDatabasePath());
        var digest = Convert.ToHexStringLower(SHA256.HashData(stream));

        digest.Should().Be(
            PristineSha256,
            "data/chinook.db has changed; if that was deliberate update PristineSha256 and say why, " +
            "otherwise restore the file");
    }

    /// <summary>
    ///     The repository used to track a second copy at <c>data/chinook.db</c>, which the host never
    ///     opened and which carried test leftovers. It has been removed, and this keeps it gone.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         That copy was not merely stale — the suite wrote to it on every run, and the isolation
    ///         meant to prevent that never took effect. <c>AddKernelPersistence</c> resolved the
    ///         connection string at registration time, before <c>WebApplicationFactory</c> had applied
    ///         the <c>ConfigureAppConfiguration</c> hook that points each test at its own copy under
    ///         <c>TestData</c>. The value it did see was the relative <c>./data/chinook.db</c> from
    ///         appsettings.json, which resolves to nothing under the test bin directory, so
    ///         <c>ResolveConnectionString</c> fell through to <c>FindUsableDatabasePath()</c> — and the
    ///         first <c>data/chinook.db</c> that walk found was the one at the repository root. The
    ///         per-test copies were created and then never opened.
    ///     </para>
    ///     <para>
    ///         The connection string is now read from the built host's configuration, so those copies
    ///         are used. Removing the root file matters independently of that fix: it is what the
    ///         fallback search finds whenever configuration does not resolve, so putting a database
    ///         back at the repository root would hand any future such gap a tracked file to write to.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheRepositoryRootCarriesNoSecondCopy()
    {
        var rootCopy = Path.Combine(FindRepositoryRoot(), "data", "chinook.db");

        File.Exists(rootCopy).Should().BeFalse(
            "a database at the repository root is what the persistence fallback finds when " +
            "configuration does not resolve, and the copy that used to sit there collected writes " +
            "from every test run");
    }

    private static SqliteConnection OpenSeedDatabase()
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = SeedDatabasePath(),
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());

        connection.Open();
        return connection;
    }

    /// <summary>The copy the host resolves from its content root.</summary>
    private static string SeedDatabasePath()
    {
        return Path.Combine(FindRepositoryRoot(), "src", "ModularMonolith.Api", "data", "chinook.db");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ModularMonolith.Api.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static string ScalarText(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (string)(command.ExecuteScalar() ?? string.Empty);
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (long)(command.ExecuteScalar() ?? 0L);
    }
}
