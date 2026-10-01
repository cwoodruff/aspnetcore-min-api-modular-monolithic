namespace Catalog.Modules.Models;

/// <summary>A track's sales as Catalog has counted them so far (eventually consistent, ADR-0009).</summary>
internal sealed record TrackSalesApiModel(int TrackId, int TimesSold, DateTime? LastSoldAt);
