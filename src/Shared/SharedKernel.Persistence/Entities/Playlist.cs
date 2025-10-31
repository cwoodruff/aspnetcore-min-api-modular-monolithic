using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public partial class Playlist : BaseEntity, IConvertModel<PlaylistApiModel>
{
    public string? Name { get; set; }

    public ICollection<Track> Tracks { get; set; } = new List<Track>();

    public PlaylistApiModel Convert() =>
        new()
        {
            Id = Id,
            Name = Name
        };
}
