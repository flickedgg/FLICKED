using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;

using Contestant = Flicked.Api.Tests.PoolFixture.Contestant;

namespace Flicked.Api.Tests;

/* Paying out a match, against a real database like the pool and matchmaker suites.

   It has to be a real one. What is being checked here is a conditional UPDATE and
   the row lock Postgres takes for it: an in-memory provider has neither, so it
   would happily pass the double-apply test while the real thing paid everybody
   twice. */
[Collection("pool")]
public class MatchResultsTests(PoolFixture fixture)
{
    private static Contestant[] EvenTenAtFifteenHundred() =>
        Enumerable.Range(0, 10)
            .Select(i => new Contestant(Rating: 1500, Team: i % 2))
            .ToArray();

    /// Finishes a match the way a series_end would, and says whether it was the call that closed it.
    private async Task<bool> FinishAsync(int matchId, int scoreA, int scoreB)
    {
        await using var db = fixture.NewContext();
        var match = await db.Matches.SingleAsync(m => m.Id == matchId);
        return await fixture.NewResults(db).FinishAsync(match, scoreA, scoreB);
    }

    [Fact]
    public async Task Finishing_a_match_pays_the_winners_and_charges_the_losers()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(EvenTenAtFifteenHundred());

        Assert.True(await FinishAsync(match.Id, 13, 9));

        var rated = await fixture.RatedAsync(match.Id);
        Assert.Equal(10, rated.Count);

        // nobody reported a stat, so there is no performance term: the result alone
        Assert.Equal(5, rated.Count(r => r is (1516, 21, 20, 16)));
        Assert.Equal(5, rated.Count(r => r is (1484, 20, 21, -16)));

        await using var check = fixture.NewContext();
        var finished = await check.Matches.SingleAsync(m => m.Id == match.Id);
        Assert.Equal(MatchStatus.Finished, finished.Status);
        Assert.Equal(13, finished.ScoreA);
        Assert.Equal(9, finished.ScoreB);
    }

    /* The test this whole design exists for.

       MatchZy does not deduplicate and series_end is documented to arrive more
       than once (backend/MATCHZY.md). If applying a result twice were merely
       unlikely rather than impossible, everyone in the match would be paid twice
       and the leaderboard would quietly stop meaning anything. */
    [Fact]
    public async Task Applying_the_same_finished_match_twice_changes_nothing()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(EvenTenAtFifteenHundred());

        Assert.True(await FinishAsync(match.Id, 13, 9));
        var afterFirst = await fixture.RatedAsync(match.Id);

        // the same event again, and then a third time with a different score
        Assert.False(await FinishAsync(match.Id, 13, 9));
        Assert.False(await FinishAsync(match.Id, 16, 14));

        Assert.Equal(afterFirst, await fixture.RatedAsync(match.Id));

        await using var check = fixture.NewContext();
        var finished = await check.Matches.SingleAsync(m => m.Id == match.Id);
        Assert.Equal(13, finished.ScoreA);       // and the score is not rewritten either
        Assert.Equal(9, finished.ScoreB);
    }

    /* The same thing, but arriving at the same instant rather than one after the
       other, which is the case a status check in application code cannot cover:
       both copies of the match would read Live before either had written. The
       claim is a single conditional UPDATE, so one of these blocks on the other's
       row lock and then finds nothing left to claim. */
    [Fact]
    public async Task Two_series_end_events_at_once_rate_the_match_once()
    {
        await fixture.ResetMatchmakingAsync();
        var live = await fixture.AddLiveMatchAsync(EvenTenAtFifteenHundred());

        // separate contexts, so these are genuinely two connections racing
        await using var db1 = fixture.NewContext();
        await using var db2 = fixture.NewContext();
        var match1 = await db1.Matches.SingleAsync(m => m.Id == live.Id);
        var match2 = await db2.Matches.SingleAsync(m => m.Id == live.Id);

        var both = await Task.WhenAll(
            fixture.NewResults(db1).FinishAsync(match1, 13, 9),
            fixture.NewResults(db2).FinishAsync(match2, 13, 9));

        Assert.Single(both, applied => applied);

        var rated = await fixture.RatedAsync(live.Id);
        Assert.Equal(5, rated.Count(r => r.Rating == 1516));   // 1532 would be a double payout
        Assert.Equal(5, rated.Count(r => r.Rating == 1484));
    }

    /* The instance can run with Matchmaking:CompetitivePlayers=2, so a match can
       be one player a side. Nothing in the formula assumes five. */
    [Fact]
    public async Task A_one_versus_one_rates_both_players()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(
            new Contestant(Rating: 1000, Team: 0, Kills: 13, Deaths: 7, Adr: 95),
            new Contestant(Rating: 1000, Team: 1, Kills: 7, Deaths: 13, Adr: 70));

        Assert.True(await FinishAsync(match.Id, 13, 7));

        var rated = await fixture.RatedAsync(match.Id);
        var winner = rated.Single(r => r.Delta > 0);
        var loser = rated.Single(r => r.Delta < 0);

        // the result is worth 16 either way; the rest is the performance term,
        // which is relative, so the two of them come out mirrored
        Assert.Equal(23, winner.Delta);
        Assert.Equal(-23, loser.Delta);
        Assert.Equal(1023, winner.Rating);
        Assert.Equal(977, loser.Rating);
    }

    /* Performance decides how far, never which way. The player who carried and the
       player who was carried both gain; the top fragger on the losing side still
       loses. */
    [Fact]
    public async Task Performance_moves_people_within_the_result_but_never_past_it()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(
            new Contestant(1500, Team: 0, Kills: 28, Deaths: 10, Adr: 115),   // carried the win
            new Contestant(1500, Team: 0, Kills: 8, Deaths: 18, Adr: 45),     // along for the ride
            new Contestant(1500, Team: 1, Kills: 24, Deaths: 14, Adr: 105),   // lost anyway
            new Contestant(1500, Team: 1, Kills: 9, Deaths: 19, Adr: 50));

        Assert.True(await FinishAsync(match.Id, 13, 9));

        var rated = await fixture.RatedAsync(match.Id);
        var deltas = rated.Select(r => r.Delta).OrderByDescending(d => d).ToList();

        Assert.Equal(2, deltas.Count(d => d > 0));      // both winners gained
        Assert.Equal(2, deltas.Count(d => d < 0));      // both losers lost
        Assert.True(deltas[0] > deltas[1], "the best winner should gain more than the worst");
        Assert.True(deltas[2] > deltas[3], "the best loser should lose less than the worst");

        // and nobody escapes the quarter-of-K bound in either direction
        Assert.All(deltas, d => Assert.InRange(Math.Abs(d), 8, 24));
    }

    /* A rating cannot fall forever, and the number written to the history is what
       the floor allowed rather than what the formula asked for - so a player's
       rating always equals their starting one plus the sum of their deltas. */
    [Fact]
    public async Task The_floor_holds_and_the_history_still_adds_up()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(
            new Contestant(Rating: 105, Team: 0),
            new Contestant(Rating: 105, Team: 1));

        Assert.True(await FinishAsync(match.Id, 13, 4));

        var rated = await fixture.RatedAsync(match.Id);
        var loser = rated.Single(r => r.Delta < 0);

        Assert.Equal(Rating.Floor, loser.Rating);
        Assert.Equal(-5, loser.Delta);            // 105 down to 100, not down to 89
    }

    [Fact]
    public async Task A_draw_is_neither_a_win_nor_a_loss()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(
            new Contestant(Rating: 1500, Team: 0),
            new Contestant(Rating: 1500, Team: 1));

        Assert.True(await FinishAsync(match.Id, 12, 12));

        var rated = await fixture.RatedAsync(match.Id);
        Assert.All(rated, r =>
        {
            Assert.Equal(1500, r.Rating);
            Assert.Equal(20, r.Wins);
            Assert.Equal(20, r.Losses);
            Assert.Equal(0, r.Delta);
        });
    }

    /* 0-0 is a match that did not happen: one that was cancelled and reported
       anyway, or that never went live. It is still closed, so the server is
       released and nothing is left hanging, but nobody is paid for it. */
    [Fact]
    public async Task A_match_with_no_rounds_rates_nobody()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(EvenTenAtFifteenHundred());

        Assert.True(await FinishAsync(match.Id, 0, 0));

        Assert.All(await fixture.RatedAsync(match.Id), r =>
        {
            Assert.Equal(1500, r.Rating);
            Assert.Equal(0, r.Delta);
        });

        await using var check = fixture.NewContext();
        Assert.Equal(MatchStatus.Finished, (await check.Matches.SingleAsync(m => m.Id == match.Id)).Status);
    }

    /* The degraded path: no map_result ever arrived, so the saved score is the
       series score, 1-0 with one map per match. Per-round figures worked out
       against a single round are nonsense, so the performance term is dropped and
       the result decides alone. */
    [Fact]
    public async Task A_series_only_result_is_rated_on_the_result_alone()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(
            new Contestant(1500, Team: 0, Kills: 4, Deaths: 22, Adr: 30),     // won, played badly
            new Contestant(1500, Team: 1, Kills: 26, Deaths: 6, Adr: 120));   // lost, played well

        Assert.True(await FinishAsync(match.Id, 1, 0));

        var rated = await fixture.RatedAsync(match.Id);
        Assert.Equal(16, rated.Single(r => r.Delta > 0).Delta);
        Assert.Equal(-16, rated.Single(r => r.Delta < 0).Delta);
    }

    /* A match where one side never had a player on it is not a contest, and there
       is no opponent rating to expect anything against. */
    [Fact]
    public async Task A_match_with_an_empty_team_rates_nobody()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(
            new Contestant(1500, Team: 0),
            new Contestant(1500, Team: 0));

        Assert.True(await FinishAsync(match.Id, 13, 0));

        Assert.All(await fixture.RatedAsync(match.Id), r => Assert.Equal(0, r.Delta));
    }

    /* Provisional ratings move twice as far, and the match that ends the
       provisional window is the tenth one. */
    [Fact]
    public async Task A_new_account_moves_twice_as_far_as_an_established_one()
    {
        await fixture.ResetMatchmakingAsync();
        var match = await fixture.AddLiveMatchAsync(
            new Contestant(1500, Team: 0, Wins: 0, Losses: 0),
            new Contestant(1500, Team: 0, Wins: 100, Losses: 100),
            new Contestant(1500, Team: 1, Wins: 100, Losses: 100),
            new Contestant(1500, Team: 1, Wins: 100, Losses: 100));

        Assert.True(await FinishAsync(match.Id, 13, 6));

        var rated = await fixture.RatedAsync(match.Id);
        Assert.Equal(32, rated.Single(r => r.Wins == 1).Delta);           // the newcomer, K = 64
        Assert.Equal(16, rated.Single(r => r.Wins == 101).Delta);         // their teammate, K = 32
    }
}
