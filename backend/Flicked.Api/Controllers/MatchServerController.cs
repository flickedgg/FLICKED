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
    IConfiguration config,
    ILogger<MatchServerController> log) : ControllerBase
{
    /* Where a CS2 server should reach this API: a public address it can resolve,
       which in development is a tunnel rather than localhost. Same setting the
       start-match command uses when it points a server at its config. */
    private string PublicUrl() =>
        (config["Api:PublicUrl"] ?? config["FLICKED_API_PUBLIC_URL"]
         ?? config["Steam:PublicUrl"] ?? "http://localhost:5165").TrimEnd('/');

    /* The match config, in MatchZy's format.

       Token-protected rather than public: it lists every player's Steam ID, and
       matchzy_loadmatch_url can send a header, so there is no reason to leave it
       open. */
    public const string ConfigTokenHeader = "X-Match-Token";

    [HttpGet("{id:int}/config")]
    public async Task<IActionResult> Config(int id, CancellationToken ct)
    {
        /* Authenticated by the per-match token, not the server's: the command that
           sends a server here has to carry a secret in plaintext, and the server's
           own token only exists as a hash. */
        var presented = Request.Headers[ConfigTokenHeader].ToString();
        if (string.IsNullOrWhiteSpace(presented)) return Unauthorized();

        var match = await db.Matches
            .Include(m => m.Players).ThenInclude(mp => mp.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (match is null) return NotFound();

        if (match.ConfigTokenHash is null || match.ConfigTokenHash != Secrets.Hash(presented.Trim()))
        {
            log.LogWarning("Bad config token for match {MatchId}", id);
            return Unauthorized();
        }

        /* Players are keyed by Steam64: it is the only identifier MatchZy accepts.
           Anyone without one (the seeded demo players) is left out rather than
           sent as something invalid. */
        static Dictionary<string, string> Roster(IEnumerable<MatchPlayer> players) =>
            players
                .Where(p => !string.IsNullOrEmpty(p.Player?.SteamId))
                .ToDictionary(p => p.Player!.SteamId!, p => p.Player!.Name);

        var bothTeamsPresent = match.Players.Any(p => p.Team == 0) && match.Players.Any(p => p.Team == 1);

        return Ok(new
        {
            matchid = match.Id.ToString(),
            num_maps = 1,                       // FLICKED plays one map per match
            maplist = new[] { match.Map },
            /* A knife round needs two teams to fight it, and hangs forever without
               them: the match never goes live and no events are sent. Real matches
               knife for sides; a half-empty one (a test, or a scrim short of
               players) starts straight away instead. */
            map_sides = bothTeamsPresent ? new[] { "knife" } : new[] { "team1_ct" },
            players_per_team = Math.Max(1, match.Players.Count / 2),
            team1 = new { name = "Team A", players = Roster(match.Players.Where(p => p.Team == 0)) },
            team2 = new { name = "Team B", players = Roster(match.Players.Where(p => p.Team == 1)) },
            /* MatchZy applies these before it sends its first event, so the
               reporting settings ride along with the match instead of having to be
               edited into server.cfg by hand.

               The credential is the token this request arrived with: the caller
               already knows it, it is worth exactly one match, and the server's
               long-lived token never has to leave the dashboard. */
            cvars = new Dictionary<string, string>
            {
                ["hostname"] = $"FLICKED #{match.Id}",
                ["matchzy_remote_log_url"] = $"{PublicUrl()}/api/matches/events",
                ["matchzy_remote_log_header_key"] = ConfigTokenHeader,
                ["matchzy_remote_log_header_value"] = presented.Trim(),
            },
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
        if (string.IsNullOrWhiteSpace(body.Event)) return BadRequest("No event.");
        if (!int.TryParse(body.MatchId, out var matchId)) return BadRequest("No match id.");

        var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == matchId, ct);
        if (match is null) return NotFound();

        /* Two ways a server can prove itself:

             its own long-lived token, set up by an admin, or
             the per-match token the match config handed it.

           The second is what makes this work with stock MatchZy: the config we
           serve tells the server where to report and what to send, so nothing has
           to be configured on the server by hand. */
        var server = await auth.GetAsync(ct);
        var presented = Request.Headers[ConfigTokenHeader].ToString().Trim();
        var matchTokenOk = presented.Length > 0
                        && match.ConfigTokenHash is not null
                        && match.ConfigTokenHash == Secrets.Hash(presented);

        if (server is null && !matchTokenOk)
        {
            /* Logged loudly on purpose. MatchZy neither retries nor reports a
               rejected post to anyone who will see it, so a wrong credential is
               otherwise silent at both ends. */
            log.LogWarning("Event {Event} for match {MatchId} rejected: no valid token", body.Event, matchId);
            return Unauthorized();
        }

        // With only a match token we still want the server, to release it later.
        server ??= await db.Servers.FirstOrDefaultAsync(s => s.CurrentMatchId == matchId, ct);

        /* Normally the server reporting is the one holding the match. It may not
           be: if nothing extended the lease, the sweep will have released it while
           the match was still being played. Throwing the result away in that case
           would lose a real game that really happened, so a finished match is
           still saved. Anything else is ignored, since the server is not the
           authority on a match it no longer holds. */
        var holdsMatch = server is null || server.CurrentMatchId == matchId;
        if (!holdsMatch)
        {
            log.LogWarning("Server {Name} reported {Event} for match {MatchId}, which it no longer holds",
                server!.Name, body.Event, matchId);

            if (body.Event is "series_end" or "map_result" && match.Status != MatchStatus.Finished)
            {
                await SaveResultAsync(match, body, ct);
                log.LogInformation("Match {MatchId} finished {A}-{B} (late report)", matchId, match.ScoreA, match.ScoreB);
            }
            return Ok();
        }

        switch (body.Event)
        {
            case "going_live":
                if (match.Status == MatchStatus.Pending)
                {
                    match.Status = MatchStatus.Live;
                    match.PlayedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                    // the five-minute "waiting for players" lease becomes a match-length one
                    if (server is not null) await pool.MarkHostingAsync(server.Id, ct);
                    log.LogInformation("Match {MatchId} is live on {Server}", matchId, server?.Name ?? "an unknown server");
                }
                break;

            /* map_result carries the score and every player's stats; series_end
               carries only the series score and a winner (see MatchZySeriesResultEvent
               in the plugin). With one map per match, map_result is the one worth
               saving, and series_end is what says the server is free again.

               Both are handled because either can arrive first, or alone: MatchZy
               does not retry, so a lost map_result must not mean a match that never
               finishes. */
            case "map_result":
                if (match.Status != MatchStatus.Finished)
                {
                    await SaveResultAsync(match, body, ct);
                    log.LogInformation("Match {MatchId} finished {A}-{B}", matchId, match.ScoreA, match.ScoreB);
                }
                break;

            case "series_end":
                if (match.Status != MatchStatus.Finished)
                {
                    // no per-player stats here; the scores are the series ones
                    match.ScoreA = body.Team1SeriesScore ?? match.ScoreA;
                    match.ScoreB = body.Team2SeriesScore ?? match.ScoreB;
                    match.Status = MatchStatus.Finished;
                    await db.SaveChangesAsync(ct);
                    log.LogWarning("Match {MatchId} ended without a map_result; saved the series score only", matchId);
                }
                if (server is not null) await pool.ReleaseAsync(server.Id, ct);
                log.LogInformation("Match {MatchId} is over; {Server} released", matchId, server?.Name ?? "no server");
                break;

            default:
                /* round_end, series_start, player_disconnect and the rest: accepted
                   and not stored yet. Logged at information rather than debug so
                   that "is the server reporting at all?" is answerable from the
                   ordinary log, which is the first question whenever this breaks. */
                log.LogInformation("Event {Event} received for match {MatchId}", body.Event, matchId);
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
