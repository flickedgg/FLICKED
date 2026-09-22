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

        var request = accessor.HttpContext?.Request;
        if (request is null) return null;

        /* Two ways in, one kind of session. The launcher sends a bearer token kept
           in the OS keychain; the dashboard has a cookie, because a browser cannot
           hold a token safely. Both name the same row in Sessions. */
        var header = request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : request.Cookies["flicked.session"];

        if (string.IsNullOrEmpty(token)) return null;

        var hash = Secrets.Hash(token);
        var now = DateTimeOffset.UtcNow;

        var session = await db.Sessions
            .Include(s => s.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        return _cached = session is not null && session.IsActive(now) ? session.Player : null;
    }
}
