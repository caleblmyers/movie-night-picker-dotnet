using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Core.Insights;
using MovieNightPicker.Data;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Dtos;

namespace MovieNightPicker.Api.Services;

/// <summary>
/// Computes <see cref="CollectionInsightsResult"/> for a user's own collection:
/// reads the collection's TMDB ids straight from the database, enriches each one
/// from TMDB (movie + credits + keywords) in parallel, then runs the Core
/// aggregation. Reads <see cref="Data.Entities.CollectionMovie"/> directly so it
/// stays independent of the collections CRUD service.
/// </summary>
public sealed class InsightsService(MovieNightPickerDbContext db, ITmdbClient client)
{
    /// <summary>
    /// Compute insights for <paramref name="collectionId"/> when it's owned by
    /// <paramref name="userId"/>; null when the collection is missing or owned by
    /// someone else (the endpoint turns that into a 404).
    /// </summary>
    public async Task<CollectionInsightsResult?> ComputeAsync(
        int userId, int collectionId, CancellationToken ct = default)
    {
        var owned = await db.Collections
            .AnyAsync(c => c.Id == collectionId && c.UserId == userId, ct);
        if (!owned)
        {
            return null;
        }

        var tmdbIds = await db.CollectionMovies
            .Where(cm => cm.CollectionId == collectionId)
            .Select(cm => cm.TmdbId)
            .ToListAsync(ct);

        // Enrich every movie concurrently; drop any TMDB no longer knows about.
        var movies = (await Task.WhenAll(tmdbIds.Select(id => BuildAsync(id, ct))))
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();

        return CollectionInsights.Compute(movies);
    }

    /// <summary>
    /// Build one enriched <see cref="InsightsMovie"/> from TMDB (detail + credits +
    /// keywords, the latter two fetched concurrently); null if the movie is gone.
    /// </summary>
    private async Task<InsightsMovie?> BuildAsync(int tmdbId, CancellationToken ct)
    {
        TmdbMovie movie;
        try
        {
            movie = await client.GetMovieAsync(tmdbId, ct: ct);
        }
        catch (TmdbApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }

        var creditsTask = client.GetMovieCreditsAsync(tmdbId, ct: ct);
        var keywordsTask = client.GetMovieKeywordsAsync(tmdbId, ct);
        await Task.WhenAll(creditsTask, keywordsTask);
        var credits = await creditsTask;
        var keywords = await keywordsTask;

        return new InsightsMovie(
            movie.ToCore(),
            keywords.Select(k => (k.Id, k.Name ?? string.Empty)).ToList(),
            credits.Cast.Select(c => (c.Id, c.Name ?? string.Empty, c.ProfilePath)).ToList(),
            credits.Crew
                .Select(c => (c.Id, c.Name ?? string.Empty, c.ProfilePath, c.Job ?? string.Empty, c.Department ?? string.Empty))
                .ToList());
    }
}
