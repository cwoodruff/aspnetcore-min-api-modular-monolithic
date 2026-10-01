using Catalog.Modules.Domain;
using Catalog.Modules.Models;

namespace Catalog.Modules.Mapping;

/// <summary>Entity and API model conversions for the Catalog module.</summary>
internal static class CatalogMappings
{
    public static ArtistApiModel ToApiModel(this Artist entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name
    };

    public static Artist ToEntity(this ArtistApiModel model) => new()
    {
        Id = model.Id,
        Name = model.Name ?? string.Empty
    };

    public static AlbumApiModel ToApiModel(this Album entity) => new()
    {
        Id = entity.Id,
        ArtistId = entity.ArtistId,
        Title = entity.Title,
        ArtistName = entity.Artist?.Name
    };

    public static Album ToEntity(this AlbumApiModel model) => new()
    {
        Id = model.Id,
        ArtistId = model.ArtistId,
        Title = model.Title ?? string.Empty
    };

    public static TrackApiModel ToApiModel(this Track entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        AlbumId = entity.AlbumId,
        MediaTypeId = entity.MediaTypeId,
        GenreId = entity.GenreId,
        Composer = entity.Composer,
        Milliseconds = entity.Milliseconds,
        Bytes = entity.Bytes,
        UnitPrice = entity.UnitPrice
    };

    public static Track ToEntity(this TrackApiModel model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        AlbumId = model.AlbumId,
        MediaTypeId = model.MediaTypeId,
        GenreId = model.GenreId,
        Composer = model.Composer,
        Milliseconds = model.Milliseconds,
        Bytes = model.Bytes,
        UnitPrice = model.UnitPrice
    };

    public static PlaylistApiModel ToApiModel(this Playlist entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name
    };

    public static Playlist ToEntity(this PlaylistApiModel model) => new()
    {
        Id = model.Id,
        Name = model.Name
    };

    public static List<ArtistApiModel> ToApiModels(this IEnumerable<Artist> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();

    public static List<AlbumApiModel> ToApiModels(this IEnumerable<Album> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();

    public static List<TrackApiModel> ToApiModels(this IEnumerable<Track> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();

    public static List<PlaylistApiModel> ToApiModels(this IEnumerable<Playlist> entities) =>
        entities.Select(entity => entity.ToApiModel()).ToList();
}
