using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public class Playlist : BaseEntity, IConvertModel<PlaylistApiModel>
{
    public string? Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();

    public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();

    public PlaylistApiModel Convert()
    {
        return new PlaylistApiModel
        {
            Id = Id,
            Name = Name
        };
    }
}
