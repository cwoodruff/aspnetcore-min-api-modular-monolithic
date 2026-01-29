using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public class MediaType : BaseEntity, IConvertModel<MediaTypeApiModel>
{
    public string? Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();

    public MediaTypeApiModel Convert()
    {
        return new MediaTypeApiModel
        {
            Id = Id,
            Name = Name
        };
    }
}
