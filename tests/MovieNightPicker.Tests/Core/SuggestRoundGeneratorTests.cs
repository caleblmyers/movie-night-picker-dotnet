using MovieNightPicker.Core.Constants;
using MovieNightPicker.Core.Suggestions;

namespace MovieNightPicker.Tests.Core;

public class SuggestRoundGeneratorTests
{
    [Theory]
    [InlineData(1, RoundCategory.Genre)]
    [InlineData(2, RoundCategory.Era)]
    [InlineData(3, RoundCategory.Mood)]
    [InlineData(4, RoundCategory.Popularity)]
    [InlineData(5, RoundCategory.Genre)]
    [InlineData(6, RoundCategory.Era)]
    [InlineData(7, RoundCategory.Mood)]
    [InlineData(8, RoundCategory.Popularity)]
    [InlineData(9, RoundCategory.Genre)]
    [InlineData(10, RoundCategory.Mixed)]
    public void Each_round_has_the_expected_category_and_four_slots(int round, RoundCategory expected)
    {
        var def = SuggestRoundGenerator.Generate(round);

        Assert.Equal(expected, def.Category);
        Assert.Equal(4, def.Slots.Count);
        Assert.False(string.IsNullOrWhiteSpace(def.CategoryLabel));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public void Out_of_range_rounds_throw(int round)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SuggestRoundGenerator.Generate(round));
    }

    [Fact]
    public void Genre_rounds_spread_distinct_single_genres_and_carry_no_anchor()
    {
        var def = SuggestRoundGenerator.Generate(1);

        Assert.Null(def.AnchorGenre);
        Assert.All(def.Slots, slot => Assert.Single(slot.Genres));
        Assert.Equal([28, 18, 35, 878], def.Slots.Select(s => s.Genres[0]));
    }

    [Fact]
    public void Genre_rounds_use_the_strict_genre_quality_floor()
    {
        var def = SuggestRoundGenerator.Generate(5);

        Assert.Equal(QualityFloors.GenreRound.VoteAverageGte, def.DefaultVoteAverageGte);
        Assert.Equal(QualityFloors.GenreRound.VoteCountGte, def.DefaultVoteCountGte);
        Assert.Equal([53, 27, 10749, 16], def.Slots.Select(s => s.Genres[0]));
    }

    [Fact]
    public void Round9_uses_the_third_genre_set()
    {
        var def = SuggestRoundGenerator.Generate(9);

        Assert.Equal([80, 12, 14, 36], def.Slots.Select(s => s.Genres[0]));
    }

    [Fact]
    public void Era_round_anchors_on_drama_then_action_with_year_ranges()
    {
        var drama = SuggestRoundGenerator.Generate(2);
        Assert.Equal(18, drama.AnchorGenre);
        Assert.All(drama.Slots, slot => Assert.Equal([18], slot.Genres));
        Assert.All(drama.Slots, slot => Assert.NotNull(slot.YearRange));
        Assert.Equal(QualityFloors.EraMoodRound.VoteAverageGte, drama.DefaultVoteAverageGte);

        var action = SuggestRoundGenerator.Generate(6);
        Assert.Equal(28, action.AnchorGenre);
        Assert.All(action.Slots, slot => Assert.Equal([28], slot.Genres));
    }

    [Fact]
    public void Mood_round_anchors_on_the_genre_and_attaches_keywords()
    {
        var drama = SuggestRoundGenerator.Generate(3);
        Assert.Equal(18, drama.AnchorGenre);
        Assert.All(drama.Slots, slot => Assert.Equal([18], slot.Genres));
        Assert.All(drama.Slots, slot => Assert.NotEmpty(slot.KeywordIds));

        var action = SuggestRoundGenerator.Generate(7);
        Assert.Equal(28, action.AnchorGenre);
        Assert.All(action.Slots, slot => Assert.Equal([28], slot.Genres));
    }

    [Fact]
    public void Popularity_round_uses_three_bands_plus_a_catch_all()
    {
        var def = SuggestRoundGenerator.Generate(4);

        Assert.Equal(18, def.AnchorGenre);
        Assert.Equal("popularity.desc", def.DefaultSortBy);
        Assert.Equal(QualityFloors.PopularityRound.VoteAverageGte, def.DefaultVoteAverageGte);

        Assert.Equal(PopularityLevels.High, def.Slots[0].PopularityRange);
        Assert.Equal(PopularityLevels.Average, def.Slots[1].PopularityRange);
        Assert.Equal(PopularityLevels.Low, def.Slots[2].PopularityRange);
        Assert.Null(def.Slots[3].PopularityRange); // any popularity
    }

    [Fact]
    public void Mixed_round_adapts_to_the_anchor_and_mixes_all_axes()
    {
        var def = SuggestRoundGenerator.Generate(10, anchorGenre: 35);

        Assert.Equal(RoundCategory.Mixed, def.Category);
        Assert.Equal(35, def.AnchorGenre);
        Assert.All(def.Slots, slot => Assert.Equal([35], slot.Genres));

        // genre / era / mood / popularity, one apiece
        Assert.Null(def.Slots[0].YearRange);
        Assert.NotNull(def.Slots[1].YearRange);
        Assert.NotEmpty(def.Slots[2].KeywordIds);
        Assert.NotNull(def.Slots[3].PopularityRange);
    }

    [Fact]
    public void Mixed_round_defaults_to_drama_when_no_anchor_is_known()
    {
        var def = SuggestRoundGenerator.Generate(10);

        Assert.Equal(18, def.AnchorGenre);
        Assert.All(def.Slots, slot => Assert.Equal([18], slot.Genres));
    }
}
