using System.Security.Cryptography;
using System.Text;
using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Controllers;

/* Everything the side of the Play screen shows, in one request.

     GET /api/social/state

   This is the endpoint the launcher polls, so its cost is paid over and over by
   every signed-in client, and the things that make polling expensive are dealt
   with here rather than left to the client.

   One request rather than three. Friends, party and invites change at the same
   sort of rate and are drawn on the same screen, so asking for them separately
   would triple the session lookups and introduce the one bug this avoids
   entirely: a panel showing a friend list and a party fetched at different
   instants.

   Three indexed queries rather than a query per friend or per member. Friends
   and pending requests are rows in the same table with different statuses, so
   splitting them is a filter over a handful of rows in memory rather than a
   second round trip; the party reads its members in one statement; invites read
   both directions in another.

   The answer is ETagged. Nothing usually changes between polls, so the common
   case ends at 304 with no body: no serialisation, no transfer, and the client
   keeps what it has instead of re-rendering identical lists. The queries still
   run - the ETag is computed from the rows, not instead of them - so this saves
   everything above the database.

   When a push channel replaces this, the client keeps the same shape: a version
   changed, so reload. Only the transport moves. */
[ApiController]
[Route("api/social")]
public class SocialController(FlickedDbContext db, CurrentPlayer current, Social social) : ControllerBase
{
    /* The friend shapes are the ones /api/friends already returns, so a client
       has one Friend type rather than one per endpoint. */
    public record SocialState(
        List<FriendsController.FriendSummary> Friends,
        List<FriendsController.RequestSummary> Requests,
        Social.PartyView? Party,
        List<Social.InviteView> Invites);

    [HttpGet("state")]
    public async Task<IActionResult> State(CancellationToken ct)
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
            .Select(r => new FriendsController.FriendSummary(r.OtherId, r.Name, r.AvatarUrl, r.Rating))
            .ToList();

        var requests = rows.Where(r => r.Status == FriendshipStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new FriendsController.RequestSummary(r.OtherId, r.Name, r.AvatarUrl, r.CreatedAt, r.Incoming))
            .ToList();

        var (party, invites) = await social.PartyAndInvitesAsync(me.Id, ct);

        /* The ETag covers all four lists, so an invite arriving or a friend's
           rating changing counts as a change and the whole panel updates
           together. Built from the ordered lists that were just made, so it
           cannot disagree with the body it labels. */
        var etag = Etag(friends, requests, party, invites);
        if (Request.Headers.IfNoneMatch.Contains(etag)) return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = etag;
        return Ok(new SocialState(friends, requests, party, invites));
    }

    private static string Etag(List<FriendsController.FriendSummary> friends,
                               List<FriendsController.RequestSummary> requests,
                               Social.PartyView? party,
                               List<Social.InviteView> invites)
    {
        var text = new StringBuilder();
        foreach (var f in friends) text.Append(f.PlayerId).Append(':').Append(f.Rating).Append(':').Append(f.Name).Append(';');
        text.Append('|');
        foreach (var r in requests) text.Append(r.PlayerId).Append(':').Append(r.Incoming ? '1' : '0').Append(';');

        /* Not the expiry times: an invite refreshed by a second ask is the same
           invite on screen, and putting a clock in the ETag would answer 200
           with an identical body every time one ticked. */
        text.Append('|').Append(party?.Id).Append(':').Append(party?.LeaderId);
        foreach (var m in party?.Members ?? []) text.Append(m.PlayerId).Append(':').Append(m.Rating).Append(';');
        text.Append('|');
        foreach (var seat in party?.Invited ?? []) text.Append(seat.PlayerId).Append(';');
        text.Append('|');
        foreach (var i in invites) text.Append(i.PartyId).Append(':').Append(i.FromPlayerId).Append(';');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        // Quoted, because an ETag that is not quoted is not a valid one.
        return $"\"{Convert.ToHexString(hash)[..16]}\"";
    }
}
