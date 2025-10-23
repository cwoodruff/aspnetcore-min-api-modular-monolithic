using SharedKernel.Persistence.Converters;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.ApiModels;

public sealed class MediaTypeApiModel : BaseApiModel, IConvertModel<MediaType>
{
    public string? Name { get; set; }

    public ICollection<TrackApiModel> Tracks { get; set; } = new List<TrackApiModel>();

    public MediaType Convert() =>
        new()
        {
            Id = Id,
            Name = Name
        };
}