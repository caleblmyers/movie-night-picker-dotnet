using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Services;
using MovieNightPicker.Api.Validation;

namespace MovieNightPicker.Api.Endpoints;

/// <summary>
/// User-scoped ratings HTTP surface (1-10 per movie). The whole group requires
/// authentication and every handler scopes to <see cref="CurrentUser.GetUserId"/>.
/// The rating value's 1-10 range is enforced by <see cref="ValidationEndpointFilter{T}"/>
/// (running the <c>[Range]</c> attribute) so a friendly 400 beats the DB's check
/// constraint. Handlers return typed results for host-free unit testing.
/// </summary>
public static class RatingEndpoints
{
    public static IEndpointRouteBuilder MapRatingEndpoints(this IEndpointRouteBuilder app)
    {
        var ratings = app.MapGroup("/ratings").RequireAuthorization();

        ratings.MapGet("/", ListAsync).WithName("ListRatings");
        ratings.MapGet("/{tmdbId:int}", GetAsync).WithName("GetRating");
        ratings.MapPut("/{tmdbId:int}", UpsertAsync)
            .WithRequestValidation<UpsertRatingRequest>()
            .WithName("UpsertRating");
        ratings.MapDelete("/{tmdbId:int}", DeleteAsync).WithName("DeleteRating");

        return app;
    }

    /// <summary>Lists the current user's ratings (<c>GET /ratings</c>).</summary>
    internal static async Task<Results<Ok<IReadOnlyList<RatingResponse>>, UnauthorizedHttpResult>> ListAsync(
        ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var ratings = await service.ListRatingsAsync(userId, ct);
        IReadOnlyList<RatingResponse> response = ratings.Select(RatingResponse.FromEntity).ToList();
        return TypedResults.Ok(response);
    }

    /// <summary>Gets the user's rating for a movie (<c>GET /ratings/{tmdbId}</c>); 404 if none.</summary>
    internal static async Task<Results<Ok<RatingResponse>, NotFound, UnauthorizedHttpResult>> GetAsync(
        int tmdbId, ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var rating = await service.GetRatingAsync(userId, tmdbId, ct);
        return rating is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(RatingResponse.FromEntity(rating));
    }

    /// <summary>Creates or updates the user's rating (<c>PUT /ratings/{tmdbId}</c>).</summary>
    internal static async Task<Results<Ok<RatingResponse>, ValidationProblem, UnauthorizedHttpResult>> UpsertAsync(
        int tmdbId, UpsertRatingRequest body, ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        // The 1-10 range is enforced by WithRequestValidation<UpsertRatingRequest>()
        // (the [Range] attribute), so by the time we get here the value is valid.
        var rating = await service.UpsertRatingAsync(userId, tmdbId, body.Value, ct);
        return TypedResults.Ok(RatingResponse.FromEntity(rating));
    }

    /// <summary>Deletes the user's rating (<c>DELETE /ratings/{tmdbId}</c>); 404 if none.</summary>
    internal static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> DeleteAsync(
        int tmdbId, ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var deleted = await service.DeleteRatingAsync(userId, tmdbId, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
