using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MovieNightPicker.Data;
using MovieNightPicker.Data.Entities;

namespace MovieNightPicker.Tests.Data;

public class RatingConstraintTests
{
    // Builds the model without opening a connection — UseNpgsql only supplies the
    // provider conventions; no database is touched when we inspect context.Model.
    private static MovieNightPickerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MovieNightPickerDbContext>()
            .UseNpgsql("Host=localhost;Database=model-inspection")
            .Options;
        return new MovieNightPickerDbContext(options);
    }

    [Fact]
    public void RatingEntity_HasRatingValueRangeCheckConstraint()
    {
        using var context = CreateContext();

        // Check constraints are relational metadata, stripped from the runtime
        // read-optimized model — read them from the design-time model instead.
        var model = context.GetService<IDesignTimeModel>().Model;
        var rating = model.FindEntityType(typeof(Rating));
        Assert.NotNull(rating);

        var constraint = Assert.Single(rating!.GetCheckConstraints());
        Assert.Equal("CK_Rating_RatingValue_Range", constraint.Name);
        Assert.Equal("\"RatingValue\" >= 1 AND \"RatingValue\" <= 10", constraint.Sql);
    }
}
