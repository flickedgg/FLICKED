using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* Handing servers out, and taking them back.

   This is the heart of the pool, and the only place in FLICKED where two things
   can genuinely happen at the same instant: two matches filling up together and
   both reaching for the last free server. */
public class ServerPool(FlickedDbContext db, Rcon rcon, ServerSecrets secrets, ILogger<ServerPool> log)
{
    /// No heartbeat for this long and a server stops being handed out.
    public static readonly TimeSpan Silence = TimeSpan.FromSeconds(90);

    /* A claimed server waiting for players. Long enough for a map to load, ten
       people to connect and everyone to ready up, which five minutes was not. If
       the match never starts, the pool waits this long before taking it back. */
    public static readonly TimeSpan ReserveLease = TimeSpan.FromMinutes(20);

    /// A match in progress. Longer than any real CS2 match, so this only fires
    /// when something has gone wrong and nobody reported the result.
    public static readonly TimeSpan MatchLease = TimeSpan.FromMinutes(90);

    /* Takes a free server for a match, or returns null when there is none.

       The interesting line is FOR UPDATE SKIP LOCKED. Without it, two requests
       can read the same idle row, both decide it is free, and both claim it: ten
       players land on a server already running somebody else's match. A plain
       transaction does not help, because reading does not block reading.

       FOR UPDATE locks the row this transaction picked. SKIP LOCKED tells any
       other transaction to ignore locked rows and take the next free one instead
       of queueing behind us. The result is that concurrent claims hand out
       different servers, and nobody waits. */
    public async Task<GameServer?> ClaimAsync(ServerType type, int matchId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var alive = now - Silence;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        /* A server this match already holds comes back to it.

           Starting a match takes as long as a map change, and anything that
           interrupts it partway (a restart, a failed config fetch) leaves the
           server reserved with the match still Pending. Without this, the retry
           asks for a free server, finds its own sitting there marked Reserved,
           and waits for a lease that has twenty minutes to run: the match
           deadlocks against itself. */
        var held = await db.Servers
            .FromSql($"""
                SELECT * FROM "Servers"
                WHERE "CurrentMatchId" = {matchId}
                  AND "Status" = 'Reserved'
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .FirstOrDefaultAsync(ct);

        if (held is not null)
        {
            held.LeaseUntil = now + ReserveLease;
            await NoteServerAsync(matchId, held.Id, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            log.LogInformation("Server {Name} still held for match {MatchId}; reusing it", held.Name, matchId);
            return held;
        }

        // Raw SQL because EF has no way to express FOR UPDATE SKIP LOCKED.
        // Status and Type are stored as text (see FlickedDbContext).
        var server = await db.Servers
            .FromSql($"""
                SELECT * FROM "Servers"
                WHERE "IsEnabled"
                  AND "Status" = 'Idle'
                  AND "Type" = {type.ToString()}
                  AND "LastSeenAt" > {alive}
                ORDER BY "LastSeenAt" DESC
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .FirstOrDefaultAsync(ct);

        if (server is null)
        {
            await tx.RollbackAsync(ct);
            log.LogWarning("No free {Type} server for match {MatchId}", type, matchId);
            return null;
        }

        server.Status = ServerStatus.Reserved;
        server.CurrentMatchId = matchId;
        server.LeaseUntil = now + ReserveLease;
        await NoteServerAsync(matchId, server.Id, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        log.LogInformation("Server {Name} claimed for match {MatchId}", server.Name, matchId);
        return server;
    }

    /* Record on the match which server it went to, in the same transaction as
       the claim.

       The match's own copy, because the server's is cleared on release and this
       is what later says a result came from the server the match was sent to (see
       Match.ServerId). Saving it with the claim means there is no instant where a
       server is holding a match that does not know it. */
    private async Task NoteServerAsync(int matchId, int serverId, CancellationToken ct)
    {
        var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is not null) match.ServerId = serverId;
    }

    /// The players have connected: the lease becomes a match-length one.
    public async Task MarkHostingAsync(int serverId, CancellationToken ct = default)
    {
        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null || server.Status != ServerStatus.Reserved) return;

        server.Status = ServerStatus.Hosting;
        server.LeaseUntil = DateTimeOffset.UtcNow + MatchLease;
        await db.SaveChangesAsync(ct);
    }

    /// Back into the pool. Safe to call twice: releasing a free server does nothing.
    public async Task ReleaseAsync(int serverId, CancellationToken ct = default)
    {
        var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null) return;

        server.Status = ServerStatus.Idle;
        server.CurrentMatchId = null;
        server.LeaseUntil = null;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Server {Name} released", server.Name);
    }

    /* The safety net, run on a timer.

       Two failures it cleans up:

         a claim nobody ever used, or a match that never reported finishing: the
         lease runs out and the server goes back into the pool, because otherwise
         one crash quietly removes a server from the network forever;

         a server that has stopped talking to us: marked offline so it is not
         handed out. It comes back by itself on the next heartbeat.

       Returns what it changed, which is what the tests assert on. */
    /* Is each server actually there?

       MatchZy does not call us, so nothing would ever move a server out of
       Offline on its own. Rather than require a custom plugin, the backend asks:
       an RCON round trip proves the machine is up, the password still works and
       the port is open, which is exactly the channel a match needs. A server that
       answers becomes Idle; one that does not becomes Offline.

       Servers in a match are left alone: their lease decides, and a slow reply
       should not interrupt ten people playing. */
    public async Task<int> CheckAsync(CancellationToken ct = default)
    {
        var servers = await db.Servers
            .Where(s => s.IsEnabled && (s.Status == ServerStatus.Idle || s.Status == ServerStatus.Offline))
            .ToListAsync(ct);

        var changed = 0;
        foreach (var server in servers)
        {
            var password = secrets.TryDecrypt(server.RconPasswordEncrypted);
            if (password is null) continue;   // nothing to check with; admin must set it again

            // "echo" is the cheapest command that proves the whole path works
            var alive = await rcon.RunAsync(server.Host, server.Port, password, "echo flicked_ping", ct) is not null;
            var status = alive ? ServerStatus.Idle : ServerStatus.Offline;

            if (alive) server.LastSeenAt = DateTimeOffset.UtcNow;
            if (server.Status != status)
            {
                log.LogInformation("Server {Name} is now {Status}", server.Name, status);
                server.Status = status;
                changed++;
            }
        }

        if (servers.Count > 0) await db.SaveChangesAsync(ct);
        return changed;
    }

    public async Task<(int reclaimed, int offline)> SweepAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var expired = await db.Servers
            .Where(s => s.LeaseUntil != null && s.LeaseUntil < now)
            .ToListAsync(ct);

        foreach (var server in expired)
        {
            log.LogWarning("Lease expired on {Name} (match {MatchId}); returning it to the pool",
                server.Name, server.CurrentMatchId);
            server.Status = ServerStatus.Idle;
            server.CurrentMatchId = null;
            server.LeaseUntil = null;
        }

        // Quiet servers stop being handed out. Anything mid-match is left alone:
        // its lease is the thing that decides, not its silence.
        var quiet = await db.Servers
            .Where(s => s.Status == ServerStatus.Idle
                     && (s.LastSeenAt == null || s.LastSeenAt < now - Silence))
            .ToListAsync(ct);

        foreach (var server in quiet) server.Status = ServerStatus.Offline;

        if (expired.Count > 0 || quiet.Count > 0) await db.SaveChangesAsync(ct);
        return (expired.Count, quiet.Count);
    }
}
