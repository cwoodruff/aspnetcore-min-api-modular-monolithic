using SharedKernel.Persistence.ApiModels;

namespace SharedKernel.Persistence.Entities;

public partial class Track
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int? AlbumId { get; set; }

    public int? MediaTypeId { get; set; }

    public int? GenreId { get; set; }

    public string? Composer { get; set; }

    public int? Milliseconds { get; set; }

    public int? Bytes { get; set; }

    public decimal? UnitPrice { get; set; }

    public virtual Album? Album { get; set; }

    public virtual Genre? Genre { get; set; }

    public virtual ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();

    public virtual MediaType? MediaType { get; set; }

    public virtual ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();

    public TrackApiModel Convert() =>
        new()
        {
            Id = Id,
            Name = Name,
            AlbumId = AlbumId,
            MediaTypeId = MediaTypeId,
            GenreId = GenreId,
            Composer = Composer,
            Milliseconds = Milliseconds,
            Bytes = Bytes,
            UnitPrice = UnitPrice,
            InvoiceLines = InvoiceLines.Select(i => i.Convert()).ToList(),
            Album = Album?.Convert(),
            Genre = Genre?.Convert(),
            MediaType = MediaType?.Convert(),
            Playlists = Playlists.Select(p => p.Convert()).ToList()
        };
}
