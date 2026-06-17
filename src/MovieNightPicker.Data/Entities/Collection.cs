namespace MovieNightPicker.Data.Entities;

/// <summary>
/// A user-curated collection (list) of movies. May be public or private.
/// </summary>
public class Collection
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public ICollection<CollectionMovie> Movies { get; set; } = new List<CollectionMovie>();
}
