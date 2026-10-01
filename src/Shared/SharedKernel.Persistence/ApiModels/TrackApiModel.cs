using SharedKernel.Persistence.Converters;
using SharedKernel.Persistence.Entities;

namespace SharedKernel.Persistence.ApiModels;

public sealed class TrackApiModel : BaseApiModel, IConvertModel<Track>
{
    public string? Name { get; set; } = null!;

    public int? AlbumId { get; set; }

    public int? MediaTypeId { get; set; }

    public int? GenreId { get; set; }

    public string? Composer { get; set; }

    public int? Milliseconds { get; set; }

    public int? Bytes { get; set; }

    public decimal? UnitPrice { get; set; }

    public AlbumApiModel? Album { get; set; }

    public ICollection<PlaylistApiModel> Playlists { get; set; } = new List<PlaylistApiModel>();
    public string? AlbumName { get; set; }

    public Track Convert()
    {
        return new Track
        {
            Id = Id,
            Name = Name,
            AlbumId = AlbumId,
            MediaTypeId = MediaTypeId,
            GenreId = GenreId,
            Composer = Composer,
            Milliseconds = Milliseconds,
            Bytes = Bytes,
            UnitPrice = UnitPrice
        };
    }
}
