namespace Flicked.Api.Models;

/* A group of players who queue as one.

   Every player who queues is in a party, and somebody playing alone is a party
   of one. That is the whole point of the table: the matchmaker never asks
   whether a queue entry is a party or a person, only how many seats it takes,
   so there is no solo path running alongside a party path for the two to
   disagree about.

   Only the leader queues, invites and kicks. Not for hierarchy: two members
   queueing for different modes in the same instant is a question with no good
   answer, so only one of them is allowed to ask it. */
public class Party
{
    public int Id { get; set; }

    public int LeaderId { get; set; }
    public Player? Leader { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<PartyMember> Members { get; set; } = [];
}

/* One player's membership of one party.

   PlayerId is unique across the whole table, and that index is load-bearing
   rather than decorative. Two invites accepted in the same instant, or one
   accepted twice by a client that retried, cannot put a player in two parties:
   the second insert violates the index and its transaction fails. No lock, no
   read-then-write race, and it holds even if a second API instance ever runs. */
public class PartyMember
{
    public int Id { get; set; }

    public int PartyId { get; set; }
    public Party? Party { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// Display order, and who becomes leader when the leader leaves.
    public DateTimeOffset JoinedAt { get; set; }
}

/* An offer to join a party, waiting for an answer.

   It expires, because a stale invite is worse than no invite: it offers a party
   that may have disbanded, filled up, or already be halfway through a match. The
   janitor sweeps the expired ones, and the social poll ignores anything past its
   expiry meanwhile, so an invite that outlives its sweep is never shown. */
public class PartyInvite
{
    public int Id { get; set; }

    public int PartyId { get; set; }
    public Party? Party { get; set; }

    public int ToPlayerId { get; set; }
    public Player? ToPlayer { get; set; }

    /// Who asked, so the launcher can name a person rather than a party number.
    public int FromPlayerId { get; set; }
    public Player? FromPlayer { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
