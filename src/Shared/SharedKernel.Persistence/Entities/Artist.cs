using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Entities;

public partial class Artist
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Album> Albums { get; set; } = new List<Album>();

    public ArtistApiModel Convert() =>
        new()
        {
            Id = Id,
            Name = Name,
            Albums = Albums.Select(t => t.Convert()).ToList()
        };
}
