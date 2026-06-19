using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Core;
using MovieNightPicker.Core.Suggestions;

namespace MovieNightPicker.Api.Endpoints;

/// <summary>
/// The 10-round suggest flow over HTTP. Stateless — the caller passes the ids
/// picked so far with each round, mirroring the original anonymous suggest flow,
/// so no auth is needed.
/// </summary>
public static class SuggestEndpoints
{
    public static IEndpointRouteBuilder MapSuggestEndpoints(this IEndpointRouteBuilder app)
    {
        var suggest = app.MapGroup("/suggest");

        suggest.MapPost("/round/{round:int}", RoundAsync).WithName("SuggestRound");

        return app;
    }

    /// <summary>
    /// Drive one round (<c>POST /suggest/round/{round}</c>, round 1-10) of the
    /// suggest flow via <see cref="SuggestFlow.GetRoundAsync"/>, returning the
    /// round's four movies plus its category. 400 when the round is out of range.
    /// </summary>
    public static async Task<IResult> RoundAsync(
        int round, SuggestRoundRequest? body, IMovieDataSource source, CancellationToken ct)
    {
        if (round is < 1 or > 10)
        {
            return TypedResults.BadRequest(new { error = "round must be between 1 and 10" });
        }

        var selectedIds = body?.SelectedMovieIds ?? [];
        var result = await SuggestFlow.GetRoundAsync(round, selectedIds, source, ct);

        return TypedResults.Ok(SuggestRoundResponse.FromResult(result));
    }
}
