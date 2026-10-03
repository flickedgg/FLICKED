using System.Text;
using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* Writing, editing and removing news.

   The rules a post has to obey live here rather than in the controller, so the
   dashboard and anything else that ever writes a post (a release script, say)
   cannot disagree about what a valid one looks like. */
public class NewsPosts(FlickedDbContext db, ILogger<NewsPosts> log)
{
    /* The feed, newest first.

       Date decides the order, CreatedAt only breaks a tie between two posts
       written on the same day — see NewsPost.CreatedAt for why that matters. */
    public async Task<List<NewsPost>> ListAsync(CancellationToken ct = default) =>
        await db.News
            .OrderByDescending(p => p.Date)
            .ThenByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    /* What an admin fills in. Date is optional and defaults to today; Badge is
       optional outright. Everything else the controller has already checked. */
    public record Draft(string Category, string Title, string Excerpt, string[] Body,
                        DateOnly? Date = null, string? Badge = null);

    /* An edit. Null means "leave this alone", which is what lets the dashboard
       send only the fields it changed. Body arrives whole, because paragraphs
       have no stable identity to patch one of them by. */
    public record Edit(string? Category = null, string? Title = null, string? Excerpt = null,
                       string[]? Body = null, DateOnly? Date = null, string? Badge = null);

    public async Task<NewsPost> CreateAsync(Draft draft, CancellationToken ct = default)
    {
        var post = new NewsPost
        {
            Id = await FreeSlugAsync(Slug(draft.Title), ct),
            Category = draft.Category,
            Date = draft.Date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Badge = Blank(draft.Badge),
            Title = draft.Title.Trim(),
            Excerpt = draft.Excerpt.Trim(),
            Body = Paragraphs(draft.Body),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.News.Add(post);
        await db.SaveChangesAsync(ct);

        log.LogInformation("News post {Id} published ({Category})", post.Id, post.Category);
        return post;
    }

    /// The edited post, or null when there is no post with that id.
    public async Task<NewsPost?> EditAsync(string id, Edit edit, CancellationToken ct = default)
    {
        var post = await db.News.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return null;

        /* The id is deliberately not recomputed from a new title. It is what the
           launcher shows as read and what anyone who linked the post is holding,
           so a corrected typo in a headline must not turn into a dead link. */
        if (edit.Category is not null) post.Category = edit.Category;
        if (edit.Title is not null) post.Title = edit.Title.Trim();
        if (edit.Excerpt is not null) post.Excerpt = edit.Excerpt.Trim();
        if (edit.Body is not null) post.Body = Paragraphs(edit.Body);
        if (edit.Date is not null) post.Date = edit.Date.Value;

        // "" clears the badge, null leaves it alone — the same rule the server
        // form uses for a game password.
        if (edit.Badge is not null) post.Badge = Blank(edit.Badge);

        await db.SaveChangesAsync(ct);
        log.LogInformation("News post {Id} edited", post.Id);
        return post;
    }

    /// True if a post was removed, false if there was nothing there.
    public async Task<bool> RemoveAsync(string id, CancellationToken ct = default)
    {
        var post = await db.News.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return false;

        db.News.Remove(post);
        await db.SaveChangesAsync(ct);

        log.LogInformation("News post {Id} removed", post.Id);
        return true;
    }

    /* A title turned into an id: "Alpha 0.3: bans" → "alpha-0-3-bans".

       Lower case, letters and digits kept, everything else collapsed into single
       hyphens, and cut to a length that still reads in a URL. A title with no
       Latin letters at all (an emoji, or Cyrillic) would leave nothing, so it
       falls back to "post" and FreeSlugAsync numbers it. */
    public static string Slug(string title)
    {
        var slug = new StringBuilder(title.Length);

        foreach (var c in title.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c)) slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }

        var trimmed = slug.ToString().Trim('-');
        if (trimmed.Length > 60) trimmed = trimmed[..60].TrimEnd('-');

        return trimmed.Length == 0 ? "post" : trimmed;
    }

    /* That slug, or the first numbered variant nobody is using.

       Two posts can honestly share a headline ("Weekly 5v5 night" every week),
       and the id is the primary key, so the second one has to become
       "weekly-5v5-night-2" rather than fail to save. */
    private async Task<string> FreeSlugAsync(string slug, CancellationToken ct)
    {
        var taken = await db.News
            .Where(p => p.Id == slug || p.Id.StartsWith(slug + "-"))
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (!taken.Contains(slug)) return slug;

        for (var n = 2; ; n++)
        {
            var candidate = $"{slug}-{n}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    /* The body as the launcher wants it: trimmed paragraphs, no empty ones.

       The dashboard sends one textarea split on blank lines, so a stray blank
       line at the end is normal input, not a mistake worth refusing. */
    private static string[] Paragraphs(string[] body) =>
        [.. body.Select(p => p.Trim()).Where(p => p.Length > 0)];

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
