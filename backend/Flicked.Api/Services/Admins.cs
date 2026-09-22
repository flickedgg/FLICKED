namespace Flicked.Api.Services;

/* Who runs this instance.

   The host lists their own Steam ID in configuration (.env or appsettings), and
   that account becomes an admin the next time it signs in. After that it is a
   database flag, so a second admin can be promoted without editing config and
   restarting.

   Configuration only ever *grants*: an id removed from the list keeps its flag
   until someone clears it deliberately. A typo in .env should not silently lock
   every admin out of a running instance. */
public class Admins(IConfiguration config)
{
    private readonly HashSet<string> _ids =
        (config["Admin:SteamIds"] ?? config["FLICKED_ADMIN_STEAM_IDS"] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.Ordinal);

    public bool Includes(string steamId) => _ids.Contains(steamId);
}
