using SharedKernel.Persistence.Converters;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.ApiModels;

public sealed class GenreApiModel : BaseApiModel, IConvertModel<Genre>
{
    public string? Name { get; set; }

    public ICollection<TrackApiModel> Tracks { get; set; } = new List<TrackApiModel>();

    public Genre Convert()
    {
        return new Genre
        {
            Id = Id,
            Name = Name
        };
    }
}
