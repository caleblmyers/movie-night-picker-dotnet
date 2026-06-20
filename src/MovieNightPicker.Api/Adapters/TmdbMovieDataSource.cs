using MovieNightPicker.Core;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Dtos;
using CoreModels = MovieNightPicker.Core.Models;
using TmdbDiscoverParams = MovieNightPicker.Tmdb.DiscoverParams;

namespace MovieNightPicker.Api.Adapters;

/// <summary>
/// Adapts the TMDB typed client (<see cref="ITmdbClient"/>) to the Core engine's
/// <see cref="IMovieDataSource"/> abstraction. This is the seam that keeps the
/// suggestion/discovery logic in Core free of any TMDB-specific types.
/// </summary>
public sealed class TmdbMovieDataSource(ITmdbClient client) : IMovieDataSource
{
    public async Task<IReadOnlyList<CoreModels.Movie>> DiscoverMoviesAsync(
        CoreModels.DiscoverParams p, CancellationToken ct = default)
    {
        var (discover, options) = ToTmdb(p);
        var page = await client.DiscoverMoviesAsync(discover, options, ct);
        return page.Results.Select(m => m.ToCore()).ToList();
    }

    public async Task<CoreModels.Movie?> GetMovieAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var movie = await client.GetMovieAsync(id, ct: ct);
            return movie.ToCore();
        }
        catch (TmdbApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<int>> GetMovieKeywordsAsync(int id, CancellationToken ct = default)
    {
        var keywords = await client.GetMovieKeywordsAsync(id, ct);
        return keywords.Select(k => k.Id).ToList();
    }

    /// <summary>Maps a Core discover query onto the TMDB-shaped params + request options.</summary>
    private static (TmdbDiscoverParams Discover, TmdbRequestOptions Options) ToTmdb(CoreModels.DiscoverParams p)
    {
        var discover = new TmdbDiscoverParams
        {
            Genres = [.. p.Genres],
            YearRange = p.YearRange,
            Actors = [.. p.Actors],
            Crew = [.. p.Crew],
            Keywords = [.. p.Keywords],
            RuntimeRange = p.RuntimeRange,
            WatchProviders = p.WatchProviders,
            ExcludeGenres = [.. p.ExcludeGenres],
            ExcludeCast = [.. p.ExcludeCast],
            ExcludeCrew = [.. p.ExcludeCrew],
            PopularityRange = p.PopularityRange is { } range ? (range.Start, range.End) : null,
            OriginCountries = [.. p.OriginCountries],
            VoteAverageGte = p.VoteAverageGte,
            VoteCountGte = p.VoteCountGte,
        };

        var options = new TmdbRequestOptions
        {
            SortBy = p.SortBy,
            Page = p.Page,
        };

        return (discover, options);
    }
}
