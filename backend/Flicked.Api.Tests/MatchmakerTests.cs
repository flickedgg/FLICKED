using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flicked.Api.Tests;

/* The matchmaker, against a real database like the pool tests.

   What is being checked here is behaviour players would complain about: being
   matched against somebody far better, waiting forever at a high rating, losing
   your place in the queue because somebody else declined. */
[Collection("pool")]
public class MatchmakerTests(PoolFixture fixture)
{
    private Matchmaker NewMatchmaker(FlickedDbContext db) => new(db, NullLogger<Matchmaker>.Instance);

    /// Ten players of similar rating should become one match, and leave the queue.
    [Fact]
    public async Task Forms_a_match_when_ten_similar_players_wait()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(10, rating: 1500);
        await fixture.QueueAsync(players);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        Assert.Single(made);
        Assert.Equal(10, made[0].Players.Count);
        Assert.Equal(MatchStatus.Accepting, made[0].Status);

        await using var check = fixture.NewContext();
        Assert.Empty(await check.Queue.ToListAsync());
    }

    [Fact]
    public async Task Waits_when_there_are_not_enough_players()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(9, rating: 1500));

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());

        await using var check = fixture.NewContext();
        Assert.Equal(9, await check.Queue.CountAsync());   // still waiting, not lost
    }

    /* The rule that stops a 2600 being dropped in with nine 1000s. Everyone here
       joined just now, so the window is at its narrowest. */
    [Fact]
    public async Task Does_not_match_players_who_are_far_apart()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(9, rating: 1000));
        await fixture.QueueAsync(await fixture.AddPlayersAsync(1, rating: 2600));

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());
    }

    /* ...and the rule that stops that turning into an infinite wait. The same
       players, but everyone has been waiting ten minutes, so the windows have
       opened wide enough to accept each other. */
    [Fact]
    public async Task Matches_far_apart_players_once_they_have_waited()
    {
        await fixture.ResetMatchmakingAsync();
        var waited = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(9, rating: 1000), waited);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(1, rating: 2600), waited);

        await using var db = fixture.NewContext();
        Assert.Single(await NewMatchmaker(db).FormMatchesAsync());
    }

    /// Teams should be close in strength, not "the five who queued first".
    [Fact]
    public async Task Splits_the_teams_by_rating()
    {
        await fixture.ResetMatchmakingAsync();
        /* A 450-point spread, so they have to have been waiting a while for the
           windows to open far enough to accept each other. Queued fresh, the
           matchmaker would refuse them, which is what the test above checks. */
        var waited = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
        var ratings = new[] { 1900, 1850, 1800, 1750, 1700, 1650, 1600, 1550, 1500, 1450 };
        foreach (var rating in ratings)
            await fixture.QueueAsync(await fixture.AddPlayersAsync(1, rating), waited);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        await using var check = fixture.NewContext();
        var rows = await check.MatchPlayers
            .Include(mp => mp.Player)
            .Where(mp => mp.MatchId == made[0].Id)
            .ToListAsync();

        var teamA = rows.Where(r => r.Team == 0).Sum(r => r.Player!.Rating);
        var teamB = rows.Where(r => r.Team == 1).Sum(r => r.Player!.Rating);

        Assert.Equal(5, rows.Count(r => r.Team == 0));
        Assert.Equal(5, rows.Count(r => r.Team == 1));
        Assert.True(Math.Abs(teamA - teamB) <= 100, $"teams differ by {Math.Abs(teamA - teamB)}");
    }

    /* Ten seats that cannot make two fives. 4+3 is seven and the remaining 3 is
       not five, so forming this match would mean splitting a party across the
       teams, which is the one thing a party is for. */
    [Fact]
    public async Task Parties_of_four_three_and_three_are_not_matched()
    {
        await fixture.ResetMatchmakingAsync();
        foreach (var size in new[] { 4, 3, 3 })
            await fixture.QueuePartyAsync(await fixture.AddPlayersAsync(size, rating: 1500));

        await using var db = fixture.NewContext();
        Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());

        await using var check = fixture.NewContext();
        Assert.Equal(3, await check.Queue.CountAsync());   // still waiting, not discarded
    }

    /// Five who queued together are a team, and the draft cannot take one of them.
    [Fact]
    public async Task A_party_of_five_fills_a_team_by_itself()
    {
        await fixture.ResetMatchmakingAsync();
        var five = await fixture.AddPlayersAsync(5, rating: 1500);
        await fixture.QueuePartyAsync(five);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(5, rating: 1500));

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        await using var check = fixture.NewContext();
        var rows = await check.MatchPlayers.Where(mp => mp.MatchId == made[0].Id).ToListAsync();
        var theirs = rows.Where(r => five.Any(p => p.Id == r.PlayerId)).Select(r => r.Team).Distinct();

        Assert.Single(theirs);
        Assert.Equal(5, rows.Count(r => r.Team == 0));
    }

    /* Two parties that fill one side between them. The ratings are arranged so
       that putting them together is also the most balanced arrangement, which is
       what the draft is choosing between: every split here keeps both parties
       whole, and only one of them makes the teams equal. */
    [Fact]
    public async Task Two_parties_that_fill_a_team_are_kept_whole_and_put_together()
    {
        await fixture.ResetMatchmakingAsync();
        var three = await fixture.AddPlayersAsync(3, rating: 1600);
        var two = await fixture.AddPlayersAsync(2, rating: 1500);
        await fixture.QueuePartyAsync(three);
        await fixture.QueuePartyAsync(two);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(5, rating: 1560));

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        await using var check = fixture.NewContext();
        var rows = await check.MatchPlayers.Where(mp => mp.MatchId == made[0].Id).ToListAsync();

        var theirs = rows.Where(r => three.Any(p => p.Id == r.PlayerId)).Select(r => r.Team).Distinct().ToList();
        var others = rows.Where(r => two.Any(p => p.Id == r.PlayerId)).Select(r => r.Team).Distinct().ToList();

        Assert.Single(theirs);
        Assert.Single(others);
        Assert.Equal(theirs[0], others[0]);
    }

    /* The draft is exact, not merely legal.

       These ten balance to a difference of 100, and no arrangement does better.
       The snake draft that came before put them 500 apart, so this is the test
       that would notice it coming back. */
    [Fact]
    public async Task The_teams_are_the_most_balanced_split_there_is()
    {
        await fixture.ResetMatchmakingAsync();
        var waited = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10);
        foreach (var rating in new[] { 1500, 1500, 1500, 1500, 1500, 1400, 1400, 1400, 1400, 1000 })
            await fixture.QueueAsync(await fixture.AddPlayersAsync(1, rating), waited);

        await using var db = fixture.NewContext();
        var made = await NewMatchmaker(db).FormMatchesAsync();

        await using var check = fixture.NewContext();
        var rows = await check.MatchPlayers
            .Include(mp => mp.Player)
            .Where(mp => mp.MatchId == made[0].Id)
            .ToListAsync();

        var teamA = rows.Where(r => r.Team == 0).Sum(r => r.Player!.Rating);
        var teamB = rows.Where(r => r.Team == 1).Sum(r => r.Player!.Rating);

        Assert.Equal(100, Math.Abs(teamA - teamB));
    }

    /* A party is as strong as its best member, not its average.

       A 2600 queueing with a 1200 friend is a 2600 in the match. Averaging them
       to 1900 would hand them nine opponents their friend cannot play against,
       so the party waits for a 2600's opposition instead. */
    [Fact]
    public async Task A_party_is_matched_on_its_highest_rating()
    {
        await fixture.ResetMatchmakingAsync();
        var star = (await fixture.AddPlayersAsync(1, rating: 2600))[0];
        var friend = (await fixture.AddPlayersAsync(1, rating: 1200))[0];
        await fixture.QueuePartyAsync([star, friend]);
        await fixture.QueueAsync(await fixture.AddPlayersAsync(8, rating: 1900));

        await using (var db = fixture.NewContext())
            Assert.Empty(await NewMatchmaker(db).FormMatchesAsync());

        // the same party, against opponents who match the 2600 rather than the average
        await fixture.QueueAsync(await fixture.AddPlayersAsync(8, rating: 2600));

        await using var db2 = fixture.NewContext();
        var made = await NewMatchmaker(db2).FormMatchesAsync();

        Assert.Single(made);
        Assert.Contains(made[0].Players, p => p.PlayerId == friend.Id);
    }

    [Fact]
    public async Task Everyone_accepting_moves_the_match_to_voting()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(10, rating: 1500));

        await using var db = fixture.NewContext();
        var maker = NewMatchmaker(db);
        var match = (await maker.FormMatchesAsync())[0];

        await fixture.AcceptAllAsync(match.Id);

        await using var db2 = fixture.NewContext();
        await NewMatchmaker(db2).AdvancePhasesAsync();

        await using var check = fixture.NewContext();
        Assert.Equal(MatchStatus.Voting, (await check.Matches.SingleAsync(m => m.Id == match.Id)).Status);
    }

    /* Somebody stopped paying attention. The match dies, and the nine who did
       accept must not be punished for it: they go back to the queue, ahead of
       people who only just arrived. */
    [Fact]
    public async Task A_match_nobody_finished_accepting_is_cancelled_and_the_willing_requeue()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(10, rating: 1500));

        await using var db = fixture.NewContext();
        var match = (await NewMatchmaker(db).FormMatchesAsync())[0];

        await fixture.AcceptAllAsync(match.Id, except: 1);       // nine accept, one does not
        await fixture.AgeMatchAsync(match.Id, Matchmaker.AcceptWindow + TimeSpan.FromSeconds(1));

        await using var db2 = fixture.NewContext();
        var (cancelled, _) = await NewMatchmaker(db2).AdvancePhasesAsync();

        Assert.Equal(1, cancelled);

        await using var check = fixture.NewContext();
        Assert.Equal(MatchStatus.Cancelled, (await check.Matches.SingleAsync(m => m.Id == match.Id)).Status);
        Assert.Equal(9, await check.Queue.CountAsync());          // the silent one is not back
    }

    /* The same forgiveness, applied to parties rather than people.

       A party whose members all accepted keeps its place, whole. A party where
       one member never answered is dropped altogether: requeueing the rest
       without them would break up the party they queued as, which is worse for
       them than losing a minute. */
    [Fact]
    public async Task Requeueing_keeps_a_party_whole_and_drops_one_that_did_not_all_accept()
    {
        await fixture.ResetMatchmakingAsync();
        var willing = await fixture.AddPlayersAsync(2, rating: 1500);
        var distracted = await fixture.AddPlayersAsync(2, rating: 1500);
        var solos = await fixture.AddPlayersAsync(6, rating: 1500);

        var kept = await fixture.QueuePartyAsync(willing);
        var dropped = await fixture.QueuePartyAsync(distracted);
        await fixture.QueueAsync(solos);

        await using var db = fixture.NewContext();
        var match = (await NewMatchmaker(db).FormMatchesAsync())[0];

        // everyone but one member of the second party answers
        await fixture.AcceptAsync(match.Id, [.. willing, .. solos, distracted[0]]);
        await fixture.AgeMatchAsync(match.Id, Matchmaker.AcceptWindow + TimeSpan.FromSeconds(1));

        await using var db2 = fixture.NewContext();
        await NewMatchmaker(db2).AdvancePhasesAsync();

        await using var check = fixture.NewContext();
        var waiting = await check.Queue.Select(q => q.PartyId).ToListAsync();

        Assert.Contains(kept.Id, waiting);
        Assert.DoesNotContain(dropped.Id, waiting);
        Assert.Equal(7, waiting.Count);            // the party of two, and six on their own
    }

    [Fact]
    public async Task The_most_voted_map_is_played()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(10, rating: 1500));

        await using var db = fixture.NewContext();
        var match = (await NewMatchmaker(db).FormMatchesAsync())[0];
        await fixture.AcceptAllAsync(match.Id);

        await using (var votes = fixture.NewContext())
        {
            var rows = await votes.MatchPlayers.Where(mp => mp.MatchId == match.Id).ToListAsync();
            for (var i = 0; i < rows.Count; i++) rows[i].MapVote = i < 6 ? "de_nuke" : "de_mirage";
            await votes.SaveChangesAsync();
        }

        await using var db2 = fixture.NewContext();
        await NewMatchmaker(db2).AdvancePhasesAsync();   // accepting -> voting
        await using var db3 = fixture.NewContext();
        await NewMatchmaker(db3).AdvancePhasesAsync();   // voting -> pending, since everyone voted

        await using var check = fixture.NewContext();
        var played = await check.Matches.SingleAsync(m => m.Id == match.Id);
        Assert.Equal("de_nuke", played.Map);
        Assert.Equal(MatchStatus.Pending, played.Status);
    }

    /// Nobody voted: a map is still chosen, rather than the match hanging.
    [Fact]
    public async Task A_vote_nobody_cast_still_picks_a_map()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.QueueAsync(await fixture.AddPlayersAsync(10, rating: 1500));

        await using var db = fixture.NewContext();
        var match = (await NewMatchmaker(db).FormMatchesAsync())[0];
        await fixture.AcceptAllAsync(match.Id);

        await using var db2 = fixture.NewContext();
        await NewMatchmaker(db2).AdvancePhasesAsync();
        await fixture.AgeMatchAsync(match.Id, Matchmaker.VoteWindow + TimeSpan.FromSeconds(1));

        await using var db3 = fixture.NewContext();
        await NewMatchmaker(db3).AdvancePhasesAsync();

        await using var check = fixture.NewContext();
        var played = await check.Matches.SingleAsync(m => m.Id == match.Id);
        Assert.Contains(played.Map, Matchmaker.MapPool);
        Assert.Equal(MatchStatus.Pending, played.Status);
    }
}
