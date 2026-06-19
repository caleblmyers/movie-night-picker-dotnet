using System.ComponentModel.DataAnnotations;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Api.Contracts;

/// <summary>
/// Request body for upserting a rating (<c>PUT /ratings/{tmdbId}</c>). The value is
/// validated to the documented 1-10 scale at the request layer so a friendly 400 is
/// returned before the DB's CK_Rating_RatingValue_Range check ever fires.
/// </summary>
public sealed record UpsertRatingRequest(
    [property: Range(1, 10)] int Value);

/// <summary>A user's rating for a movie.</summary>
public sealed record RatingResponse(
    int TmdbId, int Value, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static RatingResponse FromEntity(Rating r) =>
        new(r.TmdbId, r.RatingValue, r.CreatedAt, r.UpdatedAt);
}

/// <summary>Request body for upserting a review (<c>PUT /reviews/{tmdbId}</c>).</summary>
public sealed record UpsertReviewRequest(
    [property: Required] string Content);

/// <summary>A user's written review for a movie.</summary>
public sealed record ReviewResponse(
    int TmdbId, string Content, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ReviewResponse FromEntity(Review r) =>
        new(r.TmdbId, r.Content, r.CreatedAt, r.UpdatedAt);
}
