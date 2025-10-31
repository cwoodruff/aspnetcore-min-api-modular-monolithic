using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public partial class MediaType : BaseEntity, IConvertModel<MediaTypeApiModel>
{
    public string? Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();

    public MediaTypeApiModel Convert() =>
        new()
        {
            Id = Id,
            Name = Name
        };
}
