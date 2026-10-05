using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flicked.Api.Tests;

/* Starting a match short-handed, for an instance too small to fill one.

   Kept apart from MatchmakerTests because every test here has to move the
   settings that decide match sizes, and those are static: shared by the whole
   process and restored by SmallCommunity below. Both classes are in the "pool"
   collection, which xUnit runs one test at a time, so nothing else is reading
   them while one of these is running. */
[Collection("pool")]
public class ShortHandedTests(PoolFixture fixture)
{
    private Matchmaker NewMatchmaker(FlickedDbContext db) => new(db, NullLogger<Matchmaker>.Instance);

    /* An instance that will settle for `minimum` after `after`, until disposed.

       Restoring on the way out matters more than usual here: leaving
       MinCompetitivePlayers at 6 would quietly let every other test in the suite
       form short matches, and most of them would still pass. */
    private sealed class SmallCommunity : IDisposable
    {
        private readonly int _min = Matchmaker.MinCompetitivePlayers;
        private readonly TimeSpan _after = Matchmaker.ShortHandedAfter;

        public SmallCommunity(int minimum, TimeSpan? after = null)
        {
            Matchmaker.MinCompetitivePlayers = minimum;
            if (after is not null) Matchmaker.ShortHandedAfter = after.Value;
        }

        public void Dispose()
        {
            Matchmaker.MinCompetitivePlayers = _min;
            Matchmaker.ShortHandedAfter = _after;
        }
    }

    /// Long enough ago that the short-handed window has passed.
    private static DateTimeOffset Waited => DateTimeOffset.UtcNow - Matchmaker.ShortHandedAfter
                                                                 - TimeSpan.FromSeconds(5);

    // --- what the setting resolves to -------------------------------------

    /* Even, because everything downstream assumes two teams of the same size.
       Rounded up, so a minimum of 5 never starts a match of 4. */
    [Theory]
    [InlineData(6, 6)]
    [InlineData(5, 6)]    // odd rounds up
    [InlineData(7, 8)]
    [InlineData(4, 4)]
    [InlineData(10, 10)]  // the same as a full match: switched off
    [InlineData(12, 10)]  // never more than a full match
    [InlineData(1, 2)]
    [InlineData(0, 2)]
    [InlineData(-4, 2)]   // nonsense, not a crash
    public void A_minimum_resolves_to_an_even_number_a_match_can_be(int configured, int expected)
    {
        using var _ = new SmallCommunity(configured);
        Assert.Equal(expected, Matchmaker.MinPlayersFor(ServerType.Competitive));
    }

    // --- forming one ------------------------------------------------------

    [Fact]
    public async Task Starts_short_once_the_wait_has_passed()
    {
        using var _ = new SmallCommunity(minimum: 6);
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(6, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        Assert.Single(made);
        Assert.Equal(6, made[0].Players.Count);
        Assert.Equal(3, made[0].Players.Count(p => p.Team == 0));
        Assert.Equal(3, made[0].Players.Count(p => p.Team == 1));

        await using var check = fixture.NewContext();
        Assert.Empty(await check.Queue.ToListAsync());
    }

    /* The whole point of the delay: six people who arrived a moment ago are not
       given a 3v3 when four more might walk in. */
    [Fact]
    public async Task Does_not_start_short_before_the_wait_has_passed()
    {
        using var _ = new SmallCommunity(minimum: 6);
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(6, rating: 1500));   // just now

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());

        await using var check = fixture.NewContext();
        Assert.Equal(6, await check.Queue.CountAsync());   // still waiting, not lost
    }

    /* Setting nothing is the old behaviour exactly: the minimum defaults to the
       full size, and nine players wait however long they have been waiting. */
    [Fact]
    public async Task Never_starts_short_when_the_setting_is_left_alone()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(9, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());
    }

    /* A full match beats a short one even when the short one could be built
       first. All ten have waited, so without the two passes the oldest six would
       be taken into a 3v3 and the other four left behind. */
    [Fact]
    public async Task A_full_match_is_preferred_over_a_short_one()
    {
        using var _ = new SmallCommunity(minimum: 4);
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(10, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        Assert.Single(made);
        Assert.Equal(10, made[0].Players.Count);
    }

    /// Between two short sizes, the bigger one wins: eight before six.
    [Fact]
    public async Task Takes_the_largest_short_match_it_can()
    {
        using var _ = new SmallCommunity(minimum: 4);
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(8, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        Assert.Single(made);
        Assert.Equal(8, made[0].Players.Count);
    }

    /* Nine is not a match whatever the minimum says, because nine cannot be two
       equal teams. Eight of them play and the ninth keeps waiting, which is the
       honest outcome: the alternative is 5v4. */
    [Fact]
    public async Task An_odd_queue_leaves_one_player_waiting_rather_than_making_uneven_teams()
    {
        using var _ = new SmallCommunity(minimum: 4);
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(9, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        Assert.Single(made);
        Assert.Equal(8, made[0].Players.Count);
        Assert.Equal(4, made[0].Players.Count(p => p.Team == 0));
        Assert.Equal(4, made[0].Players.Count(p => p.Team == 1));

        await using var check = fixture.NewContext();
        Assert.Equal(1, await check.Queue.CountAsync());
    }

    /// Fewer than the minimum is still not a match.
    [Fact]
    public async Task Waits_when_there_are_fewer_than_the_minimum()
    {
        using var _ = new SmallCommunity(minimum: 6);
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(4, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());
    }

    /* A party is still never split, short-handed or not. A four-stack and two
       solos are six, but 4+1+1 cannot make two threes, so there is no match
       here — and the four-stack is not quietly broken up to make one. */
    [Fact]
    public async Task Does_not_split_a_party_to_reach_the_minimum()
    {
        using var _ = new SmallCommunity(minimum: 6);
        await fixture.ResetMatchmakingAsync();

        var stack = await fixture.AddPlayersAsync(4, rating: 1500);
        await fixture.QueuePartyAsync(stack, Waited);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(2, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());

        await using var check = fixture.NewContext();
        Assert.Equal(3, await check.Queue.CountAsync());   // the stack and the two solos
    }

    /* Two parties of three do make a six, one per side, which is the case the
       setting exists for. */
    [Fact]
    public async Task Two_threes_become_a_three_versus_three()
    {
        using var _ = new SmallCommunity(minimum: 6);
        await fixture.ResetMatchmakingAsync();

        await fixture.QueuePartyAsync(await fixture.AddPlayersAsync(3, rating: 1500), Waited);
        await fixture.QueuePartyAsync(await fixture.AddPlayersAsync(3, rating: 1500), Waited);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        Assert.Single(made);
        Assert.Equal(6, made[0].Players.Count);
        Assert.Equal(3, made[0].Players.Count(p => p.Team == 0));
    }

    /* Rating still decides who plays with whom. Six have waited long enough for
       a short match, but one of them is nowhere near the rest, so the five who
       match each other are not enough and nobody plays. */
    [Fact]
    public async Task Rating_windows_still_apply_to_a_short_match()
    {
        using var _ = new SmallCommunity(minimum: 6);
        await fixture.ResetMatchmakingAsync();

        var recent = DateTimeOffset.UtcNow - Matchmaker.ShortHandedAfter - TimeSpan.FromSeconds(1);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(5, rating: 1000), recent);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(1, rating: 2600), recent);

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());
    }

    /* Somebody who has only just joined can still be swept into a short match
       that an older party anchors. It costs them nothing — they get a match
       immediately — and holding the whole thing back for them would mean a
       steady trickle of arrivals could stop a small instance playing at all. */
    [Fact]
    public async Task A_newcomer_can_be_taken_into_a_short_match_but_cannot_cause_one()
    {
        using var _ = new SmallCommunity(minimum: 6);
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(5, rating: 1500), Waited);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(1, rating: 1500));   // just now

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        Assert.Single(made);
        Assert.Equal(6, made[0].Players.Count);
    }
}
