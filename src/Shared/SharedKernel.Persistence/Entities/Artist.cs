using SharedKernel.Persistence.ApiModels;
using SharedKernel.Persistence.Converters;

namespace SharedKernel.Persistence.Entities;

public partial class Artist : BaseEntity, IConvertModel<ArtistApiModel>
{
    public string? Name { get; set; }

    public virtual ICollection<Album> Albums { get; set; } = new List<Album>();

    public ArtistApiModel Convert() =>
        new()
        {
            Id = Id,
            Name = Name
        };
}
