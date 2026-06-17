using MovieNightPicker.Core.Constants;

namespace MovieNightPicker.Tests.Core;

public class ConstantsTests
{
    [Theory]
    [InlineData(28, "Action")]
    [InlineData(35, "Comedy")]
    [InlineData(878, "Sci-Fi")]
    [InlineData(37, "Western")]
    public void Genres_ById_resolves_name(int id, string expected)
    {
        Assert.Equal(expected, Genres.NameOf(id));
    }

    [Theory]
    [InlineData("Action", 28)]
    [InlineData("Sci-Fi", 878)]
    [InlineData("western", 37)] // case-insensitive
    public void Genres_ByName_resolves_id(string name, int expected)
    {
        Assert.Equal(expected, Genres.IdOf(name));
    }

    [Fact]
    public void Genres_map_is_a_perfect_round_trip()
    {
        foreach (var (id, name) in Genres.ById)
        {
            Assert.Equal(id, Genres.IdOf(name));
        }
    }

    [Fact]
    public void Genres_lookups_return_null_for_unknown()
    {
        Assert.Null(Genres.NameOf(999_999));
        Assert.Null(Genres.IdOf("NotAGenre"));
    }

    [Fact]
    public void PopularityLevels_resolve_to_expected_bands()
    {
        Assert.Equal((100, int.MaxValue), PopularityLevels.RangeFor("HIGH"));
        Assert.Equal((20, 100), PopularityLevels.RangeFor("AVERAGE"));
        Assert.Equal((0, 20), PopularityLevels.RangeFor("LOW"));
        Assert.Null(PopularityLevels.RangeFor("nope"));
    }

    [Fact]
    public void EraYearRanges_resolve_fixed_decades()
    {
        Assert.Equal((1980, 1989), EraYearRanges.RangeFor("80s"));
        Assert.Equal((1990, 1999), EraYearRanges.RangeFor("90s"));
        Assert.Equal((1940, 1970), EraYearRanges.RangeFor("classic"));
    }

    [Fact]
    public void EraYearRanges_open_ended_eras_extend_to_current_year()
    {
        var present = EraYearRanges.RangeFor("2020-present");
        Assert.NotNull(present);
        Assert.Equal(2020, present!.Value.Start);
        Assert.Equal(EraYearRanges.CurrentYear, present.Value.End);
    }

    [Fact]
    public void MoodKeywords_resolve_known_moods_and_empty_for_unknown()
    {
        Assert.Equal([9715, 207317], MoodKeywords.KeywordsFor("dark"));
        Assert.Equal([156024], MoodKeywords.KeywordsFor("fantasy"));
        Assert.Empty(MoodKeywords.KeywordsFor("does-not-exist"));
    }

    [Fact]
    public void QualityFloors_carry_documented_defaults()
    {
        Assert.Equal(new QualityFloor(6.5, 150), QualityFloors.CascadeDefault);
        Assert.Equal(new QualityFloor(6.0, 100), QualityFloors.Emergency);
        Assert.Equal(new QualityFloor(5.0, 50), QualityFloors.Desperate);
    }
}
