using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Controllers;

/* Queueing for a match.

     POST   /api/queue            join, for a mode
     DELETE /api/queue            leave
     GET    /api/queue            where you are: waiting, accepting, voting, playing
     POST   /api/queue/accept     accept the match you were offered
     POST   /api/queue/decline    decline it, which cancels it for everyone
     POST   /api/queue/vote       vote for a map

   Everything works on the signed-in player. There is no "on behalf of" anywhere:
   the launcher cannot queue somebody else, accept for them, or vote for them.

   Joining and leaving are the exception, and only in one direction: the queue
   holds parties, so the leader's join puts their whole party in it. Accepting
   and voting stay personal, because they are answers about you. */
[ApiController]
[Route("api/queue")]
public class QueueController(FlickedDbContext db, CurrentPlayer current, Parties parties,
                             ILogger<QueueController> log)
    : ControllerBase
{
    public record JoinRequest(string? Mode);
    public record VoteRequest(string? Map);

    /* What the launcher draws. One shape for every phase, so the client has a
       single thing to render rather than four endpoints to stitch together. */
    public record QueueState(
        string Phase,                    // idle | searching | found | vote | connecting | live
        string? Mode,
        DateTimeOffset? Since,           // when this phase began: the launcher counts up from it
        int? MatchId,
        int Accepted,                    // how many have accepted so far
        int Needed,
        bool YouAccepted,
        string? YourVote,
        string? Map,                     // once the vote is settled
        string? Connect,                 // host:port, once there is a server
        string? ConnectPassword,         // the server's game password, if it has one
        Dictionary<string, int> Votes);  // map code -> votes so far, during the vote

    [HttpGet]
    public async Task<IActionResult> State(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        /* A player is in exactly one of three places: in a live-ish match, in the
           queue, or nowhere. Checked in that order, because a match beats a queue
           entry: the entry is removed when a match forms. */
        var mine = await db.MatchPlayers
            .Include(mp => mp.Match)
            .Where(mp => mp.PlayerId == me.Id
                      && (mp.Match!.Status == MatchStatus.Accepting
                       || mp.Match.Status == MatchStatus.Voting
                       || mp.Match.Status == MatchStatus.Pending
                       || mp.Match.Status == MatchStatus.Live))
            .OrderByDescending(mp => mp.MatchId)
            .FirstOrDefaultAsync(ct);

        if (mine?.Match is not null)
        {
            var match = mine.Match;
            var roster = await db.MatchPlayers.Where(mp => mp.MatchId == match.Id).ToListAsync(ct);
            var server = match.Status is MatchStatus.Pending or MatchStatus.Live
                ? await db.Servers.FirstOrDefaultAsync(s => s.CurrentMatchId == match.Id, ct)
                : null;

            var phase = match.Status switch
            {
                MatchStatus.Accepting => "found",
                MatchStatus.Voting => "vote",
                MatchStatus.Pending => "connecting",
                _ => "live",
            };

            /* Vote counts, not who voted for what: the launcher shows a tally, and
               knowing which teammate picked which map is nobody's business. */
            var votes = roster
                .Where(r => r.MapVote is not null)
                .GroupBy(r => r.MapVote!)
                .ToDictionary(g => g.Key, g => g.Count());

            return Ok(new QueueState(
                phase, null, match.PlayedAt, match.Id,
                roster.Count(r => r.AcceptedAt is not null), roster.Count,
                mine.AcceptedAt is not null, mine.MapVote,
                string.IsNullOrEmpty(match.Map) ? null : match.Map,
                /* Only once the server is confirmed on this match's map: an
                   address given any earlier drops players onto the last match. */
                server is null || match.ServerReadyAt is null ? null : $"{server.Host}:{server.Port}",
                /* Only the ten people in this match ever see this, and only while
                   it is theirs: it is how they get in, not a secret from them. */
                server?.GamePassword,
                votes));
        }

        /* Your party's place in the queue, found from you in one query: the
           unique index on PartyMember.PlayerId answers "which party", and this
           is polled every second while something is happening, so it reads the
           two columns it renders rather than the rows they sit in. */
        var waiting = await db.Queue
            .Where(q => q.Party!.Members.Any(m => m.PlayerId == me.Id))
            .Select(q => new { q.Mode, q.JoinedAt })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return waiting is null
            ? Ok(new QueueState("idle", null, null, null, 0, 0, false, null, null, null, null, []))
            : Ok(new QueueState("searching", waiting.Mode.ToString(), waiting.JoinedAt, null,
                                0, Matchmaker.PlayersFor(waiting.Mode), false, null, null, null, null, []));
    }

    [HttpPost]
    public async Task<IActionResult> Join([FromBody] JoinRequest body, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        if (!Enum.TryParse<ServerType>(body.Mode ?? "Competitive", ignoreCase: true, out var mode))
            return BadRequest("Mode must be Competitive or Wingman.");

        var party = await parties.EnsureForAsync(me.Id, ct);
        if (!party.IsLeader(me.Id)) return NotLeader();

        /* A party that cannot fit on one team of this mode could never be given
           a match: it would have to be split, and not being split is the whole
           point of a party. Refused here rather than left to wait forever. */
        if (party.Size > Matchmaker.TeamSizeFor(mode))
            return Conflict($"A party of {party.Size} is too large for {mode} (up to {Matchmaker.TeamSizeFor(mode)}).");

        /* Being in a match and in the queue at once would let somebody be matched
           twice, and one member in a match is enough to hold the party back. */
        var busy = await db.MatchPlayers
            .Where(mp => party.MemberIds.Contains(mp.PlayerId)
                && (mp.Match!.Status == MatchStatus.Accepting
                 || mp.Match.Status == MatchStatus.Voting
                 || mp.Match.Status == MatchStatus.Pending
                 || mp.Match.Status == MatchStatus.Live))
            .Select(mp => mp.PlayerId)
            .FirstOrDefaultAsync(ct);

        if (busy != 0)
            return Conflict(busy == me.Id
                ? "You are already in a match."
                : "Somebody in your party is already in a match.");

        if (await db.Queue.AnyAsync(q => q.PartyId == party.Id, ct))
            return Conflict("You are already in the queue.");

        db.Queue.Add(new QueueEntry { PartyId = party.Id, Mode = mode, JoinedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);

        log.LogInformation("Party {PartyId} of {Size} joined the {Mode} queue", party.Id, party.Size, mode);
        return await State(ct);
    }

    [HttpDelete]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        var party = await parties.OfAsync(me.Id, ct);

        /* Leaving when you are not queued is not an error: the launcher may be
           catching up with a match that formed a moment ago. Not being in a
           party at all is the same thing, one step earlier. */
        if (party is null) return await State(ct);

        var waiting = await db.Queue.FirstOrDefaultAsync(q => q.PartyId == party.Id, ct);
        if (waiting is not null)
        {
            // The leader queued for everyone, so the leader is who can stop it.
            if (!party.IsLeader(me.Id)) return NotLeader();

            db.Queue.Remove(waiting);
            await db.SaveChangesAsync(ct);
        }

        return await State(ct);
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept(CancellationToken ct)
    {
        var (me, row, error) = await MyOfferAsync(MatchStatus.Accepting, ct);
        if (error is not null) return error;

        row!.AcceptedAt ??= DateTimeOffset.UtcNow;   // accepting twice is harmless
        await db.SaveChangesAsync(ct);

        log.LogInformation("Player {PlayerId} accepted match {MatchId}", me!.Id, row.MatchId);
        return await State(ct);
    }

    /* Declining cancels the match for everyone.

       The nine who were waiting are put back at the front of the queue by the
       matchmaker's next pass, so they lose a few seconds rather than their place. */
    [HttpPost("decline")]
    public async Task<IActionResult> Decline(CancellationToken ct)
    {
        var (me, row, error) = await MyOfferAsync(MatchStatus.Accepting, ct);
        if (error is not null) return error;

        var match = await db.Matches.FirstAsync(m => m.Id == row!.MatchId, ct);
        match.Status = MatchStatus.Cancelled;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Player {PlayerId} declined match {MatchId}", me!.Id, match.Id);
        return await State(ct);
    }

    [HttpPost("vote")]
    public async Task<IActionResult> Vote([FromBody] VoteRequest body, CancellationToken ct)
    {
        var (_, row, error) = await MyOfferAsync(MatchStatus.Voting, ct);
        if (error is not null) return error;

        if (body.Map is null || !Matchmaker.MapPool.Contains(body.Map))
            return BadRequest("That map is not in the pool.");

        // Changing your mind before the timer ends is allowed.
        row!.MapVote = body.Map;
        await db.SaveChangesAsync(ct);

        return await State(ct);
    }

    /* StatusCode(403) rather than Forbid(): this app registers no authentication
       scheme, and Forbid() asks for a challenge it cannot produce. */
    private IActionResult NotLeader() =>
        StatusCode(StatusCodes.Status403Forbidden, "Only the party leader can do that.");

    /// The match this player is being asked about, if it is in the expected phase.
    private async Task<(Player? Me, MatchPlayer? Row, IActionResult? Error)> MyOfferAsync(
        MatchStatus expected, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return (null, null, Unauthorized());

        var row = await db.MatchPlayers
            .Include(mp => mp.Match)
            .FirstOrDefaultAsync(mp => mp.PlayerId == me.Id && mp.Match!.Status == expected, ct);

        return row is null
            ? (me, null, NotFound(expected == MatchStatus.Accepting
                ? "You have no match waiting to be accepted."
                : "You have no match to vote in."))
            : (me, row, null);
    }
}
