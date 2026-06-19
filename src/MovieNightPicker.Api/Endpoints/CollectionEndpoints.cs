using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Services;

namespace MovieNightPicker.Api.Endpoints;

/// <summary>
/// User-scoped collections HTTP surface: list/create/get/update/delete plus
/// add/remove movie. The whole group requires authentication; every handler scopes
/// to <see cref="CurrentUser.GetUserId"/> so callers only ever touch their own data.
/// Handlers return typed results so they can be unit-tested without a host.
/// </summary>
public static class CollectionEndpoints
{
    public static IEndpointRouteBuilder MapCollectionEndpoints(this IEndpointRouteBuilder app)
    {
        var collections = app.MapGroup("/collections").RequireAuthorization();

        collections.MapGet("/", ListAsync).WithName("ListCollections");
        collections.MapPost("/", CreateAsync).WithName("CreateCollection");
        collections.MapGet("/{id:int}", GetAsync).WithName("GetCollection");
        collections.MapPut("/{id:int}", UpdateAsync).WithName("UpdateCollection");
        collections.MapDelete("/{id:int}", DeleteAsync).WithName("DeleteCollection");
        collections.MapPost("/{id:int}/movies", AddMovieAsync).WithName("AddCollectionMovie");
        collections.MapDelete("/{id:int}/movies/{tmdbId:int}", RemoveMovieAsync).WithName("RemoveCollectionMovie");

        return app;
    }

    /// <summary>Lists the current user's collections (<c>GET /collections</c>).</summary>
    internal static async Task<Results<Ok<IReadOnlyList<CollectionResponse>>, UnauthorizedHttpResult>> ListAsync(
        ClaimsPrincipal user, CollectionService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var collections = await service.ListAsync(userId, ct);
        IReadOnlyList<CollectionResponse> response =
            collections.Select(CollectionResponse.FromEntity).ToList();
        return TypedResults.Ok(response);
    }

    /// <summary>Creates a collection (<c>POST /collections</c>).</summary>
    internal static async Task<Results<Created<CollectionResponse>, ValidationProblem, UnauthorizedHttpResult>> CreateAsync(
        CreateCollectionRequest body, ClaimsPrincipal user, CollectionService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."],
            });
        }

        var created = await service.CreateAsync(userId, body.Name, body.Description, body.IsPublic, ct);
        var response = CollectionResponse.FromEntity(created);
        return TypedResults.Created($"/collections/{created.Id}", response);
    }

    /// <summary>Gets one owned collection (<c>GET /collections/{id}</c>); 404 if not owned.</summary>
    internal static async Task<Results<Ok<CollectionResponse>, NotFound, UnauthorizedHttpResult>> GetAsync(
        int id, ClaimsPrincipal user, CollectionService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var collection = await service.GetAsync(userId, id, ct);
        return collection is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(CollectionResponse.FromEntity(collection));
    }

    /// <summary>Updates an owned collection (<c>PUT /collections/{id}</c>); 404 if not owned.</summary>
    internal static async Task<Results<Ok<CollectionResponse>, ValidationProblem, NotFound, UnauthorizedHttpResult>> UpdateAsync(
        int id, UpdateCollectionRequest body, ClaimsPrincipal user, CollectionService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."],
            });
        }

        var updated = await service.UpdateAsync(userId, id, body.Name, body.Description, body.IsPublic, ct);
        return updated is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(CollectionResponse.FromEntity(updated));
    }

    /// <summary>Deletes an owned collection (<c>DELETE /collections/{id}</c>); 404 if not owned.</summary>
    internal static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> DeleteAsync(
        int id, ClaimsPrincipal user, CollectionService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var deleted = await service.DeleteAsync(userId, id, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    /// <summary>Adds a movie to an owned collection (<c>POST /collections/{id}/movies</c>).</summary>
    internal static async Task<Results<Ok<CollectionResponse>, NotFound, UnauthorizedHttpResult>> AddMovieAsync(
        int id, AddMovieRequest body, ClaimsPrincipal user, CollectionService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var collection = await service.AddMovieAsync(userId, id, body.TmdbId, ct);
        return collection is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(CollectionResponse.FromEntity(collection));
    }

    /// <summary>Removes a movie from an owned collection (<c>DELETE /collections/{id}/movies/{tmdbId}</c>).</summary>
    internal static async Task<Results<Ok<CollectionResponse>, NotFound, UnauthorizedHttpResult>> RemoveMovieAsync(
        int id, int tmdbId, ClaimsPrincipal user, CollectionService service, CancellationToken ct)
    {
        if (user.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var collection = await service.RemoveMovieAsync(userId, id, tmdbId, ct);
        return collection is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(CollectionResponse.FromEntity(collection));
    }
}
