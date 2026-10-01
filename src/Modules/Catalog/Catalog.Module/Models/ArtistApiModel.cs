namespace Catalog.Modules.Models;

internal sealed class ArtistApiModel
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public ICollection<AlbumApiModel> Albums { get; set; } = new List<AlbumApiModel>();
}
