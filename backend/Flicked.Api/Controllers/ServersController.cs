using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Flicked.Api.Controllers;

/* What a CS2 server calls, authenticated with its own token (X-Server-Token).

     POST /api/servers/heartbeat   "still here", every 30 seconds or so

   Nothing here accepts a player session: a person should never be able to speak
   as a server, and a server has no business reading a player's data. */
[ApiController]
[Route("api/servers")]
public class ServersController(
    FlickedDbContext db,
    ServerAuth auth,
    ILogger<ServersController> log) : ControllerBase
{
    public record HeartbeatResponse(string Status, int? MatchId);

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken ct)
    {
        var server = await auth.GetAsync(ct);
        if (server is null) return Unauthorized();

        var wasOffline = server.Status == ServerStatus.Offline;
        server.LastSeenAt = DateTimeOffset.UtcNow;

        /* A server that was written off comes back by itself. Only Offline is
           changed: a Reserved or Hosting server is mid-match, and a heartbeat is
           not the thing that ends a match. */
        if (wasOffline && server.IsEnabled) server.Status = ServerStatus.Idle;

        await db.SaveChangesAsync(ct);

        if (wasOffline) log.LogInformation("Server {Name} is back", server.Name);

        /* The reply tells the server what we think it is doing. A server that
           restarted mid-match learns there is a match it should be running, and
           one holding a stale match id learns to forget it. */
        return Ok(new HeartbeatResponse(server.Status.ToString(), server.CurrentMatchId));
    }
}
