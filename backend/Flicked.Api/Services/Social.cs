using Flicked.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* Reading party state for a screen.

   Separate from Parties, which decides what may change: this only ever asks,
   and it is asked on a timer by every signed-in launcher, so the shape of these
   two queries is the thing being optimised. Both project the columns that are
   drawn and nothing else, and neither grows a query per member.

   It lives in a service rather than in the controller because two endpoints
   need the same answer: the social poll, and the party endpoints replying with
   what they just changed. */
public class Social(FlickedDbContext db)
{
    /// Somebody in a party, or holding a seat in it while they think about it.
    public record PartySeat(int PlayerId, string Name, string? AvatarUrl, int Rating, string Division);

    public record PartyView(int Id, int LeaderId, List<PartySeat> Members, List<PartySeat> Invited);

    /// An invite waiting for you, and who asked.
    public record InviteView(int PartyId, int FromPlayerId, string FromName, string? FromAvatarUrl,
                             DateTimeOffset ExpiresAt);

    public async Task<PartyView?> PartyAsync(int playerId, CancellationToken ct = default) =>
        (await PartyAndInvitesAsync(playerId, ct)).Party;

    /* Your party, the seats it is holding open, and the invites waiting for you.

       Two queries. The first finds the party through the unique index on
       PartyMember.PlayerId and reads its members in the same statement; the
       second reads both directions of invite at once, since an invite you sent
       and an invite you were sent are the same table and the same poll. */
    public async Task<(PartyView? Party, List<InviteView> Invites)> PartyAndInvitesAsync(
        int playerId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var mine = db.PartyMembers.Where(m => m.PlayerId == playerId).Select(m => m.PartyId);

        var members = await db.PartyMembers
            .Where(m => mine.Contains(m.PartyId))
            .OrderBy(m => m.JoinedAt)
            .Select(m => new
            {
                m.PartyId,
                m.PlayerId,
                m.Party!.LeaderId,
                m.Player!.Name,
                m.Player.AvatarUrl,
                m.Player.Rating,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        int? partyId = members.Count == 0 ? null : members[0].PartyId;

        /* Expired invites are ignored rather than waited on: the janitor sweeps
           them within a couple of seconds, and until it does, an invite past its
           time must not be offered to anybody. */
        var invites = await db.PartyInvites
            .Where(i => i.ExpiresAt > now && (i.ToPlayerId == playerId || i.PartyId == partyId))
            .OrderBy(i => i.ExpiresAt)
            .Select(i => new
            {
                i.PartyId,
                i.ToPlayerId,
                i.FromPlayerId,
                i.ExpiresAt,
                ToName = i.ToPlayer!.Name,
                ToAvatarUrl = i.ToPlayer.AvatarUrl,
                ToRating = i.ToPlayer.Rating,
                FromName = i.FromPlayer!.Name,
                FromAvatarUrl = i.FromPlayer.AvatarUrl,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var waitingFor = invites
            .Where(i => i.PartyId == partyId && i.ToPlayerId != playerId)
            .Select(i => new PartySeat(i.ToPlayerId, i.ToName, i.ToAvatarUrl, i.ToRating,
                                       Divisions.For(i.ToRating)))
            .ToList();

        var party = partyId is null
            ? null
            : new PartyView(
                partyId.Value,
                members[0].LeaderId,
                members.Select(m => new PartySeat(m.PlayerId, m.Name, m.AvatarUrl, m.Rating,
                                                  Divisions.For(m.Rating))).ToList(),
                waitingFor);

        var waiting = invites
            .Where(i => i.ToPlayerId == playerId)
            .Select(i => new InviteView(i.PartyId, i.FromPlayerId, i.FromName, i.FromAvatarUrl, i.ExpiresAt))
            .ToList();

        return (party, waiting);
    }
}
