using System.Text.RegularExpressions;

namespace Flicked.Api.Services;

public partial class SteamOpenId(HttpClient http, ILogger<SteamOpenId> log)
{
    private const string SteamLogin = "https://steamcommunity.com/openid/login";

    public static string BuildLoginUrl(string returnTo, string realm)
    {
        var query = new Dictionary<string, string?>
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnTo,
            ["openid.realm"] = realm,
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };
        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(SteamLogin, query);
    }

    public async Task<string?> VerifyAsync(IQueryCollection query, CancellationToken ct)
    {
        var form = query
            .Where(p => p.Key.StartsWith("openid.", StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => p.Value.ToString());

        if (form.Count == 0) return null;
        form["openid.mode"] = "check_authentication";

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(SteamLogin, new FormUrlEncodedContent(form), ct);
        }
        catch (HttpRequestException e)
        {
            log.LogWarning(e, "Could not reach Steam to verify a sign-in");
            return null;
        }

        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!body.Contains("is_valid:true", StringComparison.Ordinal))
        {
            log.LogWarning("Steam rejected an OpenID assertion");
            return null;
        }

        var claimedId = query["openid.claimed_id"].ToString();
        var match = SteamIdPattern().Match(claimedId);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^https?://steamcommunity\.com/openid/id/(\d{17})$")]
    private static partial Regex SteamIdPattern();
}
