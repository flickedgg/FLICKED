namespace Flicked.Api.Models;

/* One party waiting for a match.

   In Postgres rather than in memory, for the same reason the pool is: it can be
   looked at when something goes wrong, it survives a restart, and it can be
   tested. A queue of a few hundred rows costs the database nothing.

   The row points at a party rather than a player, and somebody playing alone is
   a party of one. That is what keeps there from being two shapes of queue entry:
   the matchmaker never asks whether this is a party, only how many seats it
   takes and what it is rated. */
public class QueueEntry
{
    public int Id { get; set; }

    public int PartyId { get; set; }
    public Party? Party { get; set; }

    /// Which kind of match they are waiting for: 5v5 or 2v2.
    public ServerType Mode { get; set; }

    /* When they joined. Two uses: the rating window widens the longer somebody
       waits, and the oldest waiting party is matched first.

       One time for the whole party, rather than one per member, so there is one
       widening window instead of five that would have to be reconciled. */
    public DateTimeOffset JoinedAt { get; set; }

    /* The rating band this party will accept, widening with time.

       Without this a 2600-rated player never matches at all: there is nobody
       within 100 points of them on a small instance. Starting narrow and opening
       up trades fairness for a wait, which is the trade every matchmaker makes.

       Calculated rather than stored, so it cannot go stale. The matchmaker reads
       the queue as scalars rather than entities, so the formula is static and
       this instance method is the same answer for callers holding a row. */
    public int ToleranceAt(DateTimeOffset now) => Tolerance(JoinedAt, now);

    public static int Tolerance(DateTimeOffset joinedAt, DateTimeOffset now)
    {
        var waited = now - joinedAt;
        return 100 + (int)(waited.TotalSeconds / 10) * 25;
    }
}
