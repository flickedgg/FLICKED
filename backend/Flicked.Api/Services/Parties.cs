using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* What a party is allowed to become.

   The rules live here rather than in the controller because the queue asks some
   of the same questions the party endpoints do, and because these are the parts
   worth testing against a real database: two people accepting an invite in the
   same instant is a race, not a request. */
public class Parties(FlickedDbContext db)
{
    /* A party a player is in, flat enough to check every rule against.

       Members are ordered by when they joined, which is both how the launcher
       draws them and who takes over when the leader leaves. */
    public record Snapshot(int Id, int LeaderId, List<int> MemberIds)
    {
        public int Size => MemberIds.Count;
        public bool IsLeader(int playerId) => playerId == LeaderId;
        public bool Holds(int playerId) => MemberIds.Contains(playerId);
    }

    /* The party this player is in, with its leader and every member.

       One query. The subquery is the unique index on PartyMember.PlayerId doing
       the "which party" lookup, so this is two index hits rather than two round
       trips, and everything that changes a party needs all of it. */
    public async Task<Snapshot?> OfAsync(int playerId, CancellationToken ct = default)
    {
        var mine = db.PartyMembers.Where(m => m.PlayerId == playerId).Select(m => m.PartyId);

        var rows = await db.PartyMembers
            .Where(m => mine.Contains(m.PartyId))
            .OrderBy(m => m.JoinedAt)
            .Select(m => new { m.PartyId, m.PlayerId, m.Party!.LeaderId })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Count == 0
            ? null
            : new Snapshot(rows[0].PartyId, rows[0].LeaderId, rows.Select(r => r.PlayerId).ToList());
    }

    /* The same, making a party of one if they are in none.

       A player who has never touched the party screen still queues as a party,
       so this is called on the way into the queue rather than at sign-in: a
       table of parties for everyone who ever logged in would be a table of
       nothing happening.

       If two requests from the same player race here, the unique index refuses
       the second insert and this reads back whichever party won, rather than
       checking first and hoping the gap between the check and the insert is
       short enough. */
    public async Task<Snapshot> EnsureForAsync(int playerId, CancellationToken ct = default)
    {
        if (await OfAsync(playerId, ct) is { } existing) return existing;

        var now = DateTimeOffset.UtcNow;
        var party = new Party
        {
            LeaderId = playerId,
            CreatedAt = now,
            Members = [new PartyMember { PlayerId = playerId, JoinedAt = now }],
        };

        db.Parties.Add(party);

        try
        {
            await db.SaveChangesAsync(ct);
            return new Snapshot(party.Id, playerId, [playerId]);
        }
        catch (DbUpdateException)
        {
            /* Detached by hand rather than by clearing the tracker: a later
               SaveChanges in this request must not retry the insert that was
               just refused, and must still write whatever else it is holding. */
            foreach (var member in party.Members) db.Entry(member).State = EntityState.Detached;
            db.Entry(party).State = EntityState.Detached;

            return await OfAsync(playerId, ct)
                ?? throw new InvalidOperationException($"Player {playerId} could not be put in a party.");
        }
    }
}
