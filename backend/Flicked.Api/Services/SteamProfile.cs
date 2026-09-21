using System.Text.Json;

namespace Flicked.Api.Services;

public record SteamProfileInfo(string PersonaName, string? AvatarUrl);

/* Steam's Web API, which is a different thing from the OpenID sign-in.
   OpenID answers "which account is this?"; this answers "what is it called?".

   It needs an API key the host creates at https://steamcommunity.com/dev/apikey.
   Without one, this returns null and sign-in still works: the player just gets a
   placeholder name. A self-hoster who does not want to register for a key should
   still get a working launcher. */
public class SteamProfile(HttpClient http, IConfiguration config, ILogger<SteamProfile> log)
{
    // Steam:ApiKey comes from appsettings or user-secrets, STEAM_API_KEY from .env
    // or the environment. Checking both means neither way of running this is wrong.
    private string? ApiKey => config["Steam:ApiKey"] ?? config["STEAM_API_KEY"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task<SteamProfileInfo?> FetchAsync(string steamId, CancellationToken ct)
    {
        if (!IsConfigured) return null;

        var url = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/"
                + $"?key={ApiKey}&steamids={steamId}";

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await http.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Steam profile lookup returned {Status}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);

            var players = json.RootElement.GetProperty("response").GetProperty("players");
            if (players.GetArrayLength() == 0) return null;

            var player = players[0];
            var name = player.TryGetProperty("personaname", out var p) ? p.GetString() : null;
            var avatar = player.TryGetProperty("avatarfull", out var a) ? a.GetString() : null;

            return string.IsNullOrWhiteSpace(name) ? null : new SteamProfileInfo(name, avatar);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            log.LogWarning(e, "Could not read the Steam profile for {SteamId}", steamId);
            return null;
        }
    }
}
