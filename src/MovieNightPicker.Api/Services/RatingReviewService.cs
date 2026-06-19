using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Api.Services;

/// <summary>
/// User-scoped persistence for ratings and reviews. There is at most one rating and
/// one review per (user, movie) — the unique indexes are honoured by upserting
/// (update if present, else insert). Every method filters by <c>userId</c> so a
/// caller can only touch their own ratings/reviews.
/// </summary>
public sealed class RatingReviewService
{
    private readonly MovieNightPickerDbContext _db;

    public RatingReviewService(MovieNightPickerDbContext db) => _db = db;

    // ---- Ratings ----

    /// <summary>Creates or updates the user's rating for a movie (one per (user, movie)).</summary>
    public async Task<Rating> UpsertRatingAsync(int userId, int tmdbId, int value, CancellationToken ct)
    {
        var rating = await _db.Ratings
            .FirstOrDefaultAsync(r => r.UserId == userId && r.TmdbId == tmdbId, ct);

        var now = DateTime.UtcNow;
        if (rating is null)
        {
            rating = new Rating
            {
                UserId = userId,
                TmdbId = tmdbId,
                RatingValue = value,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Ratings.Add(rating);
        }
        else
        {
            rating.RatingValue = value;
            rating.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return rating;
    }

    /// <summary>The user's rating for a movie, or null.</summary>
    public async Task<Rating?> GetRatingAsync(int userId, int tmdbId, CancellationToken ct) =>
        await _db.Ratings.FirstOrDefaultAsync(r => r.UserId == userId && r.TmdbId == tmdbId, ct);

    /// <summary>All of the user's ratings, newest first.</summary>
    public async Task<IReadOnlyList<Rating>> ListRatingsAsync(int userId, CancellationToken ct) =>
        await _db.Ratings
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

    /// <summary>Deletes the user's rating for a movie; false if none existed.</summary>
    public async Task<bool> DeleteRatingAsync(int userId, int tmdbId, CancellationToken ct)
    {
        var rating = await _db.Ratings
            .FirstOrDefaultAsync(r => r.UserId == userId && r.TmdbId == tmdbId, ct);
        if (rating is null)
        {
            return false;
        }

        _db.Ratings.Remove(rating);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Reviews ----

    /// <summary>Creates or updates the user's review for a movie (one per (user, movie)).</summary>
    public async Task<Review> UpsertReviewAsync(int userId, int tmdbId, string content, CancellationToken ct)
    {
        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.UserId == userId && r.TmdbId == tmdbId, ct);

        var now = DateTime.UtcNow;
        if (review is null)
        {
            review = new Review
            {
                UserId = userId,
                TmdbId = tmdbId,
                Content = content,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Reviews.Add(review);
        }
        else
        {
            review.Content = content;
            review.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return review;
    }

    /// <summary>The user's review for a movie, or null.</summary>
    public async Task<Review?> GetReviewAsync(int userId, int tmdbId, CancellationToken ct) =>
        await _db.Reviews.FirstOrDefaultAsync(r => r.UserId == userId && r.TmdbId == tmdbId, ct);

    /// <summary>All of the user's reviews, newest first.</summary>
    public async Task<IReadOnlyList<Review>> ListReviewsAsync(int userId, CancellationToken ct) =>
        await _db.Reviews
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

    /// <summary>Deletes the user's review for a movie; false if none existed.</summary>
    public async Task<bool> DeleteReviewAsync(int userId, int tmdbId, CancellationToken ct)
    {
        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.UserId == userId && r.TmdbId == tmdbId, ct);
        if (review is null)
        {
            return false;
        }

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
