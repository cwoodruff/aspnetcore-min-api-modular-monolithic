using SharedKernel.Persistence.Converters;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.ApiModels;

public sealed class ArtistApiModel : BaseApiModel, IConvertModel<Artist>
{
    public string? Name { get; set; }

    public ICollection<AlbumApiModel> Albums { get; set; } = new List<AlbumApiModel>();

    public Artist Convert() =>
        new()
        {
            Id = Id,
            Name = Name ?? string.Empty
        };
}
