namespace Catalog.Modules.Models;

internal sealed class PlaylistApiModel
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public ICollection<TrackApiModel> Tracks { get; set; } = new List<TrackApiModel>();
}
