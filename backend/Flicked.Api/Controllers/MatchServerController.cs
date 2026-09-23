using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Controllers;

/* The two endpoints a CS2 server talks to about a match.

     GET  /api/matches/{id}/config   MatchZy fetches this with matchzy_loadmatch_url
     POST /api/matches/events        MatchZy posts every event here

   Both authenticate with the server's own token (X-Server-Token), and both check
   that the server asking is the one the match was given to. Without that second
   check any registered server could read another match's roster or report a
   result for a game it never hosted.

   See backend/MATCHZY.md for the payload shapes and where they came from. */
[ApiController]
[Route("api/matches")]
public class MatchServerController(
    FlickedDbContext db,
    ServerAuth auth,
    ServerPool pool,
    ILogger<MatchServerController> log) : ControllerBase
{
    /* The match config, in MatchZy's format.

       Token-protected rather than public: it lists every player's Steam ID, and
       matchzy_loadmatch_url can send a header, so there is no reason to leave it
       open. */
    [HttpGet("{id:int}/config")]
    public async Task<IActionResult> Config(int id, CancellationToken ct)
    {
        var server = await auth.GetAsync(ct);
        if (server is null) return Unauthorized();

        var match = await db.Matches
            .Include(m => m.Players).ThenInclude(mp => mp.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (match is null) return NotFound();

        // A server may only read the match it was actually given.
        if (server.CurrentMatchId != match.Id)
        {
            log.LogWarning("Server {Name} asked for match {MatchId}, which is not its own", server.Name, id);
            return Forbidden();
        }

        /* Players are keyed by Steam64: it is the only identifier MatchZy accepts.
           Anyone without one (the seeded demo players) is left out rather than
           sent as something invalid. */
        static Dictionary<string, string> Roster(IEnumerable<MatchPlayer> players) =>
            players
                .Where(p => !string.IsNullOrEmpty(p.Player?.SteamId))
                .ToDictionary(p => p.Player!.SteamId!, p => p.Player!.Name);

        return Ok(new
        {
            matchid = match.Id.ToString(),
            num_maps = 1,                       // FLICKED plays one map per match
            maplist = new[] { match.Map },
            map_sides = new[] { "knife" },      // knife round decides sides
            players_per_team = Math.Max(1, match.Players.Count / 2),
            team1 = new { name = "Team A", players = Roster(match.Players.Where(p => p.Team == 0)) },
            team2 = new { name = "Team B", players = Roster(match.Players.Where(p => p.Team == 1)) },
            cvars = new Dictionary<string, string> { ["hostname"] = $"FLICKED #{match.Id}" },
        });
    }

    /* Everything MatchZy has to say about a match.

       Two facts from the plugin's source shape this (see MATCHZY.md):

         it never retries and never deduplicates, so a slow or failing answer
         here loses that event for good: the work must be quick, and a repeat
         must be harmless;

         which means every branch below is written to be safe to run twice. A
         second series_end finds the match already Finished and does nothing. */
    [HttpPost("events")]
    public async Task<IActionResult> Events([FromBody] MatchZyEvent body, CancellationToken ct)
    {
        var server = await auth.GetAsync(ct);
        if (server is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Event)) return BadRequest("No event.");
        if (!int.TryParse(body.MatchId, out var matchId)) return BadRequest("No match id.");

        if (server.CurrentMatchId != matchId)
        {
            /* Not an error worth failing on: a server restarting can replay an
               event for a match we already finished and released. Logged and
               accepted, so the plugin does not keep hold of it. */
            log.LogWarning("Server {Name} reported {Event} for match {MatchId}, which is not its own",
                server.Name, body.Event, matchId);
            return Ok();
        }

        var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null) return NotFound();

        switch (body.Event)
        {
            case "going_live":
                if (match.Status == MatchStatus.Pending)
                {
                    match.Status = MatchStatus.Live;
                    match.PlayedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                    // the five-minute "waiting for players" lease becomes a match-length one
                    await pool.MarkHostingAsync(server.Id, ct);
                    log.LogInformation("Match {MatchId} is live on {Server}", matchId, server.Name);
                }
                break;

            case "series_end":
            case "map_result":
                // one map per match, so either event carries the final score
                if (match.Status != MatchStatus.Finished)
                {
                    await SaveResultAsync(match, body, ct);
                    await pool.ReleaseAsync(server.Id, ct);
                    log.LogInformation("Match {MatchId} finished {A}-{B}", matchId, match.ScoreA, match.ScoreB);
                }
                break;

            default:
                // round_end, player_disconnect, demo_upload_ended and the rest:
                // accepted so the plugin stops, stored when there is a use for them
                log.LogDebug("Ignoring {Event} for match {MatchId}", body.Event, matchId);
                break;
        }

        return Ok();
    }

    private async Task SaveResultAsync(Match match, MatchZyEvent body, CancellationToken ct)
    {
        match.ScoreA = body.Team1?.Score ?? 0;
        match.ScoreB = body.Team2?.Score ?? 0;
        match.Status = MatchStatus.Finished;

        var rows = await db.MatchPlayers.Where(mp => mp.MatchId == match.Id).ToListAsync(ct);

        // Steam ID is how the plugin names people; FLICKED keys on player id.
        var steamIds = (body.Team1?.Players ?? []).Concat(body.Team2?.Players ?? [])
            .Select(p => p.SteamId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        var players = await db.Players
            .Where(p => p.SteamId != null && steamIds.Contains(p.SteamId))
            .ToDictionaryAsync(p => p.SteamId!, ct);

        void Apply(MatchZyTeam? team, int side)
        {
            foreach (var reported in team?.Players ?? [])
            {
                if (reported.SteamId is null || !players.TryGetValue(reported.SteamId, out var player)) continue;

                // The row usually exists (we created the match); a player who
                // joined late would not have one, so it is created here.
                var row = rows.FirstOrDefault(r => r.PlayerId == player.Id);
                if (row is null)
                {
                    row = new MatchPlayer { MatchId = match.Id, PlayerId = player.Id };
                    db.MatchPlayers.Add(row);
                    rows.Add(row);
                }

                row.Team = side;
                row.Kills = reported.Stats?.Kills ?? 0;
                row.Deaths = reported.Stats?.Deaths ?? 0;
                row.Adr = reported.Stats?.Adr ?? 0;
                // RatingDelta stays 0 until there is a rating system to compute it
            }
        }

        Apply(body.Team1, 0);
        Apply(body.Team2, 1);

        await db.SaveChangesAsync(ct);
    }

    // StatusCode(403) rather than Forbid(): this app registers no authentication
    // scheme, and Forbid() asks for a challenge it cannot produce.
    private IActionResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden, "Not your match.");
}
