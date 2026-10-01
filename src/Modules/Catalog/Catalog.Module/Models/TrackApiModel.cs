namespace Catalog.Modules.Models;

internal sealed class TrackApiModel
{
    public int Id { get; set; }

    public string? Name { get; set; } = null!;

    public int? AlbumId { get; set; }

    public int? MediaTypeId { get; set; }

    public int? GenreId { get; set; }

    public string? Composer { get; set; }

    public int? Milliseconds { get; set; }

    public int? Bytes { get; set; }

    public decimal? UnitPrice { get; set; }

    public AlbumApiModel? Album { get; set; }

    public ICollection<PlaylistApiModel> Playlists { get; set; } = new List<PlaylistApiModel>();
    public string? AlbumName { get; set; }
}
