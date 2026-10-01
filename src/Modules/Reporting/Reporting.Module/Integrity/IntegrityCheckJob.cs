using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reporting.Modules.Data;
using SharedKernel.Concurrency;

namespace Reporting.Modules.Integrity;

/// <summary>
/// Looks for rows whose cross-module reference points at nothing, one check per foreign key removed in
/// phase 2, and records them in reporting.IntegrityFinding (ADR-0015). It repairs nothing. Runs daily at
/// Reporting:Integrity:RunAtUtc (default 02:00) and on demand; every run goes through Reporting's work
/// queue, so two runs never overlap.
/// </summary>
internal sealed partial class IntegrityCheckJob(
    IServiceScopeFactory scopes,
    [FromKeyedServices(ReportingModule.ModuleName)] ModuleWorkQueue workQueue,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<IntegrityCheckJob> logger) : BackgroundService
{
    private const string Findings = "reporting.\"IntegrityFinding\"";

    /// <summary>Runs every check now, after any run already queued.</summary>
    public Task<IntegrityRunResult> RunNowAsync(CancellationToken ct) => workQueue.RunAsync(RunChecksAsync, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Reporting:Integrity:Enabled", true))
        {
            return;
        }

        var runAt = configuration.GetValue("Reporting:Integrity:RunAtUtc", TimeSpan.FromHours(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(UntilNext(time.GetUtcNow(), runAt), time, stoppingToken);
                var result = await RunNowAsync(stoppingToken);
                RunCompleted(logger, result.Detected, result.Resolved, result.Open);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // A failed nightly run is logged; the next night tries again.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                RunFailed(logger, ex);
            }
        }
    }

    internal static TimeSpan UntilNext(DateTimeOffset now, TimeSpan runAtUtc)
    {
        var next = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero) + runAtUtc;
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }

    /// <summary>One run of every check, outside the queue; the hosted loop and RunNowAsync queue it.</summary>
    internal async Task<IntegrityRunResult> RunChecksAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var now = time.GetUtcNow();

        int detected = 0, resolved = 0;
        foreach (var check in ReportingSql.OrphanChecks)
        {
            var source = $"{check.SourceSchema}.\"{check.SourceTable}\"";
            var target = $"{check.TargetSchema}.\"{check.TargetTable}\"";
            var reference = $"s.\"{check.ReferenceColumn}\"";
            var orphan = reference + " IS NOT NULL AND NOT EXISTS (SELECT 1 FROM " + target + " t WHERE t.\"Id\" = " +
                         reference + ")";

            // New orphans; the partial unique index skips ones already open.
            var insert = "INSERT INTO " + Findings +
                         " (\"Id\", \"CheckName\", \"SourceSchema\", \"SourceTable\", \"SourceId\", \"MissingReference\", \"DetectedAt\")" +
                         " SELECT gen_random_uuid(), {0}, {1}, {2}, s.\"Id\", {3} || " + reference + "::text, {4}" +
                         " FROM " + source + " s WHERE " + orphan +
                         " ON CONFLICT (\"CheckName\", \"SourceId\") WHERE \"ResolvedAt\" IS NULL DO NOTHING";
            detected += await db.Database.ExecuteSqlRawAsync(insert,
                [check.Name, check.SourceSchema, check.SourceTable, $"{check.TargetSchema}.{check.TargetTable} ", now], ct);

            // Open findings that no longer hold: the reference exists again, or the source row is gone.
            var resolve = "UPDATE " + Findings + " f SET \"ResolvedAt\" = {1}" +
                          " WHERE f.\"ResolvedAt\" IS NULL AND f.\"CheckName\" = {0}" +
                          " AND NOT EXISTS (SELECT 1 FROM " + source + " s WHERE s.\"Id\" = f.\"SourceId\" AND " + orphan + ")";
            resolved += await db.Database.ExecuteSqlRawAsync(resolve, [check.Name, now], ct);
        }

        var open = await db.IntegrityFindings.CountAsync(f => f.ResolvedAt == null, ct);
        return new IntegrityRunResult(detected, resolved, open, now);
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "Integrity check: {Detected} new findings, {Resolved} resolved, {Open} open")]
    private static partial void RunCompleted(ILogger logger, int detected, int resolved, int open);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Integrity check failed")]
    private static partial void RunFailed(ILogger logger, Exception exception);
}

internal sealed record IntegrityRunResult(int Detected, int Resolved, int Open, DateTimeOffset RanAt);
