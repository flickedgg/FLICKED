using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Numerics;
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
    public async Task<IActionResult> GetLeaderboard()
    {
        var players = await _db.Players
        .OrderByDescending(p => p.Rating)
        .AsNoTracking()
        .ToListAsync();
        var leaderboard = players.Select((player, index) =>
        {
        var played = player.Wins + player.Losses;
        var winRate = played == 0 ? 0 : player.Wins * 100 / played;
        return new LeaderboardEntry(index + 1, player.Id, player.Name, player.Rating, player.Wins, winRate);
        }).ToList();
        return Ok(leaderboard);
    }
}