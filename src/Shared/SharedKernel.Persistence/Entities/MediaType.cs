using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Entities;

public partial class MediaType
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();

    public MediaTypeApiModel Convert() =>
        new()
        {
            Id = Id,
            Name = Name,
            Tracks = Tracks.Select(t => t.Convert()).ToList()
        };
}
