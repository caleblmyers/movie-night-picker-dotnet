using System.Security.Claims;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Services;

namespace MovieNightPicker.Api.Endpoints;

/// <summary>Aggregate insights over a user's own collection, served over HTTP.</summary>
public static class InsightsEndpoints
{
    public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/collections/{id:int}/insights", GetInsightsAsync)
            .WithName("GetCollectionInsights")
            .RequireAuthorization();

        return app;
    }

    /// <summary>
    /// <c>GET /collections/{id}/insights</c>: aggregate insights for the caller's
    /// own collection. 401 when unauthenticated, 404 when the collection is missing
    /// or owned by another user.
    /// </summary>
    private static async Task<IResult> GetInsightsAsync(
        int id, ClaimsPrincipal user, InsightsService insights, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var result = await insights.ComputeAsync(userId.Value, id, ct);
        return result is null
            ? Results.NotFound()
            : Results.Ok(CollectionInsightsResponse.FromResult(result));
    }
}
