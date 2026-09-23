using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Tests;

[Collection("pool")]
public class ServerPoolTests(PoolFixture fixture)
{
    /* CurrentMatchId is a foreign key, so a claim has to name a match that exists.
       These two come from the seed data every database gets on migration. */
    private const int Match1 = 48213;
    private const int Match2 = 48190;

    [Fact]
    public async Task Claims_a_free_server()
    {
        await fixture.ResetServersAsync();
        await fixture.AddServerAsync("one");

        await using var db = fixture.NewContext();
        var claimed = await fixture.NewPool(db).ClaimAsync(ServerType.Competitive, matchId: Match1);

        Assert.NotNull(claimed);
        Assert.Equal(ServerStatus.Reserved, claimed.Status);
        Assert.Equal(Match1, claimed.CurrentMatchId);
        Assert.NotNull(claimed.LeaseUntil);
    }

    /* The reason the pool exists in this shape.

       Two matches fill up at the same instant and both reach for a server. With a
       plain "find idle, then update" they can both read the same row and both
       believe they have it, and ten players end up on a server already running
       somebody else's match. FOR UPDATE SKIP LOCKED is what stops that, and this
       is the test that would fail if somebody removed it. */
    [Fact]
    public async Task Two_matches_claiming_at_once_get_different_servers()
    {
        await fixture.ResetServersAsync();
        await fixture.AddServerAsync("a");
        await fixture.AddServerAsync("b");

        // Separate contexts, so these are genuinely two connections racing.
        await using var db1 = fixture.NewContext();
        await using var db2 = fixture.NewContext();

        var both = await Task.WhenAll(
            fixture.NewPool(db1).ClaimAsync(ServerType.Competitive, matchId: Match1),
            fixture.NewPool(db2).ClaimAsync(ServerType.Competitive, matchId: Match2));

        Assert.All(both, Assert.NotNull);
        Assert.NotEqual(both[0]!.Id, both[1]!.Id);

        // and the database agrees: one server per match, no sharing
        await using var check = fixture.NewContext();
        var claimed = await check.Servers.Where(s => s.CurrentMatchId != null).ToListAsync();
        Assert.Equal(2, claimed.Count);
        Assert.Equal(2, claimed.Select(s => s.CurrentMatchId).Distinct().Count());
    }

    [Fact]
    public async Task Only_one_wins_when_there_is_a_single_server()
    {
        await fixture.ResetServersAsync();
        await fixture.AddServerAsync("only");

        await using var db1 = fixture.NewContext();
        await using var db2 = fixture.NewContext();

        var both = await Task.WhenAll(
            fixture.NewPool(db1).ClaimAsync(ServerType.Competitive, matchId: Match1),
            fixture.NewPool(db2).ClaimAsync(ServerType.Competitive, matchId: Match2));

        // the loser gets null, not the same server: matchmaking must wait, not double-book
        Assert.Single(both.Where(s => s is not null));
    }

    [Theory]
    [InlineData(ServerStatus.Offline)]   // not heard from
    [InlineData(ServerStatus.Hosting)]   // busy
    public async Task Never_claims_a_server_that_is_not_idle(ServerStatus status)
    {
        await fixture.ResetServersAsync();
        await fixture.AddServerAsync("busy", status);

        await using var db = fixture.NewContext();
        Assert.Null(await fixture.NewPool(db).ClaimAsync(ServerType.Competitive, matchId: Match1));
    }

    [Fact]
    public async Task Never_claims_a_disabled_server()
    {
        await fixture.ResetServersAsync();
        await fixture.AddServerAsync("off", enabled: false);

        await using var db = fixture.NewContext();
        Assert.Null(await fixture.NewPool(db).ClaimAsync(ServerType.Competitive, matchId: Match1));
    }

    /// Idle but silent for too long: the process may be gone without saying so.
    [Fact]
    public async Task Never_claims_a_server_that_stopped_talking()
    {
        await fixture.ResetServersAsync();
        await fixture.AddServerAsync("quiet", lastSeen: DateTimeOffset.UtcNow - ServerPool.Silence * 2);

        await using var db = fixture.NewContext();
        Assert.Null(await fixture.NewPool(db).ClaimAsync(ServerType.Competitive, matchId: Match1));
    }

    [Fact]
    public async Task Releasing_puts_it_back()
    {
        await fixture.ResetServersAsync();
        var server = await fixture.AddServerAsync("recycled");

        await using var db = fixture.NewContext();
        var pool = fixture.NewPool(db);

        await pool.ClaimAsync(ServerType.Competitive, matchId: Match1);
        await pool.ReleaseAsync(server.Id);

        await using var check = fixture.NewContext();
        var after = await check.Servers.SingleAsync(s => s.Id == server.Id);
        Assert.Equal(ServerStatus.Idle, after.Status);
        Assert.Null(after.CurrentMatchId);
        Assert.Null(after.LeaseUntil);

        // and it can be claimed again
        await using var db2 = fixture.NewContext();
        Assert.NotNull(await fixture.NewPool(db2).ClaimAsync(ServerType.Competitive, matchId: Match2));
    }

    /* A server crashes mid-match and nobody ever reports the result. Without the
       sweep it stays Hosting forever and silently leaves the pool. */
    [Fact]
    public async Task Sweep_reclaims_a_server_whose_lease_ran_out()
    {
        await fixture.ResetServersAsync();
        var server = await fixture.AddServerAsync("stuck", ServerStatus.Hosting);

        await using (var setup = fixture.NewContext())
        {
            var stuck = await setup.Servers.SingleAsync(s => s.Id == server.Id);
            stuck.CurrentMatchId = null;                                   // no real match row needed
            stuck.LeaseUntil = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1);
            await setup.SaveChangesAsync();
        }

        await using var db = fixture.NewContext();
        var (reclaimed, _) = await fixture.NewPool(db).SweepAsync();

        Assert.Equal(1, reclaimed);

        await using var check = fixture.NewContext();
        var after = await check.Servers.SingleAsync(s => s.Id == server.Id);
        Assert.Equal(ServerStatus.Idle, after.Status);
        Assert.Null(after.LeaseUntil);
    }

    [Fact]
    public async Task Sweep_marks_silent_servers_offline()
    {
        await fixture.ResetServersAsync();
        await fixture.AddServerAsync("gone", lastSeen: DateTimeOffset.UtcNow - ServerPool.Silence * 2);
        await fixture.AddServerAsync("here");

        await using var db = fixture.NewContext();
        var (_, offline) = await fixture.NewPool(db).SweepAsync();

        Assert.Equal(1, offline);

        await using var check = fixture.NewContext();
        Assert.Equal(ServerStatus.Offline, (await check.Servers.SingleAsync(s => s.Name == "gone")).Status);
        Assert.Equal(ServerStatus.Idle, (await check.Servers.SingleAsync(s => s.Name == "here")).Status);
    }

    /// A match in progress is judged by its lease, not by how chatty it is.
    [Fact]
    public async Task Sweep_leaves_a_live_match_alone()
    {
        await fixture.ResetServersAsync();
        var server = await fixture.AddServerAsync("playing", ServerStatus.Hosting,
                                                  lastSeen: DateTimeOffset.UtcNow - ServerPool.Silence * 2);

        await using (var setup = fixture.NewContext())
        {
            var playing = await setup.Servers.SingleAsync(s => s.Id == server.Id);
            playing.LeaseUntil = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30);
            await setup.SaveChangesAsync();
        }

        await using var db = fixture.NewContext();
        await fixture.NewPool(db).SweepAsync();

        await using var check = fixture.NewContext();
        Assert.Equal(ServerStatus.Hosting, (await check.Servers.SingleAsync(s => s.Id == server.Id)).Status);
    }
}
