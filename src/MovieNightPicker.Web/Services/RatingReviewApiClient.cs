using System.Net.Http.Json;

namespace MovieNightPicker.Web.Services;

/// <summary>A user's rating for a movie (mirrors the API's RatingResponse).</summary>
public sealed record RatingResponse(int TmdbId, int Value, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>A user's written review for a movie (mirrors the API's ReviewResponse).</summary>
public sealed record ReviewResponse(int TmdbId, string Content, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>Request body for upserting a rating (<c>PUT /ratings/{tmdbId}</c>).</summary>
public sealed record UpsertRatingRequest(int Value);

/// <summary>Request body for upserting a review (<c>PUT /reviews/{tmdbId}</c>).</summary>
public sealed record UpsertReviewRequest(string Content);

/// <summary>
/// Typed wrapper over the API's user-scoped <c>/ratings</c> and <c>/reviews</c>
/// endpoints. The injected <see cref="HttpClient"/> carries the JWT bearer token, so
/// every call is scoped to the current user. Pages construct this with
/// <c>new RatingReviewApiClient(Http)</c>.
/// </summary>
public sealed class RatingReviewApiClient(HttpClient http)
{
    /// <summary>Lists the current user's ratings (<c>GET /ratings</c>).</summary>
    public async Task<IReadOnlyList<RatingResponse>> ListRatingsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<RatingResponse>>("/ratings", ct)
            ?? Array.Empty<RatingResponse>();

    /// <summary>Creates or updates a rating (<c>PUT /ratings/{tmdbId}</c>; value 1-10); null when rejected.</summary>
    public async Task<RatingResponse?> UpsertRatingAsync(int tmdbId, int value, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/ratings/{tmdbId}", new UpsertRatingRequest(value), ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RatingResponse>(ct)
            : null;
    }

    /// <summary>Deletes a rating (<c>DELETE /ratings/{tmdbId}</c>); true on success.</summary>
    public async Task<bool> DeleteRatingAsync(int tmdbId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/ratings/{tmdbId}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Lists the current user's reviews (<c>GET /reviews</c>).</summary>
    public async Task<IReadOnlyList<ReviewResponse>> ListReviewsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<ReviewResponse>>("/reviews", ct)
            ?? Array.Empty<ReviewResponse>();

    /// <summary>Creates or updates a review (<c>PUT /reviews/{tmdbId}</c>); null when rejected.</summary>
    public async Task<ReviewResponse?> UpsertReviewAsync(int tmdbId, string content, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/reviews/{tmdbId}", new UpsertReviewRequest(content), ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReviewResponse>(ct)
            : null;
    }

    /// <summary>Deletes a review (<c>DELETE /reviews/{tmdbId}</c>); true on success.</summary>
    public async Task<bool> DeleteReviewAsync(int tmdbId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/reviews/{tmdbId}", ct);
        return response.IsSuccessStatusCode;
    }
}
