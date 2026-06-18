namespace MovieNightPicker.Core.Models;

/// <summary>
/// The resolved, TMDB-shaped discovery query produced from a
/// <see cref="DiscoverFilters"/>. This is what gets handed to an
/// <see cref="IMovieDataSource"/>.
/// </summary>
/// <remarks>
/// Equality is <em>structural</em> (element-wise over the collection fields),
/// not the reference-based default a record would give its list properties.
/// The fallback chain in the discovery engine relies on this to dedup
/// consecutive identical param sets.
/// </remarks>
public sealed record DiscoverParams
{
    public IReadOnlyList<int> Genres { get; init; } = [];
    public (int Start, int End)? YearRange { get; init; }
    public IReadOnlyList<int> Actors { get; init; } = [];
    public IReadOnlyList<int> Crew { get; init; } = [];
    public IReadOnlyList<int> Keywords { get; init; } = [];
    public (int Start, int End)? RuntimeRange { get; init; }
    public string? WatchProviders { get; init; }
    public IReadOnlyList<int> ExcludeGenres { get; init; } = [];
    public IReadOnlyList<int> ExcludeCast { get; init; } = [];
    public IReadOnlyList<int> ExcludeCrew { get; init; } = [];
    public (int Start, int End)? PopularityRange { get; init; }
    public IReadOnlyList<string> OriginCountries { get; init; } = [];
    public double? VoteAverageGte { get; init; }
    public int? VoteCountGte { get; init; }
    public string SortBy { get; init; } = "popularity.desc";
    public int Page { get; init; } = 1;

    public bool Equals(DiscoverParams? other)
    {
        if (other is null)
        {
            return false;
        }

        return Genres.SequenceEqual(other.Genres)
            && YearRange == other.YearRange
            && Actors.SequenceEqual(other.Actors)
            && Crew.SequenceEqual(other.Crew)
            && Keywords.SequenceEqual(other.Keywords)
            && RuntimeRange == other.RuntimeRange
            && WatchProviders == other.WatchProviders
            && ExcludeGenres.SequenceEqual(other.ExcludeGenres)
            && ExcludeCast.SequenceEqual(other.ExcludeCast)
            && ExcludeCrew.SequenceEqual(other.ExcludeCrew)
            && PopularityRange == other.PopularityRange
            && OriginCountries.SequenceEqual(other.OriginCountries)
            && VoteAverageGte == other.VoteAverageGte
            && VoteCountGte == other.VoteCountGte
            && SortBy == other.SortBy
            && Page == other.Page;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var g in Genres)
        {
            hash.Add(g);
        }

        hash.Add(YearRange);
        foreach (var a in Actors)
        {
            hash.Add(a);
        }

        foreach (var c in Crew)
        {
            hash.Add(c);
        }

        foreach (var k in Keywords)
        {
            hash.Add(k);
        }

        hash.Add(RuntimeRange);
        hash.Add(WatchProviders);
        foreach (var g in ExcludeGenres)
        {
            hash.Add(g);
        }

        foreach (var c in ExcludeCast)
        {
            hash.Add(c);
        }

        foreach (var c in ExcludeCrew)
        {
            hash.Add(c);
        }

        hash.Add(PopularityRange);
        foreach (var country in OriginCountries)
        {
            hash.Add(country);
        }

        hash.Add(VoteAverageGte);
        hash.Add(VoteCountGte);
        hash.Add(SortBy);
        hash.Add(Page);
        return hash.ToHashCode();
    }
}
