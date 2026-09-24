namespace Flicked.Api.Models;

public class Player
{
    public Player() { }

    public Player(int id, string name, int rating, int wins, int losses,
                  string? steamId = null, string? avatarUrl = null, string[]? friends = null)
    {
        Id = id;
        Name = name;
        Rating = rating;
        Wins = wins;
        Losses = losses;
        SteamId = steamId;
        AvatarUrl = avatarUrl;
        Friends = friends ?? Array.Empty<string>();
    }

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Rating { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public string? SteamId { get; set; }
    public string? AvatarUrl { get; set; }

    /* Admins manage game servers from the dashboard. Granted at sign-in from the
       host's configured Steam IDs (Services/Admins.cs), and editable afterwards. */
    public bool IsAdmin { get; set; }
    public string[] Friends = Array.Empty<string>();
}

/* A row on the ladder.

   Delta is the rating change from this player's most recent finished match, so
   the board shows movement and not only standing: "2140" says where somebody is,
   "2140 +24" says they are climbing. Zero when they have never finished a match.

   Provisional marks a rating still inside its first ten matches (see RATING.md).
   A number built from two games sitting next to one built from three hundred is
   worth an asterisk. */
public record LeaderboardEntry(int Rank, int PlayerId, string Name, int Rating,
                               int Wins, int Losses, int WinRate, int Delta, bool Provisional);
