namespace Catalog.Modules.Domain;

internal sealed class Track
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int? AlbumId { get; set; }

    public int? MediaTypeId { get; set; }

    public int? GenreId { get; set; }

    public string? Composer { get; set; }

    public int? Milliseconds { get; set; }

    public int? Bytes { get; set; }

    public decimal? UnitPrice { get; set; }

    public Album? Album { get; set; }

    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();

    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
