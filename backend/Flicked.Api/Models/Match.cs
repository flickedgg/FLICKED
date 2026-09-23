namespace Flicked.Api.Models;

/* Where a match is in its life.

   Until now a Match row only ever described a finished game, because the seeded
   ones were all in the past. A match that is being played has no score yet, so
   it needs somewhere to say so. */
public enum MatchStatus
{
    Accepting,   // ten players found; waiting for everyone to accept
    Voting,      // everyone accepted; picking the map
    Pending,     // map chosen and a server claimed; waiting for players to connect
    Live,        // MatchZy said going_live
    Finished,    // a result came back
    Cancelled,   // somebody declined, nobody connected, or no server was free
}

public class Match
{
    public int Id { get; set; }
    public required string Map { get; set; }
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public DateTimeOffset PlayedAt { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Pending;

    /* Lets the CS2 server fetch this match's config, and nothing else.

       The server's own token is stored hashed, so it cannot be put into the
       matchzy_loadmatch_url command we send over RCON. This one is generated when
       the match starts, handed to the server in that command, and kept here as a
       hash like every other secret. It is worth exactly one match. */
    public string? ConfigTokenHash { get; set; }

    public List<MatchPlayer> Players { get; set; } = [];
}
