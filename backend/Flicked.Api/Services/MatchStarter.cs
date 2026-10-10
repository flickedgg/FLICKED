using Flicked.Api.Data;
using Flicked.Api.Models;

namespace Flicked.Api.Services;

/* Pointing a claimed server at a match.

   Pulled out of the dashboard controller so the matchmaker and an admin do
   exactly the same thing: mint a one-match token, then send one RCON command.
   Two copies of this would drift, and the copy nobody tests would be the one
   players actually go through. */
public class MatchStarter(
    FlickedDbContext db,
    ServerSecrets secrets,
    Rcon rcon,
    IConfiguration config,
    ILogger<MatchStarter> log)
{
    /* True when the server accepted the command. False means it could not be
       reached, and the caller should release it and try another.

       On failure the match's token is cleared again. It was minted before the
       first command, and a match that still carries one looks started to
       everything that reads it, so leaving it behind is what turns one missed
       RCON packet into a queue that never moves: the match sits Pending with a
       token nobody will ever use, and ten players wait forever. */
    public async Task<bool> StartAsync(GameServer server, Match match, CancellationToken ct = default)
    {
        if (await TryStartAsync(server, match, ct)) return true;

        match.ConfigTokenHash = null;
        match.ServerReadyAt = null;
        await db.SaveChangesAsync(CancellationToken.None);
        return false;
    }

    private async Task<bool> TryStartAsync(GameServer server, Match match, CancellationToken ct)
    {
        var password = secrets.TryDecrypt(server.RconPasswordEncrypted);
        if (password is null)
        {
            log.LogError("Server {Name} has an unreadable RCON password; set it again", server.Name);
            return false;
        }

        /* A fresh secret per match, stored hashed. It leaves here twice: in the
           RCON command, and back to us in the header of every event the server
           reports, which is what lets a stock MatchZy talk to FLICKED with
           nothing configured by hand. */
        var token = Secrets.New();
        match.ConfigTokenHash = Secrets.Hash(token);
        await db.SaveChangesAsync(ct);

        /* Give the server a clean MatchZy before loading anything.

           MatchZy sets isMatchSetup = true when a config loads and never sets it
           back (checked in its source, see docs/MATCHZY.md), so a server that
           has hosted one match refuses every later config for the life of the
           process: it keeps the old match and map while FLICKED believes the new
           one started, and players arrive at the wrong game.

           css_endmatch does not clear the flag either. Reloading the plugin builds
           a fresh instance, which does. It is heavy-handed, and it is the only
           thing that works without restarting the whole server. */
        if (!await ReloadMatchZyAsync(server, password, ct))
        {
            log.LogError("Server {Name}: MatchZy did not come back after a reload", server.Name);
            return false;
        }

        var url = $"{PublicUrl()}/api/matches/{match.Id}/config";

        /* The server fetches this URL itself, from another machine.

           A loopback address is the one value that is certainly wrong here and
           certainly looks right: the command is accepted, RCON answers with
           nothing, and the server quietly fetches its own empty port while
           FLICKED reports the match as started and ten players wait for a map
           that is never loaded. Better to refuse than to look successful. */
        if (url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
         || url.Contains("127.0.0.1") || url.Contains("://0.0.0.0"))
        {
            log.LogError(
                "Api:PublicUrl is {Url}, which a CS2 server cannot reach. Point it at the address " +
                "servers use to call back (a tunnel in development), or no match can be started.",
                PublicUrl());
            return false;
        }
        var command = $"matchzy_loadmatch_url \"{url}\" \"{MatchServerControllerTokens.Header}\" \"{token}\"";

        var reply = await rcon.RunAsync(server.Host, server.Port, password, command, ct,
                                        TimeSpan.FromSeconds(20));
        if (reply is null)
        {
            log.LogError("Server {Name} never took the config for match {MatchId}", server.Name, match.Id);
            return false;
        }

        /* RCON gives back whatever the console printed, which is usually nothing.
           When MatchZy does complain, it is worth noticing rather than reporting a
           match as started. */
        if (reply.Contains("already setup", StringComparison.OrdinalIgnoreCase)
         || reply.Contains("cannot load", StringComparison.OrdinalIgnoreCase))
        {
            /* The reply is a CS2 console's output, which is somebody else's text
               reaching our log: through Logs.OneLine, like a MatchZy event name. */
            log.LogError("Server {Name} refused match {MatchId}: {Reply}",
                server.Name, match.Id, Logs.OneLine(reply, 200));
            return false;
        }

        log.LogInformation("Match {MatchId} sent to {Server}", match.Id, server.Name);

        /* Wait for the level change before anyone is told to connect. A config is
           accepted in a moment; loading the map takes a good deal longer, and a
           player who joins in between lands on the previous match's map. */
        if (!await WaitForMapAsync(server, password, match.Map, ct))
        {
            log.LogError("Server {Name} never loaded {Map} for match {MatchId}", server.Name, match.Map, match.Id);
            return false;
        }

        match.ServerReadyAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Match {MatchId}: {Server} is on {Map}", match.Id, server.Name, match.Map);
        return true;
    }

    /// Asks the server what map it is on until it is the right one.
    private async Task<bool> WaitForMapAsync(GameServer server, string password, string map,
                                             CancellationToken ct)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            // "status" prints a line like: map     : de_inferno at: 0 x, 0 y, 0 z
            var status = await rcon.RunAsync(server.Host, server.Port, password, "status", ct);
            if (status is not null && status.Contains(map, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /* Reloads MatchZy and waits for it to come back.

       The wait matters: a reload unregisters every command the plugin owns and
       registers them again when it finishes loading. Sending the loadmatch a
       fixed number of seconds later is a guess, and when the guess is short the
       command is simply unknown, the server stays on whatever it had, and the
       RCON reply says nothing at all. So instead we ask until the plugin says it
       is loaded. */
    private async Task<bool> ReloadMatchZyAsync(GameServer server, string password, CancellationToken ct)
    {
        /* Generous timeout: a reload runs on the server's own thread, so the
           whole server stops answering while it happens. The default is sized
           for an echo, and using it here reports a healthy server as
           unreachable. */
        await rcon.RunAsync(server.Host, server.Port, password, "css_plugins reload MatchZy", ct,
                            TimeSpan.FromSeconds(30));

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);

            var plugins = await rcon.RunAsync(server.Host, server.Port, password, "css_plugins list", ct);
            if (plugins is not null
                && plugins.Contains("MatchZy", StringComparison.OrdinalIgnoreCase)
                && plugins.Contains("LOADED", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /* The address a CS2 server can reach us on. Not localhost: the server is on
       another machine, and in development that means a tunnel. */
    private string PublicUrl() =>
        (config["Api:PublicUrl"] ?? config["FLICKED_API_PUBLIC_URL"]
         ?? config["Steam:PublicUrl"] ?? "http://localhost:5165").TrimEnd('/');
}

/// Kept here so both the controller and the starter name the same header.
public static class MatchServerControllerTokens
{
    public const string Header = "X-Match-Token";
}
