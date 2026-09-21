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
        // Navigation properties
        public Match? Match { get; set; }
        public Player? Player { get; set; }
}
