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
    /* An invite nobody answers stops existing. Two minutes is long enough to
       alt-tab out of a game and back, and short enough that an invite on screen
       is an invite to a party that still looks like that. */
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromMinutes(2);

    /* At most this many invites out at once. Not manners: every invite is a row
       the invitee's poll reads, so an unbounded list is a way to make somebody
       else's launcher expensive. */
    public const int MaxInvites = 10;

    /// The most members a party may hold: one team's worth of seats.
    public static int MaxMembers => Matchmaker.TeamSizeFor(ServerType.Competitive);

    /* Why something was refused. The service answers with one of these and the
       controller turns it into a status code and a sentence, so the rules are
       testable without a request and the wording lives with the endpoint. */
    public enum Problem
    {
        None,
        NotLeader,
        NotFriends,
        AlreadyIn,
        PartyFull,
        TooManyInvites,
        NoInvite,
        NotInParty,
    }

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

    /* Asking somebody to join, which is also how most parties come into being:
       the first invite a player sends makes them a party of one and then asks.

       Only friends can be invited. Without that rule any signed-in account can
       throw invites at every player id it can guess, and there is no useful way
       to stop it afterwards; friendship already exists, is already checked, and
       already has an index. */
    public async Task<Problem> InviteAsync(int leaderId, int playerId, CancellationToken ct = default)
    {
        var party = await EnsureForAsync(leaderId, ct);
        if (!party.IsLeader(leaderId)) return Problem.NotLeader;
        if (party.Holds(playerId)) return Problem.AlreadyIn;

        /* A full party is refused here as a courtesy; the check that counts is
           the one when the invite is accepted, because the party can fill in
           between the asking and the answering. */
        if (party.Size >= MaxMembers) return Problem.PartyFull;

        if (!await AreFriendsAsync(leaderId, playerId, ct)) return Problem.NotFriends;

        var now = DateTimeOffset.UtcNow;

        /* One query answers both questions: how many invites are already out,
           and whether this player has one. Expired rows count for the second,
           because the unique index does not care that they are stale, so asking
           somebody again refreshes their row rather than colliding with it. */
        var invites = await db.PartyInvites
            .Where(i => i.PartyId == party.Id)
            .Select(i => new { i.Id, i.ToPlayerId, i.ExpiresAt })
            .AsNoTracking()
            .ToListAsync(ct);

        var already = invites.FirstOrDefault(i => i.ToPlayerId == playerId);
        if (already is null && invites.Count(i => i.ExpiresAt > now) >= MaxInvites)
            return Problem.TooManyInvites;

        if (already is not null)
        {
            await db.PartyInvites
                .Where(i => i.Id == already.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(i => i.FromPlayerId, leaderId)
                    .SetProperty(i => i.CreatedAt, now)
                    .SetProperty(i => i.ExpiresAt, now + InviteLifetime), ct);
            return Problem.None;
        }

        db.PartyInvites.Add(new PartyInvite
        {
            PartyId = party.Id,
            ToPlayerId = playerId,
            FromPlayerId = leaderId,
            CreatedAt = now,
            ExpiresAt = now + InviteLifetime,
        });
        await db.SaveChangesAsync(ct);
        return Problem.None;
    }

    /* Taking an invite up.

       Everything that follows has to happen together or not at all: leaving the
       party they are in, both parties losing their place in the queue, and the
       membership row that can only exist once. A transaction is what makes the
       failure case leave them exactly where they started. */
    public async Task<Problem> AcceptInviteAsync(int playerId, int partyId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var invite = await db.PartyInvites
            .FirstOrDefaultAsync(i => i.PartyId == partyId && i.ToPlayerId == playerId && i.ExpiresAt > now, ct);
        if (invite is null) return Problem.NoInvite;

        var mine = await OfAsync(playerId, ct);

        // Already there: the invite is stale, and deleting it is the whole answer.
        if (mine is not null && mine.Id == partyId)
        {
            db.PartyInvites.Remove(invite);
            await db.SaveChangesAsync(ct);
            return Problem.None;
        }

        /* Not serialised against another accept landing at the same moment, and
           deliberately so: locking the party to answer it would be a lock held
           across a friend's click. Two accepts racing can put one member over
           the cap, and the queue refuses to take an oversized party, so the
           worst case is a party that has to drop somebody before it can play. */
        var size = await db.PartyMembers.CountAsync(m => m.PartyId == partyId, ct);
        if (size >= MaxMembers) return Problem.PartyFull;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (mine is not null) await LeaveAsync(mine, playerId, ct);

            /* The party they are joining changes size and rating, so it stops
               waiting: a queue entry that no longer describes its party is a
               match formed on stale information. */
            await DequeueAsync(partyId, ct);

            db.PartyMembers.Add(new PartyMember { PartyId = partyId, PlayerId = playerId, JoinedAt = now });
            db.PartyInvites.Remove(invite);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Problem.None;
        }
        catch (DbUpdateException)
        {
            /* Two invites accepted in the same instant. The unique index on
               PartyMember.PlayerId refused the second insert and the whole
               transaction goes with it, so they are still in the party they were
               in a moment ago rather than in two at once. */
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Problem.AlreadyIn;
        }
    }

    /* Declining an invite sent to you, or - as a leader - cancelling one your
       party sent. The party is named either way, and the invitee only ever has
       one invite from it, so nothing else is needed to find the row. */
    public async Task<Problem> RemoveInviteAsync(int actorId, int partyId, int? toPlayerId,
                                                 CancellationToken ct = default)
    {
        var target = toPlayerId ?? actorId;

        if (target != actorId)
        {
            var party = await OfAsync(actorId, ct);
            if (party is null || party.Id != partyId) return Problem.NotInParty;
            if (!party.IsLeader(actorId)) return Problem.NotLeader;
        }

        var gone = await db.PartyInvites
            .Where(i => i.PartyId == partyId && i.ToPlayerId == target)
            .ExecuteDeleteAsync(ct);

        return gone == 0 ? Problem.NoInvite : Problem.None;
    }

    /// Leaving, if it is you; kicking, if it is the leader removing somebody else.
    public async Task<Problem> RemoveMemberAsync(int actorId, int playerId, CancellationToken ct = default)
    {
        var party = await OfAsync(actorId, ct);
        if (party is null) return Problem.NotInParty;
        if (playerId != actorId && !party.IsLeader(actorId)) return Problem.NotLeader;
        if (!party.Holds(playerId)) return Problem.NotInParty;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LeaveAsync(party, playerId, ct);
        await transaction.CommitAsync(ct);
        return Problem.None;
    }

    /// Expired invites, swept by the janitor: one indexed delete, usually of nothing.
    public Task<int> SweepExpiredInvitesAsync(CancellationToken ct = default) =>
        db.PartyInvites.Where(i => i.ExpiresAt <= DateTimeOffset.UtcNow).ExecuteDeleteAsync(ct);

    /* Taking somebody out of a party and tidying up after them.

       Three things follow from one member leaving: the party stops waiting,
       because it is not the party that joined the queue any more; an empty party
       is deleted, taking its invites with it; and a leader who leaves hands over
       to whoever has been in the party longest, since somebody has to be able to
       queue it. Expects to be inside a transaction. */
    private async Task LeaveAsync(Snapshot party, int playerId, CancellationToken ct)
    {
        await DequeueAsync(party.Id, ct);
        await db.PartyMembers.Where(m => m.PlayerId == playerId).ExecuteDeleteAsync(ct);

        var remaining = party.MemberIds.Where(id => id != playerId).ToList();
        if (remaining.Count == 0)
        {
            // the invites and the queue entry go with it, by cascade
            await db.Parties.Where(p => p.Id == party.Id).ExecuteDeleteAsync(ct);
            return;
        }

        if (party.IsLeader(playerId))
            await db.Parties.Where(p => p.Id == party.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.LeaderId, remaining[0]), ct);
    }

    private Task DequeueAsync(int partyId, CancellationToken ct) =>
        db.Queue.Where(q => q.PartyId == partyId).ExecuteDeleteAsync(ct);

    private Task<bool> AreFriendsAsync(int a, int b, CancellationToken ct) =>
        db.Friendships.AnyAsync(f => f.Status == FriendshipStatus.Accepted
            && ((f.RequesterId == a && f.AddresseeId == b)
             || (f.RequesterId == b && f.AddresseeId == a)), ct);
}
