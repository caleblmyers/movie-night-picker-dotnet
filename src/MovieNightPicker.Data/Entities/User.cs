namespace MovieNightPicker.Data.Entities;

/// <summary>
/// An application user. Owns all user-scoped data (history, saved movies,
/// ratings, reviews, collections, suggestion history) via cascade-delete relations.
/// </summary>
public class User
{
    public int Id { get; set; }

    public required string Email { get; set; }

    public required string Password { get; set; }

    public required string Name { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<MovieHistory> History { get; set; } = new List<MovieHistory>();

    public ICollection<SavedMovie> SavedMovies { get; set; } = new List<SavedMovie>();

    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public ICollection<Collection> Collections { get; set; } = new List<Collection>();

    public ICollection<SuggestMovieHistory> SuggestHistory { get; set; } = new List<SuggestMovieHistory>();
}
