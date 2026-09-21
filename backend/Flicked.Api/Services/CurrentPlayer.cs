using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

// Turns the `Authorization: Bearer <token>` header into a Player, or nothing. 
public class CurrentPlayer(FlickedDbContext db, IHttpContextAccessor accessor)
{
    private Player? _cached;
    private bool _looked;

    /* The signed-in player, or null. Cached per request, because several places
       (a controller and a filter, say) may ask during one request and there is no
       reason to hit the database twice. A new CurrentPlayer is created per request,
       so the cache cannot leak between players. */
    public async Task<Player?> GetAsync(CancellationToken ct = default)
    {
        if (_looked) return _cached;
        _looked = true;

        var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header["Bearer ".Length..].Trim();
        if (token.Length == 0) return null;

        var hash = Secrets.Hash(token);
        var now = DateTimeOffset.UtcNow;

        var session = await db.Sessions
            .Include(s => s.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        return _cached = session is not null && session.IsActive(now) ? session.Player : null;
    }
}
