using Flicked.Api.Models;
using Microsoft.AspNetCore.Mvc;
namespace Flicked.Api.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetNewsPosts()
    {
        var newsPosts = new List<NewsPost>
        {
            new NewsPost("1", "Update", "2024-06-01", null, "New Feature Released", "We are excited to announce a new feature...", new[] { "Body content of the news post." }),
            new NewsPost("2", "Announcement", "2024-05-15", "Important", "Upcoming Maintenance", "Scheduled maintenance will occur...", new[] { "Details about the maintenance." })
        };
        return Ok(newsPosts);
    }
}