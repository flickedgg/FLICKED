using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Flicked.Api.Controllers;

/* The news feed, as every launcher reads it.

   Public, and the same list the dashboard shows an admin: posts go live when
   they are saved, so there is no second version of this to keep in step.
   Writing them is AdminNewsController. */
[ApiController]
[Route("api/news")]
public class NewsController(NewsPosts news) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNewsPosts(CancellationToken ct) =>
        Ok(await news.ListAsync(ct));

    /* The categories a post may have, slug and label.

       Here so the dashboard's dropdown is filled from the one list in NewsPost
       instead of a second copy that drifts out of step and starts offering a
       category the API then refuses. Public because it is the same three words
       already printed on every news card. */
    [HttpGet("categories")]
    public IActionResult Categories() =>
        Ok(NewsPost.Categories.Select(c => new { slug = c.Slug, label = c.Label }));
}
