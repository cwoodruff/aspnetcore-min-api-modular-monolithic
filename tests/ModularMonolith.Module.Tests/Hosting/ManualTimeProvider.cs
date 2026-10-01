namespace ModularMonolith.Module.Tests.Hosting;

/// <summary>A clock the test moves by hand, to step through retry backoff without waiting.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    // Postgres keeps microseconds; start on a whole second so stored and in-memory times compare equal.
    private DateTimeOffset _now = new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
