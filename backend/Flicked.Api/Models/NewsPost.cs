namespace Flicked.Api.Models;

/* A post on the News screen.

   Written by an admin in the dashboard, read by every launcher. The field names
   here are the ones the launcher's News view reads, so renaming one is a change
   to both (launcher/src/data/demo.ts holds the matching type).

   Settable properties rather than the positional record this used to be: a post
   is now edited after it is written, which is what every other editable row in
   this project looks like. */
public class NewsPost
{
    /* A slug made from the title ("alpha-0-3-bans"), not a number.

       The launcher has always carried this as a string (the seeded rows are
       "n1"…"n6"), and a readable id survives being quoted in a Discord message
       or a bug report. NewsPosts.CreateAsync is what makes it unique, and an
       edit never changes it: the id is the one thing anything else might hold. */
    public string Id { get; set; } = "";

    /// One of Categories, stored as the slug the launcher's stylesheet keys on.
    public string Category { get; set; } = "";

    /// The date on the card. An admin may back-date a post, or post ahead.
    public DateOnly Date { get; set; }

    /// Watermark on the featured card, usually a version number. Optional.
    public string? Badge { get; set; }

    public string Title { get; set; } = "";

    /// The one line under the title, on the card and above the article.
    public string Excerpt { get; set; } = "";

    /// One entry per paragraph, in order.
    public string[] Body { get; set; } = [];

    /* Why there are two date-ish columns.

       Date is editorial: it can be back-dated, and two posts can share a day,
       which is ordinary once somebody is writing these by hand. Postgres returns
       rows in no particular order when the sort key ties, so a feed ordered on
       Date alone can reshuffle between two refreshes. This breaks the tie, and
       is never shown. */
    public DateTimeOffset CreatedAt { get; set; }

    /* The categories, slug first and the label the launcher shows second.

       Not an enum, because the slug is what the launcher's stylesheet already
       matches on in lower case (.news-meta.is-patch) and what its NewsCategory
       union already spells; an enum stored as a string would write "Patch" and
       quietly lose the colour on every card. This is the one list: the dashboard
       fetches it rather than hard-coding its own copy. */
    public static readonly (string Slug, string Label)[] Categories =
    [
        ("patch", "Patch notes"),
        ("update", "Updates"),
        ("event", "Events"),
    ];

    public static bool IsCategory(string? slug) =>
        slug is not null && Array.Exists(Categories, c => c.Slug == slug);
}
