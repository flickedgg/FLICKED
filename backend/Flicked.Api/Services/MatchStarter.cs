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
    /// True when the server accepted the command. False means it could not be
    /// reached, and the caller should release it and try another.
    public async Task<bool> StartAsync(GameServer server, Match match, CancellationToken ct = default)
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

        var url = $"{PublicUrl()}/api/matches/{match.Id}/config";
        var command = $"matchzy_loadmatch_url \"{url}\" \"{MatchServerControllerTokens.Header}\" \"{token}\"";

        var reply = await rcon.RunAsync(server.Host, server.Port, password, command, ct);
        if (reply is null) return false;

        log.LogInformation("Match {MatchId} sent to {Server}", match.Id, server.Name);
        return true;
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
