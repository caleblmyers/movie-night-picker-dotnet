using System.Net;
using System.Net.Http.Json;
using MovieNightPicker.Web.Models;

namespace MovieNightPicker.Web.Services;

/// <summary>Body for the suggest endpoints: the ids the user has picked so far.</summary>
public sealed record SuggestRequest(IReadOnlyList<int> SelectedMovieIds);

/// <summary>
/// One round of the 10-round flow: four candidate movies plus the round's
/// category (mirrors the API's <c>SuggestRoundResponse</c>).
/// </summary>
public sealed record SuggestRound(
    IReadOnlyList<MovieSummary> Movies,
    string Category,
    string CategoryLabel);

/// <summary>
/// Talks to the suggestion surface of the API: the single preference-driven
/// recommendation (<c>POST /movies/suggest</c>) and one round of the stateless
/// 10-round flow (<c>POST /suggest/round/{round}</c>). The flow is stateless, so
/// callers thread the running selection back in with every call.
/// </summary>
public sealed class SuggestApiClient(HttpClient http)
{
    /// <summary>
    /// Suggest a single movie from the user's picks. Returns null when the API
    /// has nothing to recommend (404 / no picks resolvable).
    /// </summary>
    public async Task<MovieSummary?> SuggestAsync(
        IReadOnlyList<int> selectedMovieIds, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            "/movies/suggest", new SuggestRequest(selectedMovieIds), ct);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MovieSummary>(ct);
    }

    /// <summary>
    /// Fetch the four candidates for round <paramref name="round"/> (1-10),
    /// excluding the already-picked ids.
    /// </summary>
    public async Task<SuggestRound?> GetRoundAsync(
        int round, IReadOnlyList<int> selectedMovieIds, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            $"/suggest/round/{round}", new SuggestRequest(selectedMovieIds), ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SuggestRound>(ct);
    }
}
