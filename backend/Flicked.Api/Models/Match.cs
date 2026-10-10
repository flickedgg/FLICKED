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

    /* When the server confirmed it is on this match's map.

       Claiming a server and telling it to load a match are instant; the server
       taking the config, reloading its plugin and changing level are not. Until
       this is set, sending players the address means they join whatever map was
       loaded before, which is exactly the bug it exists to prevent. */
    public DateTimeOffset? ServerReadyAt { get; set; }

    /* Which server this match was given to, from the moment it was claimed.

       Not the same fact as GameServer.CurrentMatchId, and both are needed. That
       one is live state and is cleared the moment the server is released; this
       one is history and stays.

       The difference is what authorises a result. A server reporting a score has
       to be the server the match was sent to, and asking that of live state gets
       the wrong answer twice over: the real host that had its lease swept
       mid-match no longer holds it, and any other registered server could claim
       a match it was never given. */
    public int? ServerId { get; set; }

    public List<MatchPlayer> Players { get; set; } = [];
}
