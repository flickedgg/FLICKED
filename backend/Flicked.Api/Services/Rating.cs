namespace Flicked.Api.Services;

/* What a match is worth.

   Elo on the result, with a bounded correction for how the player did inside it.
   The reasoning, the constants and the worked examples are in RATING.md at the
   repository root; what is worth repeating here is the rule the rest of the file
   exists to protect:

     a win never loses you rating, and a loss never gains you rating.

   Every ladder that weights statistics heavily ends up measuring statistics, and
   players optimise what is measured: the fight that pads a K/D instead of the one
   that wins the round. Performance is therefore capped at a quarter of what the
   result is worth and cannot change its sign. It decides how far you move, never
   which way.

   Nothing here touches a database, so all of it can be tested as arithmetic. */
public static class Rating
{
    /// What a new account is worth until it has played. AuthController starts players here.
    public const int Start = 1000;

    /* Rating stops falling here. A rating that can fall forever is not a rank, and
       it breaks matchmaking too: QueueEntry.ToleranceAt widens 25 points every ten
       seconds and can never bridge a player who has fallen off the bottom. Far
       enough below Start that no honest player reaches it. */
    public const int Floor = 100;

    /* The Elo scale: 400 points is the gap at which one side is expected to win
       ten times out of eleven. Divisions.cs spans roughly three of these, which is
       the spread a CS ladder wants. */
    public const int Scale = 400;

    /// How much an established player's rating can move on the result alone.
    public const double BaseK = 32;

    /// Matches before a rating stops being a guess. Counted as Wins + Losses.
    public const int ProvisionalMatches = 10;

    /// Performance's share of K, so at most a quarter of what the result is worth.
    public const double PerformanceShare = 0.25;

    /// A result always moves the rating, however one-sided the odds were.
    public const int MinChange = 1;

    /* What a standout game looks like, measured against the rest of the match
       rather than against an absolute standard: a quarter of a net kill per round
       clear of the field, or 25 ADR clear of it. Each is worth half of perf, so
       both together are the full +1. */
    public const double DuelReference = 0.25;
    public const double DamageReference = 25;

    /* Below this the per-round figures are not worth reading, and the rating is
       decided on the result alone. It covers the degraded path where only
       series_end arrived and the saved "score" is the series score (1-0 with one
       map per match), as well as a match abandoned after two rounds. */
    public const int MinRatedRounds = 5;

    /// The chance this rating says the player has against a team of that strength.
    public static double Expected(int rating, double opponentRating) =>
        1.0 / (1.0 + Math.Pow(10, (opponentRating - rating) / Scale));

    /* Twice the usual movement for a brand new account, sliding to the ordinary
       amount over ten matches. A new 1000 is a guess and should move fast; a 2400
       built from hundreds of matches should not. */
    public static double KFactor(int matchesPlayed) =>
        BaseK * (1 + Math.Max(0, ProvisionalMatches - matchesPlayed) / (double)ProvisionalMatches);

    public static bool IsProvisional(int matchesPlayed) => matchesPlayed < ProvisionalMatches;

    /* How this player did relative to the other nine, as −1…+1.

       Both arguments are already differences from the match's own average, which
       is what makes this immune to match length, to a stomp where everybody's
       figures are inflated, and to a patch that changes weapon damage. */
    public static double Performance(double relativeDuel, double relativeAdr) =>
        Math.Clamp(0.5 * (relativeDuel / DuelReference)
                 + 0.5 * (relativeAdr / DamageReference), -1, 1);

    /* The whole thing: what this match changes for one player.

       score is 1 for a win, 0 for a loss, 0.5 for a draw. The last two lines are
       the sign rule — after rounding, a win is still a gain and a loss is still a
       cost, whatever the performance term wanted. */
    public static int Delta(int rating, double opponentRating, double score,
                            int matchesPlayed, double performance)
    {
        var k = KFactor(matchesPlayed);
        var raw = k * (score - Expected(rating, opponentRating))
                + k * PerformanceShare * Math.Clamp(performance, -1, 1);

        // away from zero, so a half point of movement is not silently thrown away
        var delta = (int)Math.Round(raw, MidpointRounding.AwayFromZero);

        if (score > 0.5 && delta < MinChange) return MinChange;
        if (score < 0.5 && delta > -MinChange) return -MinChange;
        return delta;
    }

    /// The new rating, never below the floor.
    public static int Apply(int rating, int delta) => Math.Max(Floor, rating + delta);
}
