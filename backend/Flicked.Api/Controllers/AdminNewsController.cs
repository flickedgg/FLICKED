using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Flicked.Api.Controllers;

/* Writing news, from the dashboard.

     POST   /api/admin/news        publish a post
     PATCH  /api/admin/news/{id}   edit one
     DELETE /api/admin/news/{id}   remove one

   There is no GET here on purpose. A post is live the moment it is saved, so the
   admin list is the same list players see, and the dashboard reads the public
   GET /api/news: one query, and no way for the dashboard to show something the
   launcher would not.
    */
[ApiController]
[Route("api/admin/news")]
public class AdminNewsController(
    CurrentPlayer current,
    NewsPosts news) : ControllerBase
{
    public record NewsForm(
        string? Category, string? Title, string? Excerpt, string[]? Body,
        DateOnly? Date, string? Badge);

    [HttpPost]
    public async Task<IActionResult> Publish([FromBody] NewsForm form, CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        if (string.IsNullOrWhiteSpace(form.Title)) return BadRequest("A title is required.");
        if (string.IsNullOrWhiteSpace(form.Excerpt)) return BadRequest("A summary is required.");
        if (!NewsPost.IsCategory(form.Category)) return BadRequest(CategoryError);
        if (NoParagraphs(form.Body)) return BadRequest("A post needs at least one paragraph.");

        var post = await news.CreateAsync(
            new NewsPosts.Draft(form.Category!, form.Title, form.Excerpt, form.Body!, form.Date, form.Badge),
            ct);

        return Ok(post);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Edit(string id, [FromBody] NewsForm form, CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        if (form.Title is not null && string.IsNullOrWhiteSpace(form.Title))
            return BadRequest("A title is required.");
        if (form.Excerpt is not null && string.IsNullOrWhiteSpace(form.Excerpt))
            return BadRequest("A summary is required.");
        if (form.Category is not null && !NewsPost.IsCategory(form.Category))
            return BadRequest(CategoryError);
        if (form.Body is not null && NoParagraphs(form.Body))
            return BadRequest("A post needs at least one paragraph.");

        var post = await news.EditAsync(
            id,
            new NewsPosts.Edit(form.Category, form.Title, form.Excerpt, form.Body, form.Date, form.Badge),
            ct);

        return post is null ? NotFound("No such post.") : Ok(post);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken ct)
    {
        if (await Denied(ct) is { } denial) return denial;

        return await news.RemoveAsync(id, ct) ? NoContent() : NotFound("No such post.");
    }

    private static bool NoParagraphs(string[]? body) =>
        body is null || !body.Any(p => !string.IsNullOrWhiteSpace(p));

    private static string CategoryError =>
        "Category must be one of: " + string.Join(", ", NewsPost.Categories.Select(c => c.Slug)) + ".";

    private async Task<IActionResult?> Denied(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();
        return me.IsAdmin ? null : StatusCode(StatusCodes.Status403Forbidden, "Admins only.");
    }
}
