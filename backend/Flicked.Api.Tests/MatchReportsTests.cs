using Flicked.Api.Controllers;
using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Contestant = Flicked.Api.Tests.PoolFixture.Contestant;

namespace Flicked.Api.Tests;

/* Who is allowed to tell FLICKED how a match ended.

   The pool is other people's hardware by design (docs/ROADMAP.md, "Server owners
   bring the servers"), so one registered server is not a trusted peer of another.
   A server's own token says which server is calling; it does not say which
   matches that server may speak for. A match is rated exactly once, so a report
   accepted from the wrong server is not a mistake that can be corrected
   afterwards.

   Against a real database like the rest of the suite: what is being checked is
   what the claim wrote and what the sweep cleared, which is rows. */
[Collection("pool")]
public class MatchReportsTests(PoolFixture fixture)
{
    private static Contestant[] EvenTen() =>
        Enumerable.Range(0, 10).Select(i => new Contestant(Rating: 1500, Team: i % 2)).ToArray();

    /* The events endpoint, called as the holder of `token`.

       Built by hand rather than over HTTP: one HttpContext carries the header
       ServerAuth reads, and the controller is the thing under test. */
    private async Task<IActionResult> ReportAsync(FlickedDbContext db, string token, MatchZyEvent body)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[ServerAuth.Header] = token;

        var controller = new MatchServerController(
            db,
            new ServerAuth(db, new HttpContextAccessor { HttpContext = http }),
            fixture.NewPool(db),
            fixture.NewResults(db),
            new ConfigurationBuilder().Build(),
            NullLogger<MatchServerController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };

        return await controller.Events(body, CancellationToken.None);
    }

    private static MatchZyEvent MapResult(int matchId, int scoreA, int scoreB) => new()
    {
        Event = "map_result",
        MatchId = matchId.ToString(),
        Team1 = new MatchZyTeam { Score = scoreA },
        Team2 = new MatchZyTeam { Score = scoreB },
    };

    private async Task<Match> MatchAsync(int id)
    {
        await using var db = fixture.NewContext();
        return await db.Matches.SingleAsync(m => m.Id == id);
    }

    private async Task<GameServer> ServerAsync(int id)
    {
        await using var db = fixture.NewContext();
        return await db.Servers.SingleAsync(s => s.Id == id);
    }

    /* The hole this suite exists for.

       Any registered server used to be able to finish any match that was not
       finished yet: the handler noticed it was not the right server, logged it,
       and saved the result anyway. That decides the score and every rating in a
       game on somebody else's machine, and the real result arriving afterwards is
       refused as a duplicate. */
    [Fact]
    public async Task A_server_cannot_report_a_match_that_was_never_sent_to_it()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.ResetServersAsync();

        var match = await fixture.AddLiveMatchAsync(EvenTen());
        var host = await fixture.AddServerAsync("host");

        /* Claimed while the host is the only server in the pool, so which one got
           the match is not left to the order ClaimAsync happens to pick in. The
           stranger joins the pool afterwards, which is the situation being
           tested: a registered server with a valid token and no part in this
           match. */
        await using (var db = fixture.NewContext())
        {
            var claimed = await fixture.NewPool(db).ClaimAsync(ServerType.Competitive, match.Id);
            Assert.Equal(host.Id, claimed?.Id);
        }

        var stranger = await fixture.AddServerAsync("stranger", token: "stranger-token");

        await using (var db = fixture.NewContext())
        {
            var answer = await ReportAsync(db, "stranger-token", MapResult(match.Id, 13, 0));
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(answer).StatusCode);
        }

        // not finished, not rated, and still attributed to the server it went to
        var after = await MatchAsync(match.Id);
        Assert.Equal(MatchStatus.Live, after.Status);
        Assert.Equal(host.Id, after.ServerId);
        Assert.All(await fixture.RatedAsync(match.Id), row => Assert.Equal(0, row.Delta));
        Assert.Null((await ServerAsync(stranger.Id)).CurrentMatchId);
    }

    /* Why the check cannot simply be "does this server hold the match".

       Nothing extends the lease until the match reports going_live, so a real
       game can be released from under itself while it is still being played.
       Refusing its result would throw away a match ten people just finished,
       which is why the question asked is where the match was sent, not what the
       server is doing now. */
    [Fact]
    public async Task The_server_it_was_sent_to_can_still_report_after_its_lease_ended()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.ResetServersAsync();

        var match = await fixture.AddLiveMatchAsync(EvenTen());
        var host = await fixture.AddServerAsync("host", token: "host-token");

        await using (var db = fixture.NewContext())
        {
            var pool = fixture.NewPool(db);
            Assert.NotNull(await pool.ClaimAsync(ServerType.Competitive, match.Id));
            // what the sweep does to a match whose lease ran out
            await pool.ReleaseAsync(host.Id);
        }

        await using (var db = fixture.NewContext())
        {
            Assert.IsType<OkResult>(await ReportAsync(db, "host-token", MapResult(match.Id, 13, 9)));
        }

        var after = await MatchAsync(match.Id);
        Assert.Equal(MatchStatus.Finished, after.Status);
        Assert.Equal(13, after.ScoreA);
        Assert.Equal(9, after.ScoreB);
        Assert.All(await fixture.RatedAsync(match.Id), row => Assert.NotEqual(0, row.Delta));
    }

    /* A late report must not reach into the pool.

       By the time it arrives, the released server has been claimed for something
       else, and releasing it on the strength of the old match would hand away the
       match it is running now. The result is still saved; only the pool is left
       alone. */
    [Fact]
    public async Task A_late_report_does_not_release_a_server_that_has_moved_on()
    {
        await fixture.ResetMatchmakingAsync();
        await fixture.ResetServersAsync();

        var earlier = await fixture.AddLiveMatchAsync(EvenTen());
        var next = await fixture.AddLiveMatchAsync(EvenTen());
        var host = await fixture.AddServerAsync("host", token: "host-token");

        await using (var db = fixture.NewContext())
        {
            var pool = fixture.NewPool(db);
            Assert.NotNull(await pool.ClaimAsync(ServerType.Competitive, earlier.Id));
            await pool.ReleaseAsync(host.Id);
            // the only server in the pool, so the next match gets the same one
            Assert.NotNull(await pool.ClaimAsync(ServerType.Competitive, next.Id));
        }

        await using (var db = fixture.NewContext())
        {
            Assert.IsType<OkResult>(await ReportAsync(db, "host-token", new MatchZyEvent
            {
                Event = "series_end",
                MatchId = earlier.Id.ToString(),
                Team1SeriesScore = 1,
                Team2SeriesScore = 0,
            }));
        }

        Assert.Equal(MatchStatus.Finished, (await MatchAsync(earlier.Id)).Status);

        // still reserved for the match it is actually running
        var server = await ServerAsync(host.Id);
        Assert.Equal(next.Id, server.CurrentMatchId);
        Assert.Equal(ServerStatus.Reserved, server.Status);
    }
}
