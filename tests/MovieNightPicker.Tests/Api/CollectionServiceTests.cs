using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Api.Services;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Tests.Api;

/// <summary>
/// Direct <see cref="CollectionService"/> tests over an open in-memory SQLite
/// connection (real schema, disposable). The emphasis is per-user ownership: a
/// second user must never read or mutate the first user's collection.
/// </summary>
public class CollectionServiceTests
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

    /// <summary>Seeds a user and returns its id.</summary>
    private static async Task<int> SeedUserAsync(MovieNightPickerDbContext db, string email)
    {
        var user = new User
        {
            Email = email,
            Password = "hash",
            Name = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Create_then_get_and_list_round_trip()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new CollectionService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");

        var created = await service.CreateAsync(userId, "Faves", "My favourites", true, default);

        Assert.True(created.Id > 0);
        Assert.Equal("Faves", created.Name);
        Assert.True(created.IsPublic);
        Assert.NotEqual(default, created.CreatedAt);

        var fetched = await service.GetAsync(userId, created.Id, default);
        Assert.NotNull(fetched);
        Assert.Equal("Faves", fetched!.Name);

        var list = await service.ListAsync(userId, default);
        Assert.Single(list);
    }

    [Fact]
    public async Task Update_changes_fields_and_bumps_timestamp()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new CollectionService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");
        var created = await service.CreateAsync(userId, "Old", null, false, default);

        var updated = await service.UpdateAsync(userId, created.Id, "New", "desc", true, default);

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Name);
        Assert.Equal("desc", updated.Description);
        Assert.True(updated.IsPublic);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task Delete_removes_collection()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new CollectionService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");
        var created = await service.CreateAsync(userId, "Temp", null, false, default);

        Assert.True(await service.DeleteAsync(userId, created.Id, default));
        Assert.Null(await service.GetAsync(userId, created.Id, default));
        Assert.False(await service.DeleteAsync(userId, created.Id, default));
    }

    [Fact]
    public async Task Add_then_remove_movie()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new CollectionService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");
        var created = await service.CreateAsync(userId, "Watchlist", null, false, default);

        var withMovie = await service.AddMovieAsync(userId, created.Id, 603, default);
        Assert.NotNull(withMovie);
        Assert.Single(withMovie!.Movies);
        Assert.Equal(603, withMovie.Movies.First().TmdbId);

        var withoutMovie = await service.RemoveMovieAsync(userId, created.Id, 603, default);
        Assert.NotNull(withoutMovie);
        Assert.Empty(withoutMovie!.Movies);
    }

    [Fact]
    public async Task Add_duplicate_movie_is_idempotent()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new CollectionService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");
        var created = await service.CreateAsync(userId, "Watchlist", null, false, default);

        await service.AddMovieAsync(userId, created.Id, 603, default);
        var again = await service.AddMovieAsync(userId, created.Id, 603, default);

        // The unique (CollectionId, TmdbId) index is honoured: no second row, no error.
        Assert.NotNull(again);
        Assert.Single(again!.Movies);
    }

    [Fact]
    public async Task Remove_missing_movie_is_idempotent()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new CollectionService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");
        var created = await service.CreateAsync(userId, "Watchlist", null, false, default);

        var result = await service.RemoveMovieAsync(userId, created.Id, 999, default);

        Assert.NotNull(result);
        Assert.Empty(result!.Movies);
    }

    [Fact]
    public async Task A_user_cannot_read_or_mutate_another_users_collection()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new CollectionService(db);
        var owner = await SeedUserAsync(db, "owner@example.com");
        var intruder = await SeedUserAsync(db, "intruder@example.com");

        var owned = await service.CreateAsync(owner, "Private", null, false, default);

        // Reads are scoped to the caller.
        Assert.Null(await service.GetAsync(intruder, owned.Id, default));
        Assert.Empty(await service.ListAsync(intruder, default));

        // Mutations against someone else's collection are refused (null / false).
        Assert.Null(await service.UpdateAsync(intruder, owned.Id, "Hacked", null, false, default));
        Assert.Null(await service.AddMovieAsync(intruder, owned.Id, 1, default));
        Assert.Null(await service.RemoveMovieAsync(intruder, owned.Id, 1, default));
        Assert.False(await service.DeleteAsync(intruder, owned.Id, default));

        // The owner's collection is untouched.
        var stillThere = await service.GetAsync(owner, owned.Id, default);
        Assert.NotNull(stillThere);
        Assert.Equal("Private", stillThere!.Name);
    }
}
