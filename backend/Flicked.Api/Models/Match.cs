namespace Flicked.Api.Models;

/* Where a match is in its life.

   Until now a Match row only ever described a finished game, because the seeded
   ones were all in the past. A match that is being played has no score yet, so
   it needs somewhere to say so. */
public enum MatchStatus
{
    Pending,     // created, server claimed, waiting for players to connect
    Live,        // MatchZy said going_live
    Finished,    // a result came back
    Cancelled,   // never started: nobody connected, or no server was free
}

public class Match
{
    public int Id { get; set; }
    public required string Map { get; set; }
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public DateTimeOffset PlayedAt { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Pending;

    public List<MatchPlayer> Players { get; set; } = [];
}
