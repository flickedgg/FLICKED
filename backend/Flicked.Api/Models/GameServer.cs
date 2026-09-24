namespace Flicked.Api.Models;

public enum ServerStatus
{
    Offline,    // not heard from recently: never handed out
    Idle,       // online and free
    Reserved,   // claimed for a match, nobody connected yet
    Hosting,    // a match is being played on it
}

public enum ServerType
{
    Competitive,   // 5v5
    Wingman,       // 2v2
}

/* A CS2 server somebody runs, registered by an admin so FLICKED can hand it out.

   FLICKED does not create or host these: it keeps a pool of servers that already
   exist, claims a free one when a match is ready, and releases it afterwards
   (see docs/ROADMAP.md, "Server owners bring the servers"). */
public class GameServer
{
    public int Id { get; set; }

    /// Shown in the dashboard and logs: "fra-01" beats an IP address.
    public string Name { get; set; } = "";

    /// Free text, matched loosely when picking a nearby server later: "eu-west".
    public string Region { get; set; } = "";

    public ServerType Type { get; set; }

    public string Host { get; set; } = "";
    public int Port { get; set; } = 27015;

    public string? GamePassword { get; set; }
    public string RconPasswordEncrypted { get; set; } = "";

    public ServerStatus Status { get; set; } = ServerStatus.Offline;

    public int? CurrentMatchId { get; set; }
    public Match? CurrentMatch { get; set; }

    /* The server's own credential, for the calls it makes to us (heartbeats, match
       results). Hashed, like a session token: we only ever check it. */
    public string TokenHash { get; set; } = "";

    /// Last heartbeat. Silence for long enough means offline, so it stops being handed out.
    public DateTimeOffset? LastSeenAt { get; set; }

    /* When a claim stops being believed.

       Without this, a server that crashes mid-match stays marked Hosting forever
       and quietly leaves the pool: nothing would ever report that match finished.
       A background job returns anything past its lease. */
    public DateTimeOffset? LeaseUntil { get; set; }

    /// An admin taking a server out of rotation without deleting its history.
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    /// Free to claim: enabled, idle, and heard from recently.
    public bool IsAvailable(DateTimeOffset now, TimeSpan silenceAllowed) =>
        IsEnabled
        && Status == ServerStatus.Idle
        && LastSeenAt is not null
        && now - LastSeenAt.Value < silenceAllowed;
}
