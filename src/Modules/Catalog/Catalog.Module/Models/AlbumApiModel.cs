namespace Catalog.Modules.Models;

internal sealed class AlbumApiModel
{
    public int Id { get; set; }

    public string? Title { get; set; } = null!;
    public string? ArtistName { get; set; }
    public int? ArtistId { get; set; }
    public ArtistApiModel? Artist { get; set; } = null!;
    public ICollection<TrackApiModel> Tracks { get; set; } = new List<TrackApiModel>();
}
