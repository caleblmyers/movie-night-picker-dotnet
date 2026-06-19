using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Api.Auth;
using MovieNightPicker.Api.Contracts;
using MovieNightPicker.Api.Endpoints;
using MovieNightPicker.Api.Services;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Tests.Api;

/// <summary>
/// Direct <see cref="RatingReviewService"/> tests over an open in-memory SQLite
/// connection, plus request-layer validation tests against the endpoint handlers.
/// Covers upsert-not-duplicate, 1-10 range rejection, review CRUD, and cross-user
/// isolation.
/// </summary>
public class RatingReviewServiceTests
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

    /// <summary>A principal carrying the NameIdentifier claim the handlers read.</summary>
    private static ClaimsPrincipal PrincipalFor(int userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));

    // ---- Ratings ----

    [Fact]
    public async Task UpsertRating_creates_then_updates_without_duplicating()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");

        var created = await service.UpsertRatingAsync(userId, 603, 7, default);
        Assert.Equal(7, created.RatingValue);

        var updated = await service.UpsertRatingAsync(userId, 603, 9, default);
        Assert.Equal(created.Id, updated.Id); // same row, updated in place
        Assert.Equal(9, updated.RatingValue);
        Assert.True(updated.UpdatedAt >= created.CreatedAt);

        Assert.Single(await service.ListRatingsAsync(userId, default));
    }

    [Fact]
    public async Task GetRating_and_DeleteRating()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");
        await service.UpsertRatingAsync(userId, 603, 8, default);

        Assert.NotNull(await service.GetRatingAsync(userId, 603, default));
        Assert.True(await service.DeleteRatingAsync(userId, 603, default));
        Assert.Null(await service.GetRatingAsync(userId, 603, default));
        Assert.False(await service.DeleteRatingAsync(userId, 603, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-3)]
    public async Task Upsert_rating_endpoint_rejects_out_of_range_value(int value)
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");

        var result = await RatingEndpoints.UpsertAsync(
            603, new UpsertRatingRequest(value), PrincipalFor(userId), service, default);

        // Rejected at the request layer with a 400 — never reaches the DB constraint.
        Assert.IsType<ValidationProblem>(result.Result);
        Assert.Null(await service.GetRatingAsync(userId, 603, default));
    }

    [Fact]
    public async Task Upsert_rating_endpoint_accepts_in_range_value()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");

        var result = await RatingEndpoints.UpsertAsync(
            603, new UpsertRatingRequest(10), PrincipalFor(userId), service, default);

        var ok = Assert.IsType<Ok<RatingResponse>>(result.Result);
        Assert.Equal(10, ok.Value!.Value);
    }

    // ---- Reviews ----

    [Fact]
    public async Task UpsertReview_creates_then_updates_without_duplicating()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");

        var created = await service.UpsertReviewAsync(userId, 603, "Loved it", default);
        var updated = await service.UpsertReviewAsync(userId, 603, "Still great", default);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Still great", updated.Content);
        Assert.Single(await service.ListReviewsAsync(userId, default));
    }

    [Fact]
    public async Task GetReview_and_DeleteReview()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");
        await service.UpsertReviewAsync(userId, 603, "A review", default);

        Assert.NotNull(await service.GetReviewAsync(userId, 603, default));
        Assert.True(await service.DeleteReviewAsync(userId, 603, default));
        Assert.Null(await service.GetReviewAsync(userId, 603, default));
        Assert.False(await service.DeleteReviewAsync(userId, 603, default));
    }

    [Fact]
    public async Task Upsert_review_endpoint_rejects_empty_content()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var userId = await SeedUserAsync(db, "owner@example.com");

        var result = await ReviewEndpoints.UpsertAsync(
            603, new UpsertReviewRequest("   "), PrincipalFor(userId), service, default);

        Assert.IsType<ValidationProblem>(result.Result);
        Assert.Null(await service.GetReviewAsync(userId, 603, default));
    }

    // ---- Cross-user isolation ----

    [Fact]
    public async Task A_user_cannot_read_or_delete_another_users_rating_or_review()
    {
        var (db, conn) = CreateDb();
        using var _ = conn;
        await using var __ = db;
        var service = new RatingReviewService(db);
        var owner = await SeedUserAsync(db, "owner@example.com");
        var intruder = await SeedUserAsync(db, "intruder@example.com");

        await service.UpsertRatingAsync(owner, 603, 8, default);
        await service.UpsertReviewAsync(owner, 603, "owner review", default);

        // Reads scoped to the caller see nothing of the owner's data.
        Assert.Null(await service.GetRatingAsync(intruder, 603, default));
        Assert.Null(await service.GetReviewAsync(intruder, 603, default));
        Assert.Empty(await service.ListRatingsAsync(intruder, default));
        Assert.Empty(await service.ListReviewsAsync(intruder, default));

        // Deletes against someone else's data are no-ops.
        Assert.False(await service.DeleteRatingAsync(intruder, 603, default));
        Assert.False(await service.DeleteReviewAsync(intruder, 603, default));

        // An intruder upsert creates a *separate* row — the owner's is untouched.
        await service.UpsertRatingAsync(intruder, 603, 1, default);
        Assert.Equal(8, (await service.GetRatingAsync(owner, 603, default))!.RatingValue);
    }
}
