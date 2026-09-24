using System.Security.Cryptography;
using System.Text;
using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Controllers;

/* Friends.

     GET    /api/friends                    people you are friends with
     GET    /api/friends/requests           pending, both directions
     GET    /api/friends/search?q=          find someone, with your relationship to them
     POST   /api/friends/requests/{id}      send a request to that player
     POST   /api/friends/requests/{id}/accept
     DELETE /api/friends/requests/{id}      decline an incoming one, or cancel your own
     DELETE /api/friends/{id}               remove a friend
*/
[ApiController]
[Route("api/friends")]
public class FriendsController(FlickedDbContext db, CurrentPlayer current) : ControllerBase
{
    public record FriendSummary(int PlayerId, string Name, string? AvatarUrl, int Rating);

    public record RequestSummary(int PlayerId, string Name, string? AvatarUrl,
                                 DateTimeOffset CreatedAt, bool Incoming);

    public record SearchResult(int PlayerId, string Name, string? AvatarUrl, int Rating,
                               string Relationship);

    public record FriendsState(List<FriendSummary> Friends, List<RequestSummary> Requests);

    /* Everything the friends panel shows, in one request.

       This is the endpoint the launcher polls, so its cost is paid over and over
       by every signed-in client, and the two things that made polling expensive
       are dealt with here rather than in the client:

         one request instead of two. Each request authenticates, which is its own
         indexed lookup, so asking for friends and requests separately doubled the
         work to answer a question that is always asked as one;

         one database query instead of two. Friends and pending requests are rows
         in the same table with different statuses, and the split is a filter in
         memory over a handful of rows, not a second round trip.

       The answer is ETagged. Nothing usually changes between polls, so the common
       case ends at 304 with no body: no serialisation, no transfer, and the client
       keeps what it has instead of re-rendering an identical list. The query still
       runs - the ETag is computed from the rows, not instead of them - so this
       saves everything above the database, and the database work is two indexed
       lookups.

       When a push channel replaces this, the client keeps the same shape: a
       version changed, so reload. Only the transport moves. */
    [HttpGet("state")]
    public async Task<IActionResult> GetState(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        /* Scalars rather than the whole Player: this runs on a timer, and
           selecting the navigation would fetch every column of every friend to
           show a name, an avatar and a rating. */
        var rows = await db.Friendships
            .Where(f => (f.Status == FriendshipStatus.Accepted || f.Status == FriendshipStatus.Pending)
                     && (f.RequesterId == me.Id || f.AddresseeId == me.Id))
            .Select(f => new
            {
                f.Status,
                f.CreatedAt,
                Incoming = f.AddresseeId == me.Id,
                OtherId = f.RequesterId == me.Id ? f.AddresseeId : f.RequesterId,
                Name = f.RequesterId == me.Id ? f.Addressee!.Name : f.Requester!.Name,
                AvatarUrl = f.RequesterId == me.Id ? f.Addressee!.AvatarUrl : f.Requester!.AvatarUrl,
                Rating = f.RequesterId == me.Id ? f.Addressee!.Rating : f.Requester!.Rating,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var friends = rows.Where(r => r.Status == FriendshipStatus.Accepted)
            .OrderBy(r => r.Name)
            .Select(r => new FriendSummary(r.OtherId, r.Name, r.AvatarUrl, r.Rating))
            .ToList();

        var requests = rows.Where(r => r.Status == FriendshipStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RequestSummary(r.OtherId, r.Name, r.AvatarUrl, r.CreatedAt, r.Incoming))
            .ToList();

        /* The ETag covers everything the panel draws, so a friend's rating or
           avatar changing counts as a change. Built from the ordered lists that
           were just made, so it cannot disagree with the body it labels. */
        var etag = Etag(friends, requests);
        if (Request.Headers.IfNoneMatch.Contains(etag)) return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = etag;
        return Ok(new FriendsState(friends, requests));
    }

    private static string Etag(List<FriendSummary> friends, List<RequestSummary> requests)
    {
        var text = new StringBuilder();
        foreach (var f in friends) text.Append(f.PlayerId).Append(':').Append(f.Rating).Append(':').Append(f.Name).Append(';');
        text.Append('|');
        foreach (var r in requests) text.Append(r.PlayerId).Append(':').Append(r.Incoming ? '1' : '0').Append(';');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        // Quoted, because an ETag that is not quoted is not a valid one.
        return $"\"{Convert.ToHexString(hash)[..16]}\"";
    }

    [HttpGet]
    public async Task<IActionResult> GetFriends(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        // A friendship can be stored in either direction, so both columns are
        // checked and whichever side is not you is the friend.
        var friends = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted
                     && (f.RequesterId == me.Id || f.AddresseeId == me.Id))
            .Select(f => f.RequesterId == me.Id ? f.Addressee! : f.Requester!)
            .OrderBy(p => p.Name)
            .Select(p => new FriendSummary(p.Id, p.Name, p.AvatarUrl, p.Rating))
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(friends);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        var requests = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending
                     && (f.RequesterId == me.Id || f.AddresseeId == me.Id))
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new RequestSummary(
                f.RequesterId == me.Id ? f.AddresseeId : f.RequesterId,
                f.RequesterId == me.Id ? f.Addressee!.Name : f.Requester!.Name,
                f.RequesterId == me.Id ? f.Addressee!.AvatarUrl : f.Requester!.AvatarUrl,
                f.CreatedAt,
                f.AddresseeId == me.Id))     // incoming when you are the addressee
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(requests);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        q = (q ?? "").Trim();
        if (q.Length < 2) return Ok(Array.Empty<SearchResult>());

        var found = await db.Players
            .Where(p => EF.Functions.ILike(p.Name, $"%{q}%") || p.SteamId == q)
            .OrderBy(p => p.Name)
            .Take(20)
            .AsNoTracking()
            .ToListAsync(ct);

        var ids = found.Select(p => p.Id).ToList();

        var links = await db.Friendships
            .Where(f => (f.RequesterId == me.Id && ids.Contains(f.AddresseeId))
                     || (f.AddresseeId == me.Id && ids.Contains(f.RequesterId)))
            .AsNoTracking()
            .ToListAsync(ct);

        var results = found.Select(p =>
        {
            var link = links.FirstOrDefault(f => f.OtherId(me.Id) == p.Id);
            var relationship = p.Id == me.Id ? "self"
                : link is null ? "none"
                : link.Status == FriendshipStatus.Accepted ? "friends"
                : link.RequesterId == me.Id ? "requested"    
                : "incoming";                                 
            return new SearchResult(p.Id, p.Name, p.AvatarUrl, p.Rating, relationship);
        });

        return Ok(results);
    }

    [HttpPost("requests/{playerId:int}")]
    public async Task<IActionResult> SendRequest(int playerId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();
        if (playerId == me.Id) return BadRequest("You cannot add yourself.");

        if (!await db.Players.AnyAsync(p => p.Id == playerId, ct))
            return NotFound("No such player.");

        var existing = await db.Friendships.FirstOrDefaultAsync(
            f => (f.RequesterId == me.Id && f.AddresseeId == playerId)
              || (f.RequesterId == playerId && f.AddresseeId == me.Id), ct);

        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Pending && existing.AddresseeId == me.Id)
            {
                existing.Status = FriendshipStatus.Accepted;
                existing.RespondedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return Ok(new { status = "friends" });
            }

            return Conflict(existing.Status == FriendshipStatus.Accepted
                ? "You are already friends."
                : "There is already a request between you.");
        }

        db.Friendships.Add(new Friendship
        {
            RequesterId = me.Id,
            AddresseeId = playerId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return Ok(new { status = "requested" });
    }

    [HttpPost("requests/{playerId:int}/accept")]
    public async Task<IActionResult> Accept(int playerId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        var request = await db.Friendships.FirstOrDefaultAsync(
            f => f.RequesterId == playerId && f.AddresseeId == me.Id
              && f.Status == FriendshipStatus.Pending, ct);

        if (request is null) return NotFound("No request from that player.");

        request.Status = FriendshipStatus.Accepted;
        request.RespondedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { status = "friends" });
    }

    [HttpDelete("requests/{playerId:int}")]
    public async Task<IActionResult> RemoveRequest(int playerId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        var request = await db.Friendships.FirstOrDefaultAsync(
            f => f.Status == FriendshipStatus.Pending
              && ((f.RequesterId == me.Id && f.AddresseeId == playerId)
               || (f.RequesterId == playerId && f.AddresseeId == me.Id)), ct);

        if (request is null) return NotFound("No pending request with that player.");

        db.Friendships.Remove(request);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{playerId:int}")]
    public async Task<IActionResult> RemoveFriend(int playerId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        var friendship = await db.Friendships.FirstOrDefaultAsync(
            f => f.Status == FriendshipStatus.Accepted
              && ((f.RequesterId == me.Id && f.AddresseeId == playerId)
               || (f.RequesterId == playerId && f.AddresseeId == me.Id)), ct);

        if (friendship is null) return NotFound("You are not friends with that player.");

        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
