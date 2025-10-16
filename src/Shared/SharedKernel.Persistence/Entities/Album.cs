namespace SharedKernel.Persistence.Entities;

public partial class Album
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public int? ArtistId { get; set; }

    public virtual Artist? Artist { get; set; }

    public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
}
