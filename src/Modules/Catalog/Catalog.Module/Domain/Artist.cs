namespace Catalog.Modules.Domain;

internal sealed class Artist
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public ICollection<Album> Albums { get; set; } = new List<Album>();
}
