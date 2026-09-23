namespace Flicked.Api.Models;

/* One player waiting for a match.

   In Postgres rather than in memory, for the same reason the pool is: it can be
   looked at when something goes wrong, it survives a restart, and it can be
   tested. A queue of a few hundred rows costs the database nothing.

   Parties are not here yet. When they are, this gains a PartyId and the
   matchmaker takes a party as one indivisible block of seats. */
public class QueueEntry
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    /// Which kind of match they are waiting for: 5v5 or 2v2.
    public ServerType Mode { get; set; }

    /* When they joined. Two uses: the rating window widens the longer somebody
       waits, and the oldest waiting player is matched first. */
    public DateTimeOffset JoinedAt { get; set; }

    /* The rating band this player will accept, widening with time.

       Without this a 2600-rated player never matches at all: there is nobody
       within 100 points of them on a small instance. Starting narrow and opening
       up trades fairness for a wait, which is the trade every matchmaker makes.

       Calculated rather than stored, so it cannot go stale. */
    public int ToleranceAt(DateTimeOffset now)
    {
        var waited = now - JoinedAt;
        return 100 + (int)(waited.TotalSeconds / 10) * 25;
    }
}
