namespace MovieNightPicker.Data.Entities;

/// <summary>
/// A movie entry within a <see cref="Collection"/>. Unique per (collection, movie).
/// </summary>
public class CollectionMovie
{
    public int Id { get; set; }

    public int CollectionId { get; set; }

    public int TmdbId { get; set; }

    public DateTime AddedAt { get; set; }

    public Collection Collection { get; set; } = null!;
}
