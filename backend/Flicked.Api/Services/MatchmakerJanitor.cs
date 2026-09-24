using System.Collections.Concurrent;
using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* The clock behind matchmaking.

   Every couple of seconds: build matches out of the queue, move matches whose
   phase has ended, and hand a server to any match that is ready for one.

   Same shape as PoolJanitor, and for the same reasons: a new DI scope per pass
   because a BackgroundService is a singleton and must not hold a DbContext, and
   exceptions swallowed because one bad pass must not stop the loop forever. */
public class MatchmakerJanitor(IServiceScopeFactory scopes, ILogger<MatchmakerJanitor> log)
    : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(Every);

        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var services = scope.ServiceProvider;

                var matchmaker = services.GetRequiredService<Matchmaker>();
                await matchmaker.FormMatchesAsync(stopping);
                await matchmaker.AdvancePhasesAsync(stopping);

                /* Invites end on a clock like the accept and vote windows do,
                   and this loop is already running on one with a scope open. One
                   indexed delete, almost always of nothing. */
                await services.GetRequiredService<Parties>().SweepExpiredInvitesAsync(stopping);

                await StartReadyMatchesAsync(services, stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                log.LogError(e, "Matchmaking pass failed; carrying on");
            }

            try { await timer.WaitForNextTickAsync(stopping); }
            catch (OperationCanceledException) { break; }
        }
    }

    /* Matches being started right now.

       Starting one takes as long as a map change, and the loop ticks every two
       seconds, so without this the same match would be picked up again and
       again, each pass claiming another server for it. */
    private static readonly ConcurrentDictionary<int, byte> Starting = new();

    /* A match with a map and no server ready yet: claim one and tell it to load.

       This is the same sequence the dashboard's Start match button performs, with
       the matchmaker in place of an admin pressing it.

       The filter is ServerReadyAt rather than the config token, so a match whose
       start failed is tried again on the next pass. A token alone used to be
       enough to mark a match as handled, which meant a single failure stranded it
       Pending for good. */
    private async Task StartReadyMatchesAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<FlickedDbContext>();

        var ready = await db.Matches
            .Where(m => m.Status == MatchStatus.Pending && m.ServerReadyAt == null)
            .Select(m => m.Id)
            .ToListAsync(ct);

        foreach (var id in ready)
        {
            if (!Starting.TryAdd(id, 0)) continue;

            /* Started off the loop, because waiting for a map takes up to a
               minute and everyone else's accept and vote timers run on this same
               tick. One match being set up must not stop the queue. */
            _ = Task.Run(() => StartOneAsync(id, ct), CancellationToken.None);
        }
    }

    private async Task StartOneAsync(int matchId, CancellationToken ct)
    {
        try
        {
            // Its own scope: this outlives the pass that started it, and a
            // DbContext belongs to one unit of work.
            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;

            var db = services.GetRequiredService<FlickedDbContext>();
            var pool = services.GetRequiredService<ServerPool>();
            var starter = services.GetRequiredService<MatchStarter>();

            var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
            if (match is null || match.Status != MatchStatus.Pending) return;

            var server = await pool.ClaimAsync(ServerType.Competitive, match.Id, ct);
            if (server is null)
            {
                /* Nothing free. The match stays Pending and is tried again on the
                   next pass: servers free up constantly, and cancelling ten
                   people's match because one was busy for two seconds would be a
                   miserable way to run a queue. */
                log.LogWarning("Match {MatchId} is waiting for a free server", match.Id);
                return;
            }

            if (!await starter.StartAsync(server, match, ct))
            {
                // The server never got the command, so it must not stay claimed.
                await pool.ReleaseAsync(server.Id, ct);
                log.LogWarning("Match {MatchId}: {Server} could not be reached; will try another",
                    match.Id, server.Name);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception e)
        {
            log.LogError(e, "Starting match {MatchId} failed", matchId);
        }
        finally
        {
            Starting.TryRemove(matchId, out _);
        }
    }
}
