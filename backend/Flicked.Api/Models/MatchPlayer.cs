namespace Flicked.Api.Models;
public class MatchPlayer
{
        public int MatchId { get; set; }
        public int PlayerId { get; set; }
        public int Team { get; set; } // 0 = Team A, 1 = Team B
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Adr { get; set; } // Average Damage per Round
        public int RatingDelta { get; set; } // Change in rating after the match

        /* The accept and vote phases live on this row, because both are answers
           this player gave about this match.

           Null AcceptedAt means "has not answered yet", which is different from
           declining: a decline cancels the match, so there is no row left to
           record it on. */
        public DateTimeOffset? AcceptedAt { get; set; }

        /// Their map vote, e.g. "de_mirage". Null until they vote.
        public string? MapVote { get; set; }
        // Navigation properties
        public Match? Match { get; set; }
        public Player? Player { get; set; }
}
