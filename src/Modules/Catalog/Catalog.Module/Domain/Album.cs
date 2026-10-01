namespace Catalog.Modules.Domain;

internal sealed class Album
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public int? ArtistId { get; set; }

    public Artist? Artist { get; set; }

    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}
