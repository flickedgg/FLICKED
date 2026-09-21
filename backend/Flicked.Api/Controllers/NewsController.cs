using Flicked.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Flicked.Api.Data;
using Microsoft.EntityFrameworkCore;    
namespace Flicked.Api.Controllers;

[ApiController]
[Route("api/news")]

public class NewsController : ControllerBase
{
    private readonly FlickedDbContext _db;

    public NewsController(FlickedDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetNewsPosts()
    {
        var posts = await _db.News
            .OrderByDescending(p => p.Date)
            .AsNoTracking()
            .ToListAsync();
        return Ok(posts);
    }
}
