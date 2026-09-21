namespace Flicked.Api.Models;

public class Player
{
    public Player() { }

    public Player(int id, string name, int rating, int wins, int losses,
                  string? steamId = null, string? avatarUrl = null)
    {
        Id = id;
        Name = name;
        Rating = rating;
        Wins = wins;
        Losses = losses;
        SteamId = steamId;
        AvatarUrl = avatarUrl;
    }

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Rating { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public string? SteamId { get; set; }
    public string? AvatarUrl { get; set; }
}

public record LeaderboardEntry(int Rank, int PlayerId, string Name, int Rating, int Wins, int WinRate);
