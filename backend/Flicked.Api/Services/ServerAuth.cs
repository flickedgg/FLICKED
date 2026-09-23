using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* Identifying a CS2 server that is calling us.

   Players carry a session token; a game server carries its own, issued when an
   admin registered it. Same idea, different header: a server is not a person and
   should never be able to use a player's endpoints, or the reverse. */
public class ServerAuth(FlickedDbContext db, IHttpContextAccessor accessor)
{
    public const string Header = "X-Server-Token";

    /// The server making this request, or null. Only the hash is stored, so the
    /// incoming token is hashed and looked up, exactly like a session.
    public async Task<GameServer?> GetAsync(CancellationToken ct = default)
    {
        var token = accessor.HttpContext?.Request.Headers[Header].ToString();
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = Secrets.Hash(token.Trim());
        return await db.Servers.FirstOrDefaultAsync(s => s.TokenHash == hash, ct);
    }
}
