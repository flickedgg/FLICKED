using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Flicked.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
public class LeaderboardController : ControllerBase
{
    private readonly FlickedDbContext _db;
    public LeaderboardController(FlickedDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard(CancellationToken ct)
    {
        /* The last rating change comes along as a correlated subquery rather than
           a second round trip per player: Postgres runs it alongside the scan and
           the whole board stays one query, however long it gets. */
        var players = await _db.Players
            .OrderByDescending(p => p.Rating)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Rating,
                p.Wins,
                p.Losses,
                Delta = _db.MatchPlayers
                    .Where(mp => mp.PlayerId == p.Id && mp.Match!.Status == MatchStatus.Finished)
                    .OrderByDescending(mp => mp.Match!.PlayedAt)
                    .Select(mp => (int?)mp.RatingDelta)
                    .FirstOrDefault(),
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var leaderboard = players.Select((player, index) =>
        {
            var played = player.Wins + player.Losses;
            var winRate = played == 0 ? 0 : player.Wins * 100 / played;
            return new LeaderboardEntry(index + 1, player.Id, player.Name, player.Rating,
                                        player.Wins, player.Losses, winRate,
                                        player.Delta ?? 0, Rating.IsProvisional(played));
        }).ToList();

        return Ok(leaderboard);
    }
}