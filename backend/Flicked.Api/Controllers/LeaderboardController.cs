using Flicked.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
namespace Flicked.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
public class LeaderboardController : ControllerBase
{
    private static readonly LeaderboardEntry[] Leaderboard = [
       new LeaderboardEntry(1, "kovac", 2614, 311, 68),
new LeaderboardEntry(2, "Halden", 2571, 287, 66),
new LeaderboardEntry(3, "Nyx", 2498, 264, 64),
new LeaderboardEntry(4, "sprayz", 2402, 240, 62),
new LeaderboardEntry(5, "quietus", 2366, 198, 61),
new LeaderboardEntry(6, "reload", 2291, 215, 59),
new LeaderboardEntry(7, "Brine", 2240, 176, 59),
new LeaderboardEntry(8, "m0th", 2187, 169, 58),
new LeaderboardEntry(9, "lowground", 2105, 151, 56),
new LeaderboardEntry(10, "patchnote", 2050, 143, 55),
    ];

    [HttpGet]
    public IActionResult GetLeaderboard()
    {
        return Ok(Leaderboard);
    }
}