using Flicked.Api.Models;
using Flicked.Api.Services;

namespace Flicked.Api.Tests;

/* Writing news from the dashboard.

   The slug maths is pure, so those cases are asserted on their own below. The
   rest needs a database, because what makes an id safe to publish is that no
   other row already has it. */
[Collection("pool")]
public class NewsTests(PoolFixture fixture)
{
    private static NewsPosts.Draft Draft(string title, string category = "patch",
                                         string? excerpt = null, string[]? body = null,
                                         DateOnly? date = null, string? badge = null) =>
        new(category, title, excerpt ?? "A summary.", body ?? ["A paragraph."], date, badge);

    [Theory]
    [InlineData("Alpha 0.3: bans", "alpha-0-3-bans")]
    [InlineData("Weekly 5v5 night, Fridays at 20:00 CET", "weekly-5v5-night-fridays-at-20-00-cet")]
    [InlineData("  Trimmed  ", "trimmed")]
    [InlineData("Lots -- of --- punctuation!!!", "lots-of-punctuation")]
    public void A_title_becomes_a_readable_id(string title, string expected)
    {
        Assert.Equal(expected, NewsPosts.Slug(title));
    }

    /* A title with nothing a URL can carry still has to produce a key, because
       the id is the primary key and the post has to save. */
    [Theory]
    [InlineData("!!!")]
    [InlineData("")]
    [InlineData("Добрый день")]
    public void A_title_with_no_usable_characters_still_gets_an_id(string title)
    {
        Assert.NotEmpty(NewsPosts.Slug(title));
    }

    /// Long headlines are cut rather than refused, and never end on a hyphen.
    [Fact]
    public void A_long_title_is_cut_to_something_a_url_can_hold()
    {
        var slug = NewsPosts.Slug(string.Join(' ', Enumerable.Repeat("word", 40)));

        Assert.True(slug.Length <= 60, $"slug was {slug.Length} characters");
        Assert.DoesNotContain("--", slug);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public async Task Publishes_a_post_with_an_id_made_from_its_title()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var post = await fixture.NewNews(db).CreateAsync(Draft("Alpha 0.3: bans"));

        Assert.Equal("alpha-0-3-bans", post.Id);
        Assert.Equal("patch", post.Category);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), post.Date);
        Assert.Null(post.Badge);
    }

    /* Two posts can honestly share a headline — a weekly event, say — and the id
       is the primary key, so the second has to be numbered rather than fail. */
    [Fact]
    public async Task A_repeated_headline_is_numbered_rather_than_refused()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);

        var first = await news.CreateAsync(Draft("Weekly 5v5 night"));
        var second = await news.CreateAsync(Draft("Weekly 5v5 night"));
        var third = await news.CreateAsync(Draft("Weekly 5v5 night"));

        Assert.Equal("weekly-5v5-night", first.Id);
        Assert.Equal("weekly-5v5-night-2", second.Id);
        Assert.Equal("weekly-5v5-night-3", third.Id);
    }

    /* The dashboard sends one textarea split on blank lines, so trailing blanks
       and padding are ordinary input. The launcher renders one paragraph per
       entry and would show an empty one as a gap. */
    [Fact]
    public async Task Blank_paragraphs_are_dropped_and_the_rest_trimmed()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var post = await fixture.NewNews(db)
            .CreateAsync(Draft("Tidied", body: ["  First.  ", "", "   ", "Second.", ""]));

        Assert.Equal(["First.", "Second."], post.Body);
    }

    [Fact]
    public async Task An_empty_badge_is_stored_as_nothing_rather_than_an_empty_string()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var post = await fixture.NewNews(db).CreateAsync(Draft("No badge", badge: "   "));

        Assert.Null(post.Badge);
    }

    /* The id is what the launcher has shown as read and what anyone who linked
       the post is holding, so correcting a headline must not move the post. */
    [Fact]
    public async Task Editing_a_title_keeps_the_original_id()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);
        var post = await news.CreateAsync(Draft("Alpha 0.3: bnas"));

        var edited = await news.EditAsync(post.Id, new NewsPosts.Edit(Title: "Alpha 0.3: bans"));

        Assert.NotNull(edited);
        Assert.Equal("alpha-0-3-bnas", edited.Id);
        Assert.Equal("Alpha 0.3: bans", edited.Title);
    }

    /// A field the dashboard did not send is a field it did not change.
    [Fact]
    public async Task An_edit_leaves_the_fields_it_does_not_mention_alone()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);
        var post = await news.CreateAsync(
            Draft("Untouched", category: "event", excerpt: "The summary.",
                  body: ["One.", "Two."], date: new DateOnly(2026, 9, 1), badge: "0.2"));

        var edited = await news.EditAsync(post.Id, new NewsPosts.Edit(Category: "update"));

        Assert.NotNull(edited);
        Assert.Equal("update", edited.Category);
        Assert.Equal("The summary.", edited.Excerpt);
        Assert.Equal(["One.", "Two."], edited.Body);
        Assert.Equal(new DateOnly(2026, 9, 1), edited.Date);
        Assert.Equal("0.2", edited.Badge);
    }

    /// An empty badge on an edit clears it; null would have left it alone, as above.
    [Fact]
    public async Task An_empty_badge_on_an_edit_clears_it()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);
        var post = await news.CreateAsync(Draft("Had a badge", badge: "0.2"));

        var edited = await news.EditAsync(post.Id, new NewsPosts.Edit(Badge: ""));

        Assert.NotNull(edited);
        Assert.Null(edited.Badge);
    }

    [Fact]
    public async Task Editing_or_removing_a_post_that_is_not_there_says_so()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);

        Assert.Null(await news.EditAsync("no-such-post", new NewsPosts.Edit(Title: "Hello")));
        Assert.False(await news.RemoveAsync("no-such-post"));
    }

    [Fact]
    public async Task Removes_a_post()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);
        var post = await news.CreateAsync(Draft("Short lived"));

        Assert.True(await news.RemoveAsync(post.Id));
        Assert.Empty(await news.ListAsync());
    }

    /* The reason CreatedAt exists.

       Date is editorial and two posts can share a day, which is ordinary once
       somebody is writing these by hand. Ordered on Date alone, Postgres is free
       to return the tied rows in any order, so the feed could reshuffle between
       two refreshes and the launcher's "Latest" card could change without
       anything being published. */
    [Fact]
    public async Task Two_posts_on_one_day_are_ordered_newest_written_first()
    {
        await fixture.ResetNewsAsync();
        var sameDay = new DateOnly(2026, 9, 20);

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);

        var earlier = await news.CreateAsync(Draft("Written first", date: sameDay));
        var later = await news.CreateAsync(Draft("Written second", date: sameDay));

        var feed = await news.ListAsync();

        Assert.Equal([later.Id, earlier.Id], feed.Select(p => p.Id));
    }

    [Fact]
    public async Task The_feed_is_newest_date_first()
    {
        await fixture.ResetNewsAsync();

        await using var db = fixture.NewContext();
        var news = fixture.NewNews(db);

        await news.CreateAsync(Draft("Old", date: new DateOnly(2026, 8, 1)));
        await news.CreateAsync(Draft("New", date: new DateOnly(2026, 9, 1)));
        await news.CreateAsync(Draft("Middle", date: new DateOnly(2026, 8, 20)));

        var feed = await news.ListAsync();

        Assert.Equal(["New", "Middle", "Old"], feed.Select(p => p.Title));
    }

    /* The dashboard fills its dropdown from this, and the launcher's stylesheet
       keys on the slugs in lower case (.news-meta.is-patch), so a capital here
       would silently drop the colour from every card. */
    [Fact]
    public void The_categories_are_the_three_the_launcher_renders()
    {
        Assert.Equal(["patch", "update", "event"], NewsPost.Categories.Select(c => c.Slug));
        Assert.All(NewsPost.Categories, c => Assert.Equal(c.Slug.ToLowerInvariant(), c.Slug));

        Assert.True(NewsPost.IsCategory("patch"));
        Assert.False(NewsPost.IsCategory("Patch"));
        Assert.False(NewsPost.IsCategory("announcement"));
        Assert.False(NewsPost.IsCategory(null));
    }
}
