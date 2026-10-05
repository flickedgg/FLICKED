using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* Turning a queue into matches.

   Runs on a timer (see MatchmakerJanitor). Each pass looks at every party
   waiting for a mode, oldest first, and tries to build one full match around
   them. A player queueing alone is a party of one, so there is no second path
   through any of this for them. */
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

    /* The smallest match worth starting when nobody else turns up.

       A full match is always preferred and always tried first; these only come
       into play once the longest-waiting party has waited ShortHandedAfter.

           Matchmaking:MinCompetitivePlayers=6

       Both default to the full size, which switches the whole thing off, so an
       instance that sets nothing behaves exactly as it did before. For a
       community that cannot reliably find ten people, waiting forever for a
       tenth is worse than playing 3v3. */
    public static int MinCompetitivePlayers { get; set; } = 10;
    public static int MinWingmanPlayers { get; set; } = 4;

    /* How long the longest-waiting party must already have waited before a short
       match may be formed around it.

       The point of the delay is that somebody who joins a second before the
       tenth player should still get the real thing. Twenty seconds is long
       enough for that and short enough that a quiet night is still playable. */
    public static TimeSpan ShortHandedAfter { get; set; } = TimeSpan.FromSeconds(20);

    /* The smallest match this mode will actually form.

       Rounded up to an even number, and never more than a full match. Even,
       because everything downstream assumes two teams of the same size: Balance
       throws rather than return five against four, and the config handed to the
       CS2 server carries a single players_per_team. 4v3 is not a match anybody
       asked for.

       Rounded up rather than down so a minimum of 5 means six players, and
       nothing ever starts smaller than the number that was written down. */
    public static int MinPlayersFor(ServerType mode)
    {
        var wanted = mode == ServerType.Wingman ? MinWingmanPlayers : MinCompetitivePlayers;
        return Math.Max(2, Math.Min(PlayersFor(mode), wanted + Math.Abs(wanted % 2)));
    }

    /* One team's worth of seats, which is also the most people a party may hold
       for that mode: a party that cannot fit on one team can never be given a
       match, because the one thing a party guarantees is that it is not split.

       Deliberately the *full* size, not the short-handed one. A party of five
       stays legal on an instance that will settle for six, it simply cannot be
       given one of those: six seats split 3-3, and this party does not split. */
    public static int TeamSizeFor(ServerType mode) => Math.Max(1, PlayersFor(mode) / 2);

    /* One pass: build as many matches as the queue currently allows.

       Anchored on the longest-waiting party rather than scanning every possible
       combination. Combinations grow factorially and the difference is invisible
       at this scale, while "whoever waited longest goes first" is a rule players
       can understand and complain about fairly. */
    public async Task<List<Match>> FormMatchesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var made = new List<Match>();

        foreach (var mode in new[] { ServerType.Competitive, ServerType.Wingman })
        {
            var full = PlayersFor(mode);
            var waiting = await WaitingAsync(mode, ct);

            /* Full matches first, and all of them, before any short one is
               considered. The order matters: done the other way, an early anchor
               could take six people into a 3v3 while the players who would have
               completed a real match were sitting two rows further down. */
            made.AddRange(await FillAsync(mode, waiting, full, null, now, ct));

            /* Then the short ones, largest first, so eight beats six and six
               beats four. Each is offered only to parties that have already
               waited, which is what keeps this from quietly replacing the full
               match somebody was four seconds away from getting. */
            for (var size = full - 2; size >= MinPlayersFor(mode); size -= 2)
                made.AddRange(await FillAsync(mode, waiting, size, ShortHandedAfter, now, ct));
        }

        return made;
    }

    /* As many matches of exactly this size as the queue currently allows.

       `after` is how long the match's longest-waiting party must already have
       waited. Null is "no such requirement", which is what a full match uses. */
    private async Task<List<Match>> FillAsync(
        ServerType mode, List<WaitingParty> waiting, int size,
        TimeSpan? after, DateTimeOffset now, CancellationToken ct)
    {
        var made = new List<Match>();

        while (waiting.Sum(p => p.Size) >= size)
        {
            var group = Gather(waiting, size, now, after);
            if (group is null) break;   // nobody compatible enough yet; wait for the window to widen

            made.Add(await CreateMatchAsync(mode, group, ct));
            foreach (var party in group) waiting.Remove(party);
        }

        return made;
    }

    /* The queue for one mode, as the matchmaker needs to see it.

       One query, projected to the four things that matter (when it joined, how
       many seats, who is in it, what they are rated) rather than loading parties
       and members as entities. This runs every two seconds forever, so it fetches
       what it reads and nothing else, and it reads every member in the same round
       trip rather than one query per waiting party. */
    private async Task<List<WaitingParty>> WaitingAsync(ServerType mode, CancellationToken ct)
    {
        var rows = await db.Queue
            .Where(q => q.Mode == mode)
            .OrderBy(q => q.JoinedAt)
            .Select(q => new
            {
                q.Id,
                q.PartyId,
                q.JoinedAt,
                Members = q.Party!.Members
                    .Select(m => new { m.PlayerId, m.Player!.Rating })
                    .ToList(),
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows
            .Select(r => new WaitingParty(r.Id, r.PartyId, r.JoinedAt,
                r.Members.Select(m => new Seat(m.PlayerId, m.Rating)).ToList()))
            .ToList();
    }

    /* Enough parties to fill a match, all of whom accept each other's rating range.

       The rule is mutual: two parties match only when the gap fits inside *both*
       their windows. Since a window widens with waiting time, somebody who just
       joined will not be dragged into a lopsided match, while somebody who has
       waited two minutes becomes progressively easier to please.

       Admitting a party admits all of it, so the group is assembled by seats
       rather than by heads, and a full group is only usable if those seats can
       also be dealt into two whole teams.

       `after`, when given, is how long the anchor must already have waited. The
       anchor is the longest-waiting party in whatever group comes back, so this
       is the one place a short-handed match can be held back without having to
       check every member: a party that joined a moment ago can still be swept
       into one, which costs it nothing, but cannot cause one. */
    private static List<WaitingParty>? Gather(List<WaitingParty> waiting, int needed,
                                              DateTimeOffset now, TimeSpan? after = null)
    {
        foreach (var anchor in waiting)
        {
            if (anchor.Size > needed) continue;
            if (after is not null && now - anchor.JoinedAt < after) continue;

            var group = new List<WaitingParty> { anchor };
            var seats = anchor.Size;

            foreach (var other in waiting)
            {
                if (other.EntryId == anchor.EntryId) continue;
                if (seats + other.Size > needed) continue;      // would overflow the match

                var gap = Math.Abs(other.Rating - anchor.Rating);
                var allowed = Math.Min(anchor.ToleranceAt(now), other.ToleranceAt(now));
                if (gap > allowed) continue;

                group.Add(other);
                seats += other.Size;

                if (seats < needed) continue;
                if (Splittable(group, needed / 2)) return group;

                /* Full, but only by cutting a party in half. Parties of 4, 3 and
                   3 sum to ten and cannot make two fives: 4+3 is seven and the
                   remaining 3 is not five. The last one to arrive steps back out
                   and the search carries on, because 4+3+2+1 does work and
                   giving up on the anchor here would miss it. */
                group.RemoveAt(group.Count - 1);
                seats -= other.Size;
            }
        }

        return null;
    }

    /* Can these parties be dealt into two teams without splitting one?

       Not implied by the seats adding up, and forgetting it is the bug this
       exists to prevent. It is a subset sum over at most ten sizes: a table of
       which seat counts one side can be made to hold, which is a few hundred
       operations a few times a minute and not a cost worth thinking about. */
    private static bool Splittable(List<WaitingParty> group, int half)
    {
        var reachable = new bool[half + 1];
        reachable[0] = true;

        foreach (var party in group)
            for (var seats = half; seats >= party.Size; seats--)
                if (reachable[seats - party.Size]) reachable[seats] = true;

        return reachable[half];
    }

    private async Task<Match> CreateMatchAsync(ServerType mode, List<WaitingParty> group, CancellationToken ct)
    {
        /* Half of what this group actually holds, not half of a full match: a
           short-handed one has fewer. Gather has already proved these parties
           deal into two teams of exactly this, so Balance cannot come back
           empty. */
        var teamSize = group.Sum(p => p.Size) / 2;

        var match = new Match
        {
            Map = "",                       // decided by the vote
            Status = MatchStatus.Accepting,
            PlayedAt = DateTimeOffset.UtcNow,
            Players = Balance(group, teamSize),
        };

        db.Matches.Add(match);

        /* They are in a match now, not waiting. The rows were read as scalars, so
           they are removed by id rather than by handing EF entities it already
           has: same one statement, without a second read to get them. */
        foreach (var party in group)
            db.Queue.Remove(new QueueEntry { Id = party.EntryId, PartyId = party.PartyId });

        await db.SaveChangesAsync(ct);

        log.LogInformation("Match {MatchId} formed for {Count} players in {Parties} parties ({Mode})",
            match.Id, match.Players.Count, group.Count, mode);
        return match;
    }

    /* Two sides of similar strength, chosen rather than drafted.

       The snake draft this replaces assigned by player, which cannot keep a
       party together. Instead every way of dealing these parties out is
       considered, and the one that fills both teams with the smallest difference
       in total rating wins. A party is one item in that search, so it is never
       split, and a side's strength is the sum of its members rather than its
       best player.

       With at most ten parties there are at most 2^10 arrangements, nearly all
       of which are the wrong size and cost one addition to rule out. That is
       cheaper than the database round trip that preceded it, and unlike the
       draft it is exact: no arrangement of these parties balances better.

       Gather only ever returns a group that can be split this way, so the search
       cannot come back empty; if it ever does, something upstream is wrong and
       saying so beats forming a match with five against four. */
    private static List<MatchPlayer> Balance(List<WaitingParty> group, int teamSize)
    {
        var total = group.Sum(p => p.Strength);
        var best = -1;
        var closest = int.MaxValue;

        for (var arrangement = 0; arrangement < 1 << group.Count; arrangement++)
        {
            var seats = 0;
            var strength = 0;

            for (var i = 0; i < group.Count; i++)
            {
                if ((arrangement & (1 << i)) == 0) continue;
                seats += group[i].Size;
                strength += group[i].Strength;
            }

            if (seats != teamSize) continue;

            var difference = Math.Abs(strength - (total - strength));
            if (difference >= closest) continue;

            closest = difference;
            best = arrangement;
        }

        if (best < 0)
            throw new InvalidOperationException(
                $"These {group.Count} parties cannot make two teams of {teamSize}.");

        return group
            .SelectMany((party, i) => party.Members.Select(member => new MatchPlayer
            {
                PlayerId = member.PlayerId,
                Team = (best & (1 << i)) != 0 ? 0 : 1,
            }))
            .ToList();
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
                    await RequeueAsync(match, ct);
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

    /* Back to the queue, by party, for the parties who all accepted.

       A party goes back only if every one of its members answered. If one did
       not, the party is dropped from the queue entirely rather than requeued
       without them: they are a unit, their teammate answered for a match they
       were going to play together, and putting the rest back in alone is the
       opposite of what they queued for.

       Four indexed queries however many players were in the match, rather than
       one per player: which parties they are in, who else is in those, and which
       are somehow already waiting. */
    private async Task RequeueAsync(Match match, CancellationToken ct)
    {
        var mode = ServerType.Competitive;   // one mode for now; the match knows no better
        var joined = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1);   // ahead of newcomers

        var accepted = match.Players
            .Where(p => p.AcceptedAt is not null)
            .Select(p => p.PlayerId)
            .ToList();
        if (accepted.Count == 0) return;

        /* Their party as it stands now rather than as it was when the match
           formed: somebody who left in between is not a member this party is
           waiting to hear from. */
        var parties = await db.PartyMembers
            .Where(m => accepted.Contains(m.PlayerId))
            .Select(m => m.PartyId)
            .Distinct()
            .ToListAsync(ct);

        var members = await db.PartyMembers
            .Where(m => parties.Contains(m.PartyId))
            .Select(m => new { m.PartyId, m.PlayerId })
            .ToListAsync(ct);

        var willing = accepted.ToHashSet();
        var whole = members
            .GroupBy(m => m.PartyId)
            .Where(party => party.All(m => willing.Contains(m.PlayerId)))
            .Select(party => party.Key)
            .ToList();

        var queued = await db.Queue
            .Where(q => whole.Contains(q.PartyId))
            .Select(q => q.PartyId)
            .ToListAsync(ct);

        foreach (var partyId in whole.Except(queued))
            db.Queue.Add(new QueueEntry { PartyId = partyId, Mode = mode, JoinedAt = joined });
    }

    /// One member of a waiting party: who they are, and what they are rated.
    private sealed record Seat(int PlayerId, int Rating);

    /* A queue entry as the matchmaker sees it.

       Two ratings, used for different things, and the difference matters.
       Rating - the highest in the party - decides who this party is matched
       with, because a 2600 queueing with a 1200 friend is a 2600 in the match
       and an average would hand them opponents their friend cannot play
       against. Strength - the sum - decides which team they go on, because a
       side's strength is the whole side and not its best player.

       Both are worked out once, when the row is read, rather than on every
       comparison in a loop that is quadratic in the length of the queue. */
    private sealed record WaitingParty(int EntryId, int PartyId, DateTimeOffset JoinedAt, List<Seat> Members)
    {
        public int Size { get; } = Members.Count;
        public int Rating { get; } = Members.Count > 0 ? Members.Max(m => m.Rating) : 0;
        public int Strength { get; } = Members.Sum(m => m.Rating);

        public int ToleranceAt(DateTimeOffset now) => QueueEntry.Tolerance(JoinedAt, now);
    }
}
