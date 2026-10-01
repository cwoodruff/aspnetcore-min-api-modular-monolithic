namespace Reporting.Modules.Domain;

/// <summary>
/// A row in another module's table whose cross-module reference points at nothing (ADR-0015). Recorded,
/// never repaired; resolved when the reference exists again or the source row is gone.
/// </summary>
internal sealed class IntegrityFinding
{
    public Guid Id { get; set; }

    public string CheckName { get; set; } = string.Empty;

    public string SourceSchema { get; set; } = string.Empty;

    public string SourceTable { get; set; } = string.Empty;

    public int SourceId { get; set; }

    /// <summary>For example "catalog.Track 42".</summary>
    public string MissingReference { get; set; } = string.Empty;

    public DateTimeOffset DetectedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
