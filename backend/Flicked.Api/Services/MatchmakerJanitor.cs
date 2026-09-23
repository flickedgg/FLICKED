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

    /* A match with a map and no server yet: claim one and tell it to load.

       This is the same sequence the dashboard's Start match button performs, with
       the matchmaker in place of an admin pressing it. */
    private async Task StartReadyMatchesAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<FlickedDbContext>();
        var pool = services.GetRequiredService<ServerPool>();
        var starter = services.GetRequiredService<MatchStarter>();

        var ready = await db.Matches
            .Where(m => m.Status == MatchStatus.Pending && m.ConfigTokenHash == null)
            .ToListAsync(ct);

        foreach (var match in ready)
        {
            var server = await pool.ClaimAsync(ServerType.Competitive, match.Id, ct);
            if (server is null)
            {
                /* Nothing free. The match stays Pending and is tried again on the
                   next pass: servers free up constantly, and cancelling ten
                   people's match because one was busy for two seconds would be a
                   miserable way to run a queue. */
                log.LogWarning("Match {MatchId} is waiting for a free server", match.Id);
                continue;
            }

            if (!await starter.StartAsync(server, match, ct))
            {
                // The server never got the command, so it must not stay claimed.
                await pool.ReleaseAsync(server.Id, ct);
                log.LogWarning("Match {MatchId}: {Server} could not be reached; will try another",
                    match.Id, server.Name);
            }
        }
    }
}
