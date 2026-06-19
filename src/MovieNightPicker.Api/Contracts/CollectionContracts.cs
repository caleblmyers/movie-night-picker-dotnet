using System.ComponentModel.DataAnnotations;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Api.Contracts;

/// <summary>Request body for <c>POST /collections</c>.</summary>
public sealed record CreateCollectionRequest(
    [property: Required] string Name,
    string? Description,
    bool IsPublic = false);

/// <summary>Request body for <c>PUT /collections/{id}</c>.</summary>
public sealed record UpdateCollectionRequest(
    [property: Required] string Name,
    string? Description,
    bool IsPublic = false);

/// <summary>Request body for <c>POST /collections/{id}/movies</c>.</summary>
public sealed record AddMovieRequest([property: Required] int TmdbId);

/// <summary>A movie entry within a collection response.</summary>
public sealed record CollectionMovieResponse(int TmdbId, DateTime AddedAt)
{
    public static CollectionMovieResponse FromEntity(CollectionMovie m) =>
        new(m.TmdbId, m.AddedAt);
}

/// <summary>A user's collection plus its movie entries — keeps EF entities off the API surface.</summary>
public sealed record CollectionResponse(
    int Id,
    string Name,
    string? Description,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CollectionMovieResponse> Movies)
{
    public static CollectionResponse FromEntity(Collection c) => new(
        c.Id,
        c.Name,
        c.Description,
        c.IsPublic,
        c.CreatedAt,
        c.UpdatedAt,
        c.Movies.OrderBy(m => m.AddedAt).Select(CollectionMovieResponse.FromEntity).ToList());
}
