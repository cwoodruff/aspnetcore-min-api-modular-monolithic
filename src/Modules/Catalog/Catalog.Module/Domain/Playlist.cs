namespace Catalog.Modules.Domain;

internal sealed class Playlist
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public ICollection<Track> Tracks { get; set; } = new List<Track>();

    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
