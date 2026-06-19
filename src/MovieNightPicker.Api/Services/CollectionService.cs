using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Api.Services;

/// <summary>
/// User-scoped collection persistence. Every method filters by <c>userId</c> so a
/// caller can only ever read or mutate their own collections — ownership is enforced
/// in the query, never assumed from the route.
/// </summary>
public sealed class CollectionService
{
    private readonly MovieNightPickerDbContext _db;

    public CollectionService(MovieNightPickerDbContext db) => _db = db;

    /// <summary>All of the user's collections, each with its movie entries.</summary>
    public async Task<IReadOnlyList<Collection>> ListAsync(int userId, CancellationToken ct)
    {
        return await _db.Collections
            .Where(c => c.UserId == userId)
            .Include(c => c.Movies)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);
    }

    /// <summary>A single owned collection (with movies), or null if missing / not owned.</summary>
    public async Task<Collection?> GetAsync(int userId, int collectionId, CancellationToken ct)
    {
        return await _db.Collections
            .Include(c => c.Movies)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, ct);
    }

    /// <summary>Creates a collection owned by the user.</summary>
    public async Task<Collection> CreateAsync(
        int userId, string name, string? description, bool isPublic, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var collection = new Collection
        {
            UserId = userId,
            Name = name.Trim(),
            Description = description,
            IsPublic = isPublic,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Collections.Add(collection);
        await _db.SaveChangesAsync(ct);
        return collection;
    }

    /// <summary>Updates an owned collection; null if missing / not owned.</summary>
    public async Task<Collection?> UpdateAsync(
        int userId, int collectionId, string name, string? description, bool isPublic, CancellationToken ct)
    {
        var collection = await GetAsync(userId, collectionId, ct);
        if (collection is null)
        {
            return null;
        }

        collection.Name = name.Trim();
        collection.Description = description;
        collection.IsPublic = isPublic;
        collection.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return collection;
    }

    /// <summary>Deletes an owned collection; false if missing / not owned.</summary>
    public async Task<bool> DeleteAsync(int userId, int collectionId, CancellationToken ct)
    {
        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId, ct);
        if (collection is null)
        {
            return false;
        }

        _db.Collections.Remove(collection);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Adds a movie to an owned collection. Idempotent: a tmdbId already present is a
    /// no-op (honouring the unique (CollectionId, TmdbId) index). Null if not owned.
    /// </summary>
    public async Task<Collection?> AddMovieAsync(int userId, int collectionId, int tmdbId, CancellationToken ct)
    {
        var collection = await GetAsync(userId, collectionId, ct);
        if (collection is null)
        {
            return null;
        }

        if (!collection.Movies.Any(m => m.TmdbId == tmdbId))
        {
            collection.Movies.Add(new CollectionMovie { TmdbId = tmdbId, AddedAt = DateTime.UtcNow });
            collection.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return collection;
    }

    /// <summary>
    /// Removes a movie from an owned collection. Idempotent: a tmdbId not present is a
    /// no-op. Null if the collection is missing / not owned.
    /// </summary>
    public async Task<Collection?> RemoveMovieAsync(int userId, int collectionId, int tmdbId, CancellationToken ct)
    {
        var collection = await GetAsync(userId, collectionId, ct);
        if (collection is null)
        {
            return null;
        }

        var movie = collection.Movies.FirstOrDefault(m => m.TmdbId == tmdbId);
        if (movie is not null)
        {
            collection.Movies.Remove(movie);
            collection.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return collection;
    }
}
