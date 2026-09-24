using Flicked.Api.Services;

namespace Flicked.Api.Tests;

/* The arithmetic, on its own.

   No database here on purpose: Services/Rating.cs is deliberately pure, so what
   the formula does at the edges can be asserted without a match existing. The
   properties below are the ones docs/RATING.md promises players, and each of them is
   something a tuning change could quietly break. */
public class RatingTests
{
    [Fact]
    public void Equal_ratings_are_an_even_match()
    {
        Assert.Equal(0.5, Rating.Expected(1500, 1500), 6);
    }

    /* Both sides of a match have to agree about the odds: if one team is expected
       to win 70% of the time, the other is expected to win 30%. A formula that
       failed this would hand out more rating than it took away. */
    [Theory]
    [InlineData(1000, 1400)]
    [InlineData(2600, 1000)]
    [InlineData(1500, 1501)]
    public void The_two_sides_expectations_add_up_to_one(int a, int b)
    {
        Assert.Equal(1.0, Rating.Expected(a, b) + Rating.Expected(b, a), 6);
    }

    /// 400 points is the scale: one side expected to win ten times out of eleven.
    [Fact]
    public void Four_hundred_points_is_a_ten_to_one_favourite()
    {
        Assert.Equal(1.0 / 11.0, Rating.Expected(1000, 1400), 4);
    }

    /* The whole point of an expected-score model. Beating a team far above you is
       worth many times beating one at your own level, and beating one far below
       you is worth almost nothing - which is what makes farming weaker opponents
       pointless rather than merely frowned upon. */
    [Fact]
    public void Beating_stronger_opposition_is_worth_more()
    {
        var againstStronger = Rating.Delta(1500, 1900, score: 1, matchesPlayed: 50, performance: 0);
        var againstEqual = Rating.Delta(1500, 1500, score: 1, matchesPlayed: 50, performance: 0);
        var againstWeaker = Rating.Delta(1500, 1100, score: 1, matchesPlayed: 50, performance: 0);

        Assert.True(againstStronger > againstEqual, $"{againstStronger} should beat {againstEqual}");
        Assert.True(againstEqual > againstWeaker, $"{againstEqual} should beat {againstWeaker}");
        Assert.Equal(16, againstEqual);          // the coin flip: half of K
    }

    /// And the mirror: losing to a weaker team costs more than losing to a stronger one.
    [Fact]
    public void Losing_to_weaker_opposition_costs_more()
    {
        var toWeaker = Rating.Delta(1500, 1100, score: 0, matchesPlayed: 50, performance: 0);
        var toStronger = Rating.Delta(1500, 1900, score: 0, matchesPlayed: 50, performance: 0);

        Assert.True(toWeaker < toStronger, $"{toWeaker} should cost more than {toStronger}");
    }

    /* Performance is a correction, never the answer. The best game anybody has
       ever played and the worst are a quarter of K either side of the result,
       and no further. */
    [Fact]
    public void Performance_is_bounded_to_a_quarter_of_K()
    {
        var best = Rating.Delta(1500, 1500, score: 1, matchesPlayed: 50, performance: 1);
        var average = Rating.Delta(1500, 1500, score: 1, matchesPlayed: 50, performance: 0);
        var worst = Rating.Delta(1500, 1500, score: 1, matchesPlayed: 50, performance: -1);

        Assert.Equal(24, best);        // 16 + 8
        Assert.Equal(16, average);
        Assert.Equal(8, worst);        // 16 - 8
    }

    /// A performance score outside -1..+1 is clamped, not trusted.
    [Fact]
    public void An_absurd_performance_score_buys_nothing_extra()
    {
        Assert.Equal(Rating.Delta(1500, 1500, 1, 50, performance: 1),
                     Rating.Delta(1500, 1500, 1, 50, performance: 25));
    }

    /* The rule the whole design exists to protect: a win never loses you rating,
       and a loss never gains you rating. Here a hopeless favourite wins - worth
       almost nothing on the result - while playing as badly as it is possible to
       play. The performance term wants to take points away; it may not. */
    [Fact]
    public void A_bad_game_cannot_turn_a_win_into_a_loss()
    {
        var delta = Rating.Delta(2600, 1000, score: 1, matchesPlayed: 200, performance: -1);
        Assert.True(delta >= Rating.MinChange, $"winning paid {delta}");
    }

    /// ...and the other way: a heroic loss is still a loss.
    [Fact]
    public void A_good_game_cannot_turn_a_loss_into_a_win()
    {
        var delta = Rating.Delta(1000, 2600, score: 0, matchesPlayed: 200, performance: 1);
        Assert.True(delta <= -Rating.MinChange, $"losing paid {delta}");
    }

    /* A new account's rating is a guess and should move fast; one built from two
       hundred matches should not. The slide is linear across the first ten. */
    [Theory]
    [InlineData(0, 64)]
    [InlineData(5, 48)]
    [InlineData(10, 32)]
    [InlineData(500, 32)]
    public void K_slides_from_provisional_to_established(int played, double expected)
    {
        Assert.Equal(expected, Rating.KFactor(played));
    }

    [Fact]
    public void A_provisional_player_moves_further_for_the_same_match()
    {
        var newcomer = Rating.Delta(1000, 1200, score: 1, matchesPlayed: 0, performance: 0);
        var veteran = Rating.Delta(1000, 1200, score: 1, matchesPlayed: 200, performance: 0);

        Assert.Equal(49, newcomer);
        Assert.Equal(24, veteran);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void Provisional_lasts_ten_matches(int played, bool provisional)
    {
        Assert.Equal(provisional, Rating.IsProvisional(played));
    }

    /* A rating that can fall forever is not a rank, and matchmaking cannot bridge
       a player who has fallen off the bottom of it. */
    [Fact]
    public void Rating_stops_at_the_floor()
    {
        Assert.Equal(Rating.Floor, Rating.Apply(110, -30));
        Assert.Equal(Rating.Floor, Rating.Apply(Rating.Floor, -32));
        Assert.Equal(1516, Rating.Apply(1500, 16));
    }
}
