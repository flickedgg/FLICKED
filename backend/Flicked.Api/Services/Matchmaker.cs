using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* Turning a queue into matches.

   Runs on a timer (see MatchmakerJanitor). Each pass looks at everyone waiting
   for a mode, oldest first, and tries to build one full match around them. */
public class Matchmaker(FlickedDbContext db, ILogger<Matchmaker> log)
{
    /// Everyone must answer within this, or the match is cancelled.
    public static readonly TimeSpan AcceptWindow = TimeSpan.FromSeconds(20);

    /// How long the map vote lasts once everyone has accepted.
    public static readonly TimeSpan VoteWindow = TimeSpan.FromSeconds(15);

    /// Maps that can be voted for. The launcher shows the same seven.
    public static readonly string[] MapPool =
        ["de_mirage", "de_inferno", "de_nuke", "de_ancient", "de_anubis", "de_dust2", "de_train"];

    /* How many players a match needs.

       Ten and four are the real answers. Both can be lowered to try the whole
       chain with fewer people than a real match needs, which is otherwise
       impossible: nothing downstream cares how many there are, because the
       teams are drafted from whoever is in the match and the server is told
       players_per_team from that same roster.

           Matchmaking:CompetitivePlayers=2

       Set once at startup rather than read per pass, so the queue, the count the
       launcher shows, and the draft cannot disagree halfway through a match. */
    public static int CompetitivePlayers { get; set; } = 10;
    public static int WingmanPlayers { get; set; } = 4;

    public static int PlayersFor(ServerType mode) =>
        mode == ServerType.Wingman ? WingmanPlayers : CompetitivePlayers;

    /* One pass: build as many matches as the queue currently allows.

       Anchored on the longest-waiting player rather than scanning every possible
       combination. Combinations grow factorially and the difference is invisible
       at this scale, while "whoever waited longest goes first" is a rule players
       can understand and complain about fairly. */
    public async Task<List<Match>> FormMatchesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var made = new List<Match>();

        foreach (var mode in new[] { ServerType.Competitive, ServerType.Wingman })
        {
            var needed = PlayersFor(mode);

            var waiting = await db.Queue
                .Include(q => q.Player)
                .Where(q => q.Mode == mode)
                .OrderBy(q => q.JoinedAt)
                .ToListAsync(ct);

            while (waiting.Count >= needed)
            {
                var group = Gather(waiting, needed, now);
                if (group is null) break;   // nobody compatible enough yet; wait for the window to widen

                made.Add(await CreateMatchAsync(mode, group, ct));
                foreach (var entry in group) waiting.Remove(entry);
            }
        }

        return made;
    }

    /* Finds `needed` players who all accept each other's rating range.

       The rule is mutual: two players match only when the gap fits inside *both*
       their windows. Since a window widens with waiting time, somebody who just
       joined will not be dragged into a lopsided match, while somebody who has
       waited two minutes becomes progressively easier to please. */
    private static List<QueueEntry>? Gather(List<QueueEntry> waiting, int needed, DateTimeOffset now)
    {
        foreach (var anchor in waiting)
        {
            var group = new List<QueueEntry> { anchor };

            foreach (var other in waiting)
            {
                if (other.Id == anchor.Id) continue;

                var gap = Math.Abs((other.Player?.Rating ?? 0) - (anchor.Player?.Rating ?? 0));
                var allowed = Math.Min(anchor.ToleranceAt(now), other.ToleranceAt(now));
                if (gap <= allowed) group.Add(other);

                if (group.Count == needed) return group;
            }
        }

        return null;
    }

    private async Task<Match> CreateMatchAsync(ServerType mode, List<QueueEntry> group, CancellationToken ct)
    {
        var match = new Match
        {
            Map = "",                       // decided by the vote
            Status = MatchStatus.Accepting,
            PlayedAt = DateTimeOffset.UtcNow,
            Players = Balance(group),
        };

        db.Matches.Add(match);
        db.Queue.RemoveRange(group);        // they are in a match now, not waiting
        await db.SaveChangesAsync(ct);

        log.LogInformation("Match {MatchId} formed for {Count} players ({Mode})",
            match.Id, group.Count, mode);
        return match;
    }

    /* Two sides of similar strength, by snake draft: the best player goes to A,
       the next two to B, the next two to A, and so on. It is not optimal, but it
       is simple, predictable, and close enough that nobody can point at the
       teams and say the system did something strange. */
    private static List<MatchPlayer> Balance(List<QueueEntry> group)
    {
        var ranked = group.OrderByDescending(q => q.Player?.Rating ?? 0).ToList();
        var players = new List<MatchPlayer>();

        for (var i = 0; i < ranked.Count; i++)
        {
            // 0,3,4,7,8... on one side; 1,2,5,6... on the other
            var team = (i % 4 is 0 or 3) ? 0 : 1;
            players.Add(new MatchPlayer { PlayerId = ranked[i].PlayerId, Team = team });
        }

        return players;
    }

    /* The phases that end on a clock rather than an answer.

       Both are failures of attention, and both put the innocent back where they
       were: a match nobody finished accepting is cancelled and the players who
       did accept go back to the front of the queue, because they did nothing
       wrong and should not lose their place. */
    public async Task<(int cancelled, int started)> AdvancePhasesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cancelled = 0;
        var started = 0;

        var pending = await db.Matches
            .Include(m => m.Players)
            .Where(m => m.Status == MatchStatus.Accepting || m.Status == MatchStatus.Voting)
            .ToListAsync(ct);

        foreach (var match in pending)
        {
            var age = now - match.PlayedAt;

            if (match.Status == MatchStatus.Accepting)
            {
                if (match.Players.All(p => p.AcceptedAt is not null))
                {
                    match.Status = MatchStatus.Voting;
                    match.PlayedAt = now;                 // the vote clock starts here
                    log.LogInformation("Match {MatchId}: everyone accepted, voting", match.Id);
                }
                else if (age > AcceptWindow)
                {
                    match.Status = MatchStatus.Cancelled;
                    cancelled++;
                    await RequeueAsync(match, onlyThoseWhoAccepted: true, ct);
                    log.LogInformation("Match {MatchId} cancelled: not everyone accepted", match.Id);
                }
            }
            else if (age > VoteWindow || match.Players.All(p => p.MapVote is not null))
            {
                match.Map = WinningMap(match);
                match.Status = MatchStatus.Pending;       // ready for a server
                started++;
                log.LogInformation("Match {MatchId}: {Map} won the vote", match.Id, match.Map);
            }
        }

        if (pending.Count > 0) await db.SaveChangesAsync(ct);
        return (cancelled, started);
    }

    /// Most votes wins; ties, and a vote nobody cast, are settled at random.
    private static string WinningMap(Match match)
    {
        var votes = match.Players
            .Where(p => p.MapVote is not null && MapPool.Contains(p.MapVote))
            .GroupBy(p => p.MapVote!)
            .ToList();

        if (votes.Count == 0) return MapPool[Random.Shared.Next(MapPool.Length)];

        var most = votes.Max(v => v.Count());
        var leaders = votes.Where(v => v.Count() == most).Select(v => v.Key).ToList();
        return leaders[Random.Shared.Next(leaders.Count)];
    }

    private async Task RequeueAsync(Match match, bool onlyThoseWhoAccepted, CancellationToken ct)
    {
        var mode = ServerType.Competitive;   // one mode for now; the match knows no better
        var joined = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1);   // ahead of newcomers

        foreach (var player in match.Players)
        {
            if (onlyThoseWhoAccepted && player.AcceptedAt is null) continue;
            if (await db.Queue.AnyAsync(q => q.PlayerId == player.PlayerId, ct)) continue;

            db.Queue.Add(new QueueEntry { PlayerId = player.PlayerId, Mode = mode, JoinedAt = joined });
        }
    }
}
