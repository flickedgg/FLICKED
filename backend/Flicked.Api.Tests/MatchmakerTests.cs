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
