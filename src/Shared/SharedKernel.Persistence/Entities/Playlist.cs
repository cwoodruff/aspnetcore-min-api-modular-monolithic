using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Entities;

public partial class Playlist
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public ICollection<Track> Tracks { get; set; } = new List<Track>();

    public PlaylistApiModel Convert() =>
        new()
        {
            Id = Id,
            Name = Name
        };
}
