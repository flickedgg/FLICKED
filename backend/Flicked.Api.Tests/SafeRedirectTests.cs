using Flicked.Api.Controllers;
using Flicked.Api.Data;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flicked.Api.Tests;

/* Where an admin lands after signing in.

   /auth/web/login takes a returnTo so somebody sent to sign in from a page comes
   back to that page. A returnTo naming another site would make FLICKED bounce a
   freshly signed-in admin wherever the link said, which is a phishing page
   reached through a URL that really is yours.

   Driven through the endpoint rather than by reaching for the check itself,
   because the check and the absolute origin its caller puts in front of it are
   only safe together, and only the endpoint has both. */
public class SafeRedirectTests
{
    private const string Api = "https://flicked.test";

    /* The endpoint touches neither the database nor Steam: it builds a URL and
       redirects. The context is here because the controller asks for one, and EF
       opens no connection until something is queried. */
    private static AuthController NewController()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Steam:PublicUrl"] = Api,
                ["Dashboard:Url"] = "https://dash.flicked.test",
            })
            .Build();

        var db = new FlickedDbContext(
            new DbContextOptionsBuilder<FlickedDbContext>().UseNpgsql("Host=not-connected").Options);

        return new AuthController(
            db,
            new SteamOpenId(new HttpClient(), NullLogger<SteamOpenId>.Instance),
            new SteamProfile(new HttpClient(), config, NullLogger<SteamProfile>.Instance),
            new Admins(config),
            new CurrentPlayer(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }),
            config,
            NullLogger<AuthController>.Instance);
    }

    /* The returnTo that survived, read back out of the sign-in URL.

       It travels to Steam inside openid.return_to, as the query of the callback
       Steam is asked to send the browser to, so it comes back out the same way. */
    private static string? Surviving(string? returnTo)
    {
        var answer = NewController().WebLogin(returnTo);
        var steam = new Uri(Assert.IsType<RedirectResult>(answer).Url);

        var callback = QueryHelpers.ParseQuery(steam.Query)["openid.return_to"].Single()!;
        return QueryHelpers.ParseQuery(new Uri(callback).Query)["returnTo"].Single();
    }

    [Theory]
    // a whole URL, which is the attack the check exists for
    [InlineData("https://evil.example")]
    [InlineData("http://evil.example/login")]
    // no scheme needed: a browser reads this as a host, not a path
    [InlineData("//evil.example")]
    /* Nor does it need two slashes. A browser folds a backslash into a forward
       one while picking a URL apart, so "/\" is read as "//". Harmless while an
       origin is prepended, refused anyway so that it does not come down to
       whether the next caller remembers to prepend one. */
    [InlineData("/\\evil.example")]
    [InlineData("/\\\\evil.example/path")]
    // not a path at all
    [InlineData("\\\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_that_is_not_a_path_on_the_dashboard_becomes_the_front_page(string? returnTo) =>
        Assert.Equal("/", Surviving(returnTo));

    [Theory]
    [InlineData("/")]
    [InlineData("/servers")]
    [InlineData("/news")]
    // a query and a fragment are part of the page somebody was on
    [InlineData("/servers?page=2")]
    [InlineData("/news#n3")]
    public void A_path_on_the_dashboard_is_kept(string returnTo) =>
        Assert.Equal(returnTo, Surviving(returnTo));

    /// Whatever it was, it is a path on the dashboard that gets redirected to.
    [Theory]
    [InlineData("https://evil.example", "https://dash.flicked.test/")]
    [InlineData("/servers", "https://dash.flicked.test/servers")]
    public void The_redirect_is_built_from_our_own_dashboard(string returnTo, string expected) =>
        Assert.Equal(expected, $"https://dash.flicked.test{Surviving(returnTo)}");
}
