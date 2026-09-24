using Flicked.Api.Models;
using Flicked.Api.Data;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
namespace Flicked.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    private readonly FlickedDbContext _db;
    private readonly CurrentPlayer _current;
    public MatchesController(FlickedDbContext db, CurrentPlayer current)
    {
        _db = db;
        _current = current;
    }

    /* Your own history, and only yours.
    The player comes from the session token, never from the request: a playerId
    parameter would let anyone read anyone else's matches just by changing a
    number. Anything the caller could choose is not an identity. */
    
    [HttpGet]
    public async Task<IActionResult> GetMatches(CancellationToken ct)
    {
        var player = await _current.GetAsync(ct);
        if (player is null) return Unauthorized();
        var playerId = player.Id;

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

        var matches = rows.Select(r =>
        {
            var onTeamA = r.Team == 0;
            var mine = onTeamA ? r.ScoreA : r.ScoreB;
            var theirs = onTeamA ? r.ScoreB : r.ScoreA;
            return new MatchSummary(
                r.MatchId,
                r.Map,
                // MR12 without overtime can end 12-12, and a draw is not a loss
                mine > theirs ? "W" : mine < theirs ? "L" : "D",
                $"{mine}-{theirs}",
                $"{r.Kills}/{r.Deaths}",
                r.Adr,
                r.RatingDelta,
                r.PlayedAt);
        }).ToList();

        return Ok(matches);
    }
}
