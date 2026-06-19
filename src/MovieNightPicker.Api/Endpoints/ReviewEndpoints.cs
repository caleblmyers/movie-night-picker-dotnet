using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Services;

namespace MovieNightPicker.Api.Endpoints;

/// <summary>
/// User-scoped reviews HTTP surface (one review per movie). The whole group requires
/// authentication and every handler scopes to <see cref="CurrentUser.GetUserId"/>.
/// Review content must be non-empty. Handlers return typed results for host-free
/// unit testing.
/// </summary>
public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var reviews = app.MapGroup("/reviews").RequireAuthorization();

        reviews.MapGet("/", ListAsync).WithName("ListReviews");
        reviews.MapGet("/{tmdbId:int}", GetAsync).WithName("GetReview");
        reviews.MapPut("/{tmdbId:int}", UpsertAsync).WithName("UpsertReview");
        reviews.MapDelete("/{tmdbId:int}", DeleteAsync).WithName("DeleteReview");

        return app;
    }

    /// <summary>Lists the current user's reviews (<c>GET /reviews</c>).</summary>
    internal static async Task<Results<Ok<IReadOnlyList<ReviewResponse>>, UnauthorizedHttpResult>> ListAsync(
        ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var reviews = await service.ListReviewsAsync(userId, ct);
        IReadOnlyList<ReviewResponse> response = reviews.Select(ReviewResponse.FromEntity).ToList();
        return TypedResults.Ok(response);
    }

    /// <summary>Gets the user's review for a movie (<c>GET /reviews/{tmdbId}</c>); 404 if none.</summary>
    internal static async Task<Results<Ok<ReviewResponse>, NotFound, UnauthorizedHttpResult>> GetAsync(
        int tmdbId, ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var review = await service.GetReviewAsync(userId, tmdbId, ct);
        return review is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ReviewResponse.FromEntity(review));
    }

    /// <summary>Creates or updates the user's review (<c>PUT /reviews/{tmdbId}</c>).</summary>
    internal static async Task<Results<Ok<ReviewResponse>, ValidationProblem, UnauthorizedHttpResult>> UpsertAsync(
        int tmdbId, UpsertReviewRequest body, ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body.Content))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["content"] = ["Review content is required."],
            });
        }

        var review = await service.UpsertReviewAsync(userId, tmdbId, body.Content.Trim(), ct);
        return TypedResults.Ok(ReviewResponse.FromEntity(review));
    }

    /// <summary>Deletes the user's review (<c>DELETE /reviews/{tmdbId}</c>); 404 if none.</summary>
    internal static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> DeleteAsync(
        int tmdbId, ClaimsPrincipal user, RatingReviewService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var deleted = await service.DeleteReviewAsync(userId, tmdbId, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
