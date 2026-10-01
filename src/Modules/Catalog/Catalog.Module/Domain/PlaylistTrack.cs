namespace Catalog.Modules.Domain;

internal sealed class PlaylistTrack
{
    public int PlaylistId { get; set; }

    public int TrackId { get; set; }

    public Playlist Playlist { get; set; } = null!;

    public Track Track { get; set; } = null!;
}
