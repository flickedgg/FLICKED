namespace Flicked.Api.Models;

public class Match
{
    public int Id { get; set; }
    public required string Map { get; set; }
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public DateTimeOffset PlayedAt { get; set; }

    public List<MatchPlayer> Players { get; set; } = []; 
}