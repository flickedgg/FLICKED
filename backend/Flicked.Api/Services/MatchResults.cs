using Flicked.Api.Data;
using Flicked.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Services;

/* Closing a match, and paying out what it was worth.

   This is the only place in FLICKED where a match becomes Finished, and that is
   on purpose: if two places could finish a match, two places could rate one.

   MatchZy neither retries nor deduplicates, and series_end is documented to
   arrive more than once (docs/MATCHZY.md). A rating system that is merely
   unlikely to double-apply will double-apply, so the right to apply a result is
   not a check but a claim on a row - see FinishAsync. */
public class MatchResults(FlickedDbContext db, ILogger<MatchResults> log)
{
    /* Finish a match and rate everyone in it, or do nothing at all.

       Returns true only for the call that closed the match. Every later call for
       the same match returns false having written nothing, whether it arrives a
       second later or at the same instant. */
    public async Task<bool> FinishAsync(Match match, int scoreA, int scoreB, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        /* The claim.

           One statement decides and writes, so there is no window between the two
           for a duplicate to slip through. Postgres holds a row lock on the match
           for its duration: a concurrent second series_end waits here, then reads
           the committed Finished and changes nothing.

           Raw SQL rather than EF because this has to be a conditional update. EF
           would read the row, compare in memory - against a copy that may have
           been loaded before the duplicate committed - and write unconditionally,
           which is the exact race this replaces. */
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Matches"
               SET "Status" = 'Finished', "ScoreA" = {scoreA}, "ScoreB" = {scoreB}
             WHERE "Id" = {match.Id} AND "Status" <> 'Finished'
            """, ct);

        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            await Refresh(match, ct);
            log.LogInformation("Match {MatchId} was already finished; nothing rated", match.Id);
            return false;
        }

        /* One query for everyone who played, one save for everything that
           changes. At most ten players, so the arithmetic in between costs
           nothing and there is no reason to touch the database per player. */
        var rows = await db.MatchPlayers
            .Include(mp => mp.Player)
            .Where(mp => mp.MatchId == match.Id)
            .ToListAsync(ct);

        Rate(match.Id, rows, scoreA, scoreB);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // the caller is holding a copy of this match and is about to log from it
        await Refresh(match, ct);
        return true;
    }

    /* Everything the result is worth, worked out from the ratings as they stood
       before the match and applied afterwards.

       Split that way on purpose: a player rated halfway through the loop would be
       measured against teammates and opponents who had already moved. */
    private void Rate(int matchId, List<MatchPlayer> rows, int scoreA, int scoreB)
    {
        var played = rows.Where(r => r.Player is not null).ToList();
        var teamA = played.Where(r => r.Team == 0).ToList();
        var teamB = played.Where(r => r.Team == 1).ToList();

        if (teamA.Count == 0 || teamB.Count == 0)
        {
            // no opponent means no contest, and no mean rating to expect anything against
            log.LogWarning("Match {MatchId} finished with an empty team; nothing rated", matchId);
            return;
        }

        var rounds = scoreA + scoreB;
        if (rounds == 0)
        {
            /* 0-0 is a match that did not happen: one that was cancelled and
               reported anyway, or that never went live. Rating it would hand the
               weaker side points for a draw they never played. */
            log.LogWarning("Match {MatchId} finished 0-0; nothing rated", matchId);
            return;
        }

        var meanA = teamA.Average(r => r.Player!.Rating);
        var meanB = teamB.Average(r => r.Player!.Rating);

        /* Performance is relative to this match and to nothing else. When the
           score is too short to divide by - the series-score fallback, or an
           abandoned match - it is skipped and the result decides alone. */
        var scored = rounds >= Rating.MinRatedRounds;
        var meanDuel = scored ? played.Average(r => (r.Kills - r.Deaths) / (double)rounds) : 0;
        var meanAdr = scored ? played.Average(r => (double)r.Adr) : 0;

        foreach (var row in played)
        {
            var player = row.Player!;
            var onTeamA = row.Team == 0;

            var score = scoreA == scoreB ? 0.5
                      : onTeamA == (scoreA > scoreB) ? 1.0
                      : 0.0;

            var performance = scored
                ? Rating.Performance((row.Kills - row.Deaths) / (double)rounds - meanDuel, row.Adr - meanAdr)
                : 0;

            var delta = Rating.Delta(player.Rating, onTeamA ? meanB : meanA, score,
                                     player.Wins + player.Losses, performance);

            /* What the floor allowed, not what the formula asked for, so that a
               player's rating always equals their starting one plus the sum of
               their deltas. A history that does not add up to the leaderboard is
               a support ticket nobody can answer. */
            var applied = Rating.Apply(player.Rating, delta) - player.Rating;

            row.RatingDelta = applied;
            player.Rating += applied;

            if (score == 1) player.Wins++;
            else if (score == 0) player.Losses++;   // a draw is neither
        }

        log.LogInformation("Match {MatchId} rated {Count} players ({ScoreA}-{ScoreB})",
            matchId, played.Count, scoreA, scoreB);
    }

    /* The caller's copy of the match was loaded before any of this and is now
       wrong about the score and the status. Reloading is cheaper than asking
       every caller to remember. */
    private async Task Refresh(Match match, CancellationToken ct)
    {
        var entry = db.Entry(match);
        if (entry.State != EntityState.Detached) await entry.ReloadAsync(ct);
    }
}
