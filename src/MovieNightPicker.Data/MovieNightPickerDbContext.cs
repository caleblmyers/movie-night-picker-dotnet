using Microsoft.EntityFrameworkCore;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Data;

/// <summary>
/// EF Core context for all user-scoped persistence. Models, indexes, unique
/// constraints, and cascade-delete behaviour mirror the original Prisma schema.
/// </summary>
public class MovieNightPickerDbContext : DbContext
{
    public MovieNightPickerDbContext(DbContextOptions<MovieNightPickerDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<MovieHistory> MovieHistory => Set<MovieHistory>();

    public DbSet<SavedMovie> SavedMovies => Set<SavedMovie>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Collection> Collections => Set<Collection>();

    public DbSet<CollectionMovie> CollectionMovies => Set<CollectionMovie>();

    public DbSet<SuggestMovieHistory> SuggestMovieHistory => Set<SuggestMovieHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<MovieHistory>(entity =>
        {
            entity.HasOne(h => h.User)
                .WithMany(u => u.History)
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(h => h.UserId);
        });

        modelBuilder.Entity<SavedMovie>(entity =>
        {
            entity.HasOne(s => s.User)
                .WithMany(u => u.SavedMovies)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.UserId, s.TmdbId }).IsUnique();
            entity.HasIndex(s => s.UserId);
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasOne(r => r.User)
                .WithMany(u => u.Ratings)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // RatingValue is documented as a 1-10 scale — enforce it at the DB level so
            // bad data can't slip past the application. Postgres needs the column quoted.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Rating_RatingValue_Range", "\"RatingValue\" >= 1 AND \"RatingValue\" <= 10"));

            entity.HasIndex(r => new { r.UserId, r.TmdbId }).IsUnique();
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.TmdbId);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => new { r.UserId, r.TmdbId }).IsUnique();
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.TmdbId);
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.Property(c => c.IsPublic).HasDefaultValue(false);

            entity.HasOne(c => c.User)
                .WithMany(u => u.Collections)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.UserId);
        });

        modelBuilder.Entity<CollectionMovie>(entity =>
        {
            entity.HasOne(cm => cm.Collection)
                .WithMany(c => c.Movies)
                .HasForeignKey(cm => cm.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(cm => new { cm.CollectionId, cm.TmdbId }).IsUnique();
            entity.HasIndex(cm => cm.CollectionId);
            entity.HasIndex(cm => cm.TmdbId);
        });

        modelBuilder.Entity<SuggestMovieHistory>(entity =>
        {
            entity.HasOne(s => s.User)
                .WithMany(u => u.SuggestHistory)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.UserId, s.TmdbId }).IsUnique();
            entity.HasIndex(s => s.UserId);
            entity.HasIndex(s => s.TmdbId);
            entity.HasIndex(s => new { s.UserId, s.CreatedAt });
        });
    }
}
