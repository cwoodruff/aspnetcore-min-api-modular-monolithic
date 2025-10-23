using SharedKernel.Persistence.Converters;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.ApiModels;

public sealed class PlaylistApiModel : BaseApiModel, IConvertModel<Playlist>
{
    public string? Name { get; set; }

    public ICollection<TrackApiModel> Tracks { get; set; } = new List<TrackApiModel>();

    public Playlist Convert() =>
        new()
        {
            Id = Id,
            Name = Name
        };
}