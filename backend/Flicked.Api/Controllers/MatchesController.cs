using Flicked.Api.Models;
using Flicked.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
namespace Flicked.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    private readonly FlickedDbContext _db;
    public MatchesController(FlickedDbContext db)
    {
        _db = db;
    }

    // playerId is a query parameter until accounts exist (0.4), when it becomes the signed-in player.
    [HttpGet]
    public async Task<IActionResult> GetMatches([FromQuery] int playerId = 1)
    {
        // Starts from MatchPlayers: one row there is exactly one player's view of one match.
        // The Select reaches across to Match, so EF writes a JOIN and fetches only these columns.
        var rows = await _db.MatchPlayers
            .Where(mp => mp.PlayerId == playerId)
            .OrderByDescending(mp => mp.Match!.PlayedAt)
            .Select(mp => new
            {
                mp.MatchId,
                mp.Match!.Map,
                mp.Match.ScoreA,
                mp.Match.ScoreB,
                mp.Match.PlayedAt,
                mp.Team,
                mp.Kills,
                mp.Deaths,
                mp.Adr,
                mp.RatingDelta,
            })
            .AsNoTracking()
            .ToListAsync();

        // Shaped here rather than in the query: string building doesn't translate to SQL.
        var matches = rows.Select(r =>
        {
            var onTeamA = r.Team == 0;
            var mine = onTeamA ? r.ScoreA : r.ScoreB;
            var theirs = onTeamA ? r.ScoreB : r.ScoreA;
            return new MatchSummary(
                r.MatchId,
                r.Map,
                mine > theirs ? "W" : "L",
                $"{mine}-{theirs}",
                $"{r.Kills}/{r.Deaths}",
                r.Adr,
                r.RatingDelta,
                r.PlayedAt);
        }).ToList();

        return Ok(matches);
    }
}
