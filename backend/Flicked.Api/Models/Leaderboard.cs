namespace Flicked.Api.Models;

public record Player(int Id, string Name, int Rating, int Wins, int Losses);
public record LeaderboardEntry(int Rank, string Name, int Rating, int Wins, int WinRate);