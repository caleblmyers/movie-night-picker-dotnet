using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Api.Services;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;
using MovieNightPicker.Tmdb;
using MovieNightPicker.Tmdb.Dtos;
using TmdbDiscoverParams = MovieNightPicker.Tmdb.DiscoverParams;

namespace MovieNightPicker.Tests.Api;

/// <summary>
/// Direct tests for <see cref="InsightsService"/> against an open in-memory SQLite
/// DbContext (real, disposable schema) and a canned TMDB client — no real TMDB calls.
/// </summary>
public class InsightsServiceTests
{
    /// <summary>Opens a fresh in-memory SQLite DbContext with the schema created.</summary>
    private static (MovieNightPickerDbContext Db, SqliteConnection Conn) CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<MovieNightPickerDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new MovieNightPickerDbContext(options);
        db.Database.EnsureCreated();
        return (db, conn);
    }

    /// <summary>
    /// A canned TMDB client: every movie carries genres [28, 18] (Action/Drama),
    /// the same actor (500), and the same director (600), so frequencies are
    /// predictable. Only the three insights calls are wired.
    /// </summary>
    private sealed class CannedTmdbClient : ITmdbClient
    {
        public Task<TmdbMovie> GetMovieAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new TmdbMovie
            {
                Id = id,
                Title = $"Movie {id}",
                ReleaseDate = "2005-05-05",
                VoteAverage = 8.0,
                VoteCount = 500,
                Runtime = 120,
                Genres = id == 1
                    ? [new TmdbGenre { Id = 28, Name = "Action" }, new TmdbGenre { Id = 18, Name = "Drama" }]
                    : [new TmdbGenre { Id = 28, Name = "Action" }],
            });

        public Task<TmdbCredits> GetMovieCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new TmdbCredits
            {
                Cast = [new TmdbCastMember { Id = 500, Name = "Lead Actor" }],
                Crew = [new TmdbCrewMember { Id = 600, Name = "The Director", Job = "Director", Department = "Directing" }],
            });

        public Task<IReadOnlyList<TmdbKeyword>> GetMovieKeywordsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TmdbKeyword>>([new TmdbKeyword { Id = 9000, Name = "heist" }]);

        public Task<TmdbPagedResult<TmdbMovie>> SearchMoviesAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPagedResult<TmdbPerson>> SearchPeopleAsync(string query, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPerson> GetPersonAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbPagedResult<TmdbMovie>> DiscoverMoviesAsync(TmdbDiscoverParams discover, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbGenre>> GetGenresAsync(TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<TmdbCredits> GetPersonCombinedCreditsAsync(int id, TmdbRequestOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>Seeds a user owning a collection with two movies (tmdbIds 1, 2); returns the ids.</summary>
    private static async Task<(int UserId, int CollectionId)> SeedAsync(MovieNightPickerDbContext db)
    {
        var user = new User { Email = "owner@example.com", Password = "x", Name = "Owner" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var collection = new Collection { Name = "Faves", UserId = user.Id };
        db.Collections.Add(collection);
        await db.SaveChangesAsync();

        db.CollectionMovies.AddRange(
            new CollectionMovie { CollectionId = collection.Id, TmdbId = 1 },
            new CollectionMovie { CollectionId = collection.Id, TmdbId = 2 });
        await db.SaveChangesAsync();

        return (user.Id, collection.Id);
    }

    [Fact]
    public async Task Compute_aggregates_genre_actor_and_crew_counts_for_the_owner()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var (userId, collectionId) = await SeedAsync(db);
        var service = new InsightsService(db, new CannedTmdbClient());

        var result = await service.ComputeAsync(userId, collectionId);

        Assert.NotNull(result);
        Assert.Equal(2, result!.TotalMovies);

        // Genre 28 (Action) tags both movies; 18 (Drama) only movie 1.
        var top = result.MoviesByGenre[0];
        Assert.Equal(28, top.GenreId);
        Assert.Equal(2, top.Count);
        Assert.Equal(2, result.UniqueGenres);

        // The shared actor and director each appear in both movies.
        Assert.Equal(500, result.TopActors[0].Id);
        Assert.Equal(2, result.TopActors[0].Count);
        Assert.Equal(600, result.TopCrew[0].Id);
        Assert.Equal(2, result.TopCrew[0].Count);
    }

    [Fact]
    public async Task Compute_returns_null_for_a_non_owner()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var (_, collectionId) = await SeedAsync(db);
        var service = new InsightsService(db, new CannedTmdbClient());

        // A different user must not be able to read the collection's insights.
        var result = await service.ComputeAsync(userId: 999, collectionId);

        Assert.Null(result);
    }

    [Fact]
    public async Task Compute_returns_null_for_a_missing_collection()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var (userId, _) = await SeedAsync(db);
        var service = new InsightsService(db, new CannedTmdbClient());

        var result = await service.ComputeAsync(userId, collectionId: 12345);

        Assert.Null(result);
    }
}
