using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Tmdb;

namespace MovieNightPicker.Api.Endpoints;

/// <summary>People search + detail endpoints, backed by the TMDB client.</summary>
public static class PersonEndpoints
{
    public static IEndpointRouteBuilder MapPersonEndpoints(this IEndpointRouteBuilder app)
    {
        var people = app.MapGroup("/people");

        people.MapGet("/search", SearchAsync).WithName("SearchPeople");
        people.MapGet("/{id:int}", GetDetailAsync).WithName("GetPerson");

        return app;
    }

    /// <summary>Full-text people search (<c>GET /people/search?query=&amp;page=</c>).</summary>
    private static async Task<IResult> SearchAsync(
        string? query, ITmdbClient client, CancellationToken ct, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new { error = "query is required" });
        }

        var options = new TmdbRequestOptions { Page = page < 1 ? 1 : page };
        var results = await client.SearchPeopleAsync(query, options, ct);
        return Results.Ok(PersonPageResponse.FromTmdb(results));
    }

    /// <summary>Person detail (<c>GET /people/{id}</c>); 404 when TMDB has no such person.</summary>
    private static async Task<IResult> GetDetailAsync(int id, ITmdbClient client, CancellationToken ct)
    {
        try
        {
            var person = await client.GetPersonAsync(id, ct: ct);
            return Results.Ok(PersonResponse.FromTmdb(person));
        }
        catch (TmdbApiException ex) when (ex.StatusCode == 404)
        {
            return Results.NotFound();
        }
    }
}
