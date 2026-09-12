using FluentAssertions;
using Identity.Modules.Extensions;
using Identity.Modules.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModularMonolith.Api.Tests;

/// <summary>
///     Tests for the startup diagnostics that report which Identity:InMemoryUsers entries were loaded
///     and which were ignored because they are incomplete.
/// </summary>
public class InMemoryUserStoreDiagnosticsTests
{
    private const int IncompleteEntryEventId = 2006;
    private const int IgnoredIncompleteSummaryEventId = 2004;
    private const int NoValidEntriesEventId = 2003;
    private const int EffectiveUserEventId = 2007;

    [Fact]
    public async Task StartAsync_ShouldWarnWithIndexAndMissingField_WhenEntryIsIncomplete()
    {
        var logger = await RunDiagnosticsAsync(
            "Development",
            CompleteUser("demo", "user-1"),
            new InMemoryUserRecord { Username = "broken", UserId = "user-2" });

        var warning = logger.Records.Single(record => record.EventId == IncompleteEntryEventId);
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Message.Should().Contain("Identity:InMemoryUsers:1");
        warning.Message.Should().Contain(nameof(InMemoryUserRecord.Password));
    }

    [Fact]
    public async Task StartAsync_ShouldListEveryMissingField_WhenEntryIsMissingMoreThanOne()
    {
        var logger = await RunDiagnosticsAsync(
            "Development",
            CompleteUser("demo", "user-1"),
            new InMemoryUserRecord { Password = "  " });

        var warning = logger.Records.Single(record => record.EventId == IncompleteEntryEventId);
        warning.Message.Should().Contain(nameof(InMemoryUserRecord.Username));
        warning.Message.Should().Contain(nameof(InMemoryUserRecord.Password));
        warning.Message.Should().Contain(nameof(InMemoryUserRecord.UserId));
    }

    [Fact]
    public async Task StartAsync_ShouldSummarizeValidAndIgnoredCounts_WhenSomeEntriesAreIncomplete()
    {
        var logger = await RunDiagnosticsAsync(
            "Development",
            CompleteUser("demo", "user-1"),
            CompleteUser("admin-demo", "user-2"),
            new InMemoryUserRecord { Username = "broken", UserId = "user-3" });

        var summary = logger.Records.Single(record => record.EventId == IgnoredIncompleteSummaryEventId);
        summary.Level.Should().Be(LogLevel.Warning);
        summary.Message.Should().Contain("Loaded 2 in-memory login users and ignored 1 incomplete");

        logger.Records.Count(record => record.EventId == EffectiveUserEventId).Should().Be(2);
    }

    [Fact]
    public async Task StartAsync_ShouldWarnThatNoEntriesAreUsable_WhenEveryEntryIsIncomplete()
    {
        var logger = await RunDiagnosticsAsync(
            "Development",
            new InMemoryUserRecord { Username = "broken", UserId = "user-1" });

        var warning = logger.Records.Single(record => record.EventId == NoValidEntriesEventId);
        warning.Level.Should().Be(LogLevel.Warning);
        warning.Message.Should().Contain("none of the 1 configured Identity:InMemoryUsers entries are usable");

        logger.Records.Should().NotContain(record => record.EventId == EffectiveUserEventId);
    }

    [Fact]
    public async Task StartAsync_ShouldStillNameEachIncompleteEntry_WhenNoEntryIsValid()
    {
        var logger = await RunDiagnosticsAsync(
            "Development",
            new InMemoryUserRecord { Username = "broken", UserId = "user-1" },
            new InMemoryUserRecord { Password = "T-not-a-real-password!Aa1" });

        var warnings = logger.Records.Where(record => record.EventId == IncompleteEntryEventId).ToArray();
        warnings.Should().HaveCount(2);
        warnings[0].Message.Should().Contain("Identity:InMemoryUsers:0");
        warnings[0].Message.Should().Contain(nameof(InMemoryUserRecord.Password));
        warnings[1].Message.Should().Contain("Identity:InMemoryUsers:1");
        warnings[1].Message.Should().Contain(nameof(InMemoryUserRecord.Username));

        // The aggregate "loaded X, ignored Y" summary stays tied to there being at least one valid entry.
        logger.Records.Should().NotContain(record => record.EventId == IgnoredIncompleteSummaryEventId);
    }

    [Fact]
    public async Task StartAsync_ShouldNotWarn_WhenEveryEntryIsComplete()
    {
        var logger = await RunDiagnosticsAsync(
            "Development",
            CompleteUser("demo", "user-1"),
            CompleteUser("admin-demo", "user-2"));

        logger.Records.Should().NotContain(record => record.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task StartAsync_ShouldNotReportIncompleteEntries_OutsideDevelopmentOrDemo()
    {
        var logger = await RunDiagnosticsAsync(
            "Production",
            CompleteUser("demo", "user-1"),
            new InMemoryUserRecord { Username = "broken", UserId = "user-2" });

        logger.Records.Should().NotContain(record => record.EventId == IncompleteEntryEventId);
    }

    private static async Task<CapturingLogger<InMemoryUserStoreDiagnosticsHostedService>> RunDiagnosticsAsync(
        string environmentName, params InMemoryUserRecord[] users)
    {
        var logger = new CapturingLogger<InMemoryUserStoreDiagnosticsHostedService>();
        var options = Options.Create(new InMemoryUserStoreOptions { InMemoryUsers = [.. users] });
        var service = new InMemoryUserStoreDiagnosticsHostedService(
            new TestHostEnvironment(environmentName), options, logger);

        await service.StartAsync(CancellationToken.None);

        return logger;
    }

    private static InMemoryUserRecord CompleteUser(string username, string userId)
    {
        return new InMemoryUserRecord
        {
            Username = username,
            Password = "T-not-a-real-password!Aa1",
            UserId = userId,
            DisplayName = username,
            Roles = ["User"],
            Permissions = ["music.read"],
            Email = $"{username}@example.com",
            Tenant = "tenant-1"
        };
    }

    private sealed record LogRecord(LogLevel Level, int EventId, string Message);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Records.Add(new LogRecord(logLevel, eventId.Id, formatter(state, exception)));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "ModularMonolith.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
