using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Npgsql;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Asserts that the committed seed script still loads stock Chinook.
/// </summary>
/// <remarks>
///     <para>
///         <c>data/chinook-postgres-seed.sql</c> is what every Development database and every test
///         database starts from. It was generated from the SQLite seed this repository used before
///         phase 1, which had once collected leftover test rows (<c>Genre</c> 1 renamed from
///         <c>Rock</c> to <c>Updated_&lt;guid&gt;</c>, among others). These checks keep that from
///         creeping into the script.
///     </para>
///     <para>
///         The checks run against a fresh clone of the test template, which is loaded from the script
///         by the same <c>DbSeeder</c> the host uses. A failure means the script changed; restore it
///         rather than updating the expectations, unless the seed genuinely changed on purpose.
///     </para>
/// </remarks>
public class SeedDatabaseIntegrityTests
{
    /// <summary>Every table, with the row count stock Chinook has.</summary>
    public static readonly TheoryData<string, long> Census = new()
    {
        { "catalog.\"Album\"", 347 },
        { "catalog.\"Artist\"", 275 },
        { "administration.\"Customer\"", 59 },
        { "administration.\"Employee\"", 8 },
        { "administration.\"Genre\"", 25 },
        { "orders.\"Invoice\"", 458 },
        { "orders.\"InvoiceLine\"", 2662 },
        { "administration.\"MediaType\"", 5 },
        { "catalog.\"Playlist\"", 18 },
        { "catalog.\"PlaylistTrack\"", 8715 },
        { "catalog.\"Track\"", 3503 }
    };

    /// <summary>
    ///     Name columns a write endpoint could plausibly reach, paired with the shapes a leftover test
    ///     row tends to take.
    /// </summary>
    /// <remarks>
    ///     The rows the old SQLite copy had collected were named <c>Updated_</c>, <c>CacheTest_</c>,
    ///     <c>TestGenre_</c>, <c>Concurrent_</c> and <c>CharsetTest_</c>, each with a GUID suffix.
    /// </remarks>
    public static readonly TheoryData<string, string> ArtifactPatterns = new()
    {
        { "administration.\"Genre\"", "Name" },
        { "administration.\"MediaType\"", "Name" },
        { "catalog.\"Playlist\"", "Name" },
        { "catalog.\"Artist\"", "Name" },
        { "catalog.\"Album\"", "Title" },
        { "catalog.\"Track\"", "Name" }
    };

    /// <summary>The checksum of the seed script with line endings normalized to LF, as committed.</summary>
    /// <remarks>
    ///     Update this <b>only</b> when the seed data is meant to change, and say so in the commit
    ///     message.
    /// </remarks>
    private const string PristineSha256 =
        "2931dd5c4386dba191b0d0b9ca73f332a0aad89c8d1a58fdff702b9a79cd5029";

    private static readonly string[] LeftoverNamePatterns =
    [
        "Updated\\_%",
        "%Test\\_%",
        "Concurrent\\_%",
        "%Probe%"
    ];

    // One clone for the whole class; these tests only read.
    private static readonly Lazy<string> SeededDatabase = new(PostgresFixture.CreateSeededDatabase);

    [Theory]
    [MemberData(nameof(Census))]
    public async Task EveryTableHoldsExactlyTheStockRows(string table, long expected)
    {
        await using var connection = await OpenSeededDatabaseAsync();

        var count = await ScalarAsync<long>(connection, $"SELECT count(*) FROM {table}");

        count.Should().Be(expected, "{0} should hold the stock Chinook row count", table);
    }

    [Fact]
    public async Task TheRowsWritesCanReachStillSayWhatTheyShould()
    {
        // Genre is the entity with write endpoints, so it is the one that was corrupted in practice.
        await using var connection = await OpenSeededDatabaseAsync();

        (await ScalarAsync<string>(connection, "SELECT \"Name\" FROM administration.\"Genre\" WHERE \"Id\" = 1"))
            .Should().Be("Rock", "the polluted copy had Genre 1 renamed to Updated_<guid>");

        (await ScalarAsync<int>(connection, "SELECT max(\"Id\") FROM administration.\"Genre\""))
            .Should().Be(25, "a higher key means extra rows were added to the seed");
    }

    [Theory]
    [MemberData(nameof(ArtifactPatterns))]
    public async Task NoTableCarriesARowThatLooksLikeATestArtifact(string table, string column)
    {
        await using var connection = await OpenSeededDatabaseAsync();

        foreach (var pattern in LeftoverNamePatterns)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT \"{column}\" FROM {table} WHERE \"{column}\" LIKE @pattern ESCAPE '\\'", connection);
            command.Parameters.AddWithValue("pattern", pattern);

            var matches = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                matches.Add(reader.GetString(0));
            }

            matches.Should().BeEmpty("{0}.{1} matching {2} means the seed carries a test artifact", table, column, pattern);
        }
    }

    [Fact]
    public async Task IdentitySequencesStartAfterTheSeededIds()
    {
        // The seed inserts explicit ids, so it must move each sequence on; otherwise the first insert
        // through the API collides with an existing row.
        await using var connection = await OpenSeededDatabaseAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var id = await ScalarAsync<int>(connection,
            "INSERT INTO administration.\"Genre\" (\"Name\") VALUES ('sequence check') RETURNING \"Id\"", transaction);

        id.Should().Be(26);
        await transaction.RollbackAsync();
    }

    [Fact]
    public void TheCommittedSeedScriptIsTheStockData()
    {
        var text = File.ReadAllText(PostgresFixture.SeedScriptPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        var actual = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        actual.Should().Be(
            PristineSha256,
            "data/chinook-postgres-seed.sql has changed; if that was deliberate update PristineSha256 and say why");
    }

    private static async Task<NpgsqlConnection> OpenSeededDatabaseAsync()
    {
        var connection = new NpgsqlConnection(SeededDatabase.Value);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
