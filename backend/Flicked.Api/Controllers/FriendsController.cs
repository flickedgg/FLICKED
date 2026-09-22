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
