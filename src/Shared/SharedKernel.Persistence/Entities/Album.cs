using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public partial class Album : BaseEntity, IConvertModel<AlbumApiModel>
{
    public string? Title { get; set; }

    public int? ArtistId { get; set; }

    public virtual Artist? Artist { get; set; }

    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();

    public AlbumApiModel Convert() =>
        new()
        {
            Id = Id,
            ArtistId = ArtistId,
            Title = Title,
            ArtistName = Artist?.Name
        };
}
