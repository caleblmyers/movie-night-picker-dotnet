using MovieNightPicker.Core.Suggestions;

namespace MovieNightPicker.Api.Contracts;

/// <summary>
/// Request body for <c>POST /suggest/round/{round}</c>: the movie ids the user
/// has already picked this session. They're excluded from the round's results
/// and (on round 10) mined for the anchor genre — the flow is stateless, so the
/// caller threads the running selection back in each round.
/// </summary>
public sealed record SuggestRoundRequest(IReadOnlyList<int>? SelectedMovieIds);

/// <summary>One suggest round's four movies plus the round's category and label.</summary>
public sealed record SuggestRoundResponse(
    IReadOnlyList<MovieResponse> Movies,
    string Category,
    string CategoryLabel)
{
    /// <summary>Map a Core <see cref="SuggestRoundResult"/> onto the response shape.</summary>
    public static SuggestRoundResponse FromResult(SuggestRoundResult result) => new(
        result.Movies.Select(MovieResponse.FromCore).ToList(),
        result.Category.ToString(),
        result.CategoryLabel);
}
